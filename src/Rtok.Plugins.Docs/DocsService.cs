using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rtok.Plugin.Sdk;
using Rtok.Store;
using ZstdSharp;

namespace Rtok.Plugins.Docs;

/// <summary>
/// The docs tools (<c>docs_resolve</c>, <c>docs_query</c>, <c>docs_get</c>) and
/// <c>rtok docs fetch</c>, over the store's FTS table. Each call first syncs the workspace's
/// local docs (<see cref="LocalDocs"/>), as rtok did, so an edited <c>llms.txt</c> is searched
/// at once and unchanged bytes keep their ids.
/// </summary>
/// <param name="store">The store the docs live in.</param>
/// <param name="config">The plugin's settings.</param>
/// <param name="home">rtok's home; fetched archives are kept under <c>docs/</c> there.</param>
/// <param name="time">The clock for <c>fetched_unix</c>.</param>
public sealed class DocsService(
    RtokStore store,
    DocsConfig config,
    string home,
    TimeProvider? time = null
)
{
    private static readonly JsonWriterOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>
    /// <c>docs_resolve</c>: <c>{"name","version","cached"}</c> for the version a lockfile in
    /// <paramref name="cwd"/> pins, <c>llms</c> as version <c>local</c>, or
    /// <c>not in a lockfile: &lt;name&gt;</c>.
    /// </summary>
    public string Resolve(string name, string cwd)
    {
        ArgumentNullException.ThrowIfNull(name);
        var local = SyncLocal(cwd);
        if (string.Equals(name, LocalDocs.Name, StringComparison.Ordinal))
        {
            return Json(w =>
                Package(w, LocalDocs.Name, "local", store.DocCrateCached(LocalDocs.Name, local))
            );
        }
        return Lockfiles.Resolve(cwd, name) is { } pinned
            ? Json(w =>
                Package(
                    w,
                    pinned.Name,
                    pinned.Version,
                    store.DocCrateCached(pinned.StoreName, pinned.Version)
                )
            )
            : MissLock(name);
    }

    /// <summary>
    /// <c>docs_query</c>: the best FTS hits for <paramref name="query"/> in one cached package of
    /// the workspace <paramref name="cwd"/>, as
    /// <c>[{"id","path","kind","snippet"}]</c>, each snippet the first
    /// <see cref="DocsConfig.SnippetChars"/> characters of its body, kept while the whole reply's
    /// estimate stays within <see cref="DocsConfig.MaxTokens"/>. The version is
    /// <paramref name="version"/>, else the lockfile's; a package not cached answers
    /// <c>not cached: rtok docs fetch &lt;name&gt;</c>. A reply with hits records one
    /// <see cref="Measurement"/> against the full bodies of every hit considered.
    /// </summary>
    public string Query(string name, string? version, string query, string cwd, Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(cx);
        var local = SyncLocal(cwd);
        if (Scope(name, version, cwd, local) is not var (storeName, ver))
        {
            return NotCached(name);
        }
        var hits = store.SearchDocItems(storeName, ver, query, Math.Max(config.QueryLimit, 1));
        var snippet = Math.Max(config.SnippetChars, 1);
        var budget = Math.Max(config.MaxTokens, 1);
        var host = cx.Host;
        var kept = 0;
        var text = "[]";
        for (var n = 1; n <= hits.Count; n++)
        {
            var trial = Json(w => Hits(w, hits, n, snippet));
            if (host.Estimate(trial, TextClass.Prose) > budget)
            {
                break;
            }
            (kept, text) = (n, trial);
        }
        if (kept > 0)
        {
            var bodies = hits.Select(h => h.Docs).ToList();
            host.Record(
                new Measurement
                {
                    Plugin = DocsPlugin.Id,
                    Kind = "query",
                    BeforeBytes = bodies.Sum(b => (long)Encoding.UTF8.GetByteCount(b)),
                    AfterBytes = Encoding.UTF8.GetByteCount(text),
                    EstBefore = host.Estimate(string.Join('\n', bodies), TextClass.Prose),
                    EstAfter = host.Estimate(text, TextClass.Prose),
                    RefId = $"{name}@{ver}",
                    CallId = host.CallId,
                }
            );
        }
        return text;
    }

    /// <summary><c>docs_get</c>: one item, <c>{"id","path","kind","docs"}</c>.</summary>
    /// <exception cref="DocsException">No item has <paramref name="id"/>.</exception>
    public string Get(long id) =>
        store.GetDocItem(id) is { } item
            ? Json(w =>
            {
                w.WriteStartObject();
                w.WriteNumber("id", item.Id);
                w.WriteString("path", item.Path);
                w.WriteString("kind", item.Kind);
                w.WriteString("docs", item.Docs);
                w.WriteEndObject();
            })
            : throw new DocsException($"unknown doc id: {id}");

    /// <summary>
    /// <c>rtok docs fetch &lt;name&gt;</c>: downloads the docs of the version a lockfile in
    /// <paramref name="cwd"/> pins, keeps the archive and its SHA-256 under
    /// <c>&lt;home&gt;/docs/</c>, and ingests it (unchanged bytes keep their ids). An answer other
    /// than 200, or a body over the limit, is reported and leaves the cache as it was.
    /// </summary>
    /// <exception cref="DocsException">The upstream timed out, could not be reached, or sent docs that do not parse.</exception>
    public async Task<string> FetchAsync(
        string name,
        string cwd,
        HttpClient http,
        DocsUpstream upstream,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(upstream);
        SyncLocal(cwd);
        if (Lockfiles.Resolve(cwd, name) is not { } pinned)
        {
            return MissLock(name);
        }
        var label = pinned.Ecosystem == Ecosystem.Cargo ? "docs.rs" : "nuget.org";
        var (status, bytes) = await DownloadAsync(http, upstream, pinned, label, cancellationToken)
            .ConfigureAwait(false);
        if (status != HttpStatusCode.OK)
        {
            return $"{label} returned {(int)status}";
        }
        if (bytes is null)
        {
            return $"{label} sent more than {upstream.MaxDownloadBytes} bytes";
        }
        var (format, items) = Index(pinned, bytes, upstream.MaxUnpackedBytes, label);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Keep(pinned, bytes, sha);
        store.SyncDocCrate(
            new Rtok.Store.Model.DocCrate
            {
                Name = pinned.StoreName,
                Version = pinned.Version,
                Sha256 = sha,
                FormatVersion = format,
                FetchedUnix = _time.GetUtcNow().ToUnixTimeSeconds(),
            },
            items
        );
        return $"cached {pinned.Name} {pinned.Version} ({items.Count} items)";
    }

    private static string MissLock(string name) => $"not in a lockfile: {name}";

    private static string NotCached(string name) => $"not cached: rtok docs fetch {name}";

    // The stored (name, version) a query reads, or null when that package is not cached.
    private (string Name, string Version)? Scope(
        string name,
        string? version,
        string cwd,
        string local
    )
    {
        if (
            string.Equals(name, LocalDocs.Name, StringComparison.Ordinal)
            && (
                string.IsNullOrEmpty(version)
                || string.Equals(version, "local", StringComparison.Ordinal)
            )
        )
        {
            return store.DocCrateCached(name, local) ? (name, local) : null;
        }
        if (!string.IsNullOrEmpty(version))
        {
            foreach (var ecosystem in (ReadOnlySpan<Ecosystem>)[Ecosystem.Cargo, Ecosystem.NuGet])
            {
                var key = new PinnedPackage(name, version, ecosystem).StoreName;
                if (store.DocCrateCached(key, version))
                {
                    return (key, version);
                }
            }
            return null;
        }
        return
            Lockfiles.Resolve(cwd, name) is { } pinned
            && store.DocCrateCached(pinned.StoreName, pinned.Version)
            ? (pinned.StoreName, pinned.Version)
            : null;
    }

    // Brings the store's copy of cwd's local docs in line with the files; returns their version.
    private string SyncLocal(string cwd)
    {
        ArgumentNullException.ThrowIfNull(cwd);
        var version = LocalDocs.Version(cwd);
        if (LocalDocs.Read(cwd) is var (sha, items))
        {
            store.SyncDocCrate(
                new Rtok.Store.Model.DocCrate
                {
                    Name = LocalDocs.Name,
                    Version = version,
                    Sha256 = sha,
                    FetchedUnix = _time.GetUtcNow().ToUnixTimeSeconds(),
                },
                items
            );
        }
        else if (store.DocCrateCached(LocalDocs.Name, version))
        {
            store.DeleteDocCrate(LocalDocs.Name, version);
        }
        return version;
    }

    private static async Task<(HttpStatusCode Status, byte[]? Bytes)> DownloadAsync(
        HttpClient http,
        DocsUpstream upstream,
        PinnedPackage pinned,
        string label,
        CancellationToken cancellationToken
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(upstream.Timeout);
        try
        {
            using var response = await http.GetAsync(
                    upstream.For(pinned),
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token
                )
                .ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return (response.StatusCode, null);
            }
            if (response.Content.Headers.ContentLength > upstream.MaxDownloadBytes)
            {
                return (HttpStatusCode.OK, null);
            }
            var body = await response
                .Content.ReadAsStreamAsync(timeout.Token)
                .ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                return (
                    HttpStatusCode.OK,
                    await ReadAtMostAsync(body, upstream.MaxDownloadBytes, timeout.Token)
                        .ConfigureAwait(false)
                );
            }
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DocsException(
                $"{label} did not answer within {upstream.Timeout.TotalSeconds:0} s",
                e
            );
        }
        catch (HttpRequestException e)
        {
            throw new DocsException($"{label} could not be reached: {e.Message}", e);
        }
    }

    // The whole stream, or null when it holds more than max bytes.
    private static async Task<byte[]?> ReadAtMostAsync(
        Stream stream,
        long max,
        CancellationToken cancellationToken
    )
    {
        using var buffer = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while (
                (read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0
            )
            {
                if (buffer.Length + read > max)
                {
                    return null;
                }
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    // The whole stream, or null when it unpacks to more than max bytes.
    private static byte[]? Unpack(Stream stream, long max)
    {
        using var buffer = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = stream.Read(chunk)) > 0)
            {
                if (buffer.Length + read > max)
                {
                    return null;
                }
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    private static (long Format, IReadOnlyList<DocEntry> Items) Index(
        PinnedPackage pinned,
        byte[] bytes,
        long maxUnpacked,
        string label
    )
    {
        try
        {
            if (pinned.Ecosystem == Ecosystem.NuGet)
            {
                using var nupkg = new MemoryStream(bytes, writable: false);
                return (0, NuGetXmlDocs.Read(nupkg));
            }
            using var zst = new DecompressionStream(new MemoryStream(bytes, writable: false));
            var json =
                Unpack(zst, maxUnpacked)
                ?? throw new DocsException(
                    $"{label} rustdoc JSON unpacks to more than {maxUnpacked} bytes"
                );
            using var doc = JsonDocument.Parse(json);
            return RustdocIndex.Read(doc.RootElement);
        }
        catch (Exception e) when (e is ZstdException or JsonException or InvalidDataException)
        {
            throw new DocsException($"{label} sent docs that do not parse: {e.Message}", e);
        }
    }

    // The raw archive and its SHA-256, kept as rtok kept them: <home>/docs/<name>/<version>/.
    private void Keep(PinnedPackage pinned, byte[] bytes, string sha)
    {
        var (dir, file) =
            pinned.Ecosystem == Ecosystem.Cargo
                ? (Path.Join(home, "docs", pinned.Name, pinned.Version), "rustdoc.json.zst")
                : (
                    Path.Join(
                        home,
                        "docs",
                        "nuget",
                        pinned.Name.ToLowerInvariant(),
                        pinned.Version
                    ),
                    $"{pinned.Name.ToLowerInvariant()}.{pinned.Version}.nupkg"
                );
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Join(dir, file), bytes);
        File.WriteAllText(Path.Join(dir, file + ".sha256"), sha + "\n");
    }

    private static void Package(Utf8JsonWriter w, string name, string version, bool cached)
    {
        w.WriteStartObject();
        w.WriteString("name", name);
        w.WriteString("version", version);
        w.WriteBoolean("cached", cached);
        w.WriteEndObject();
    }

    private static void Hits(Utf8JsonWriter w, IReadOnlyList<DocHit> hits, int count, int snippet)
    {
        w.WriteStartArray();
        foreach (var hit in hits.Take(count))
        {
            w.WriteStartObject();
            w.WriteNumber("id", hit.Id);
            w.WriteString("path", hit.Path);
            w.WriteString("kind", hit.Kind);
            w.WriteString("snippet", Prefix(hit.Docs, snippet));
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    // The first `chars` characters (code points, as SQLite's substr counts them).
    internal static string Prefix(string text, int chars)
    {
        var end = 0;
        var n = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (n++ == chars)
            {
                break;
            }
            end += rune.Utf16SequenceLength;
        }
        return text[..end];
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, Compact))
        {
            write(w);
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
