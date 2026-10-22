using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rtok.Config;
using Rtok.Log;
using Rtok.Store;
using Serilog;

namespace Rtok.Proxy;

/// <summary>The summary compressor client (rtok T385.9.1). One summarisation request per archived
/// tool result, sent to the proxy's own <c>internal</c> lane (so the spend is tagged and
/// <c>[proxy.routing]</c> applies), kept beside the archive row per compressor version, and stored
/// only when it still carries every must-keep span of the original. Anthropic Messages wire
/// only. Nothing here exists unless <c>[plugins.compress.llm] enabled</c>.</summary>
internal sealed class Compressor : IDisposable
{
    private const string DefaultAnthropicVersion = "2023-06-01";
    private const int ShortId = 12;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly CompressLlmSection _cfg;
    private readonly RtokStore _store;
    private readonly HttpClient _client;
    private readonly ILogger _log;

    private Compressor(
        CompressLlmSection cfg,
        RtokStore store,
        HttpClient client,
        ILogger logger,
        Uri url
    )
    {
        _cfg = cfg;
        _store = store;
        _client = client;
        _log = logger.ForSource("plugin").Named("compress");
        Url = url;
        Version = CompressPrompt.Version(cfg.Model);
    }

    /// <summary>Where the summary requests go: <c>[plugins.compress.llm] url</c>, or this proxy.</summary>
    public Uri Url { get; }

    /// <summary>The key half beside the archive id: prompt hash and model.</summary>
    public string Version { get; }

    /// <summary>Null while the flag is off, so a disabled proxy holds no client at all.</summary>
    /// <param name="config">The loaded config.</param>
    /// <param name="store">Where archives are read and summaries written.</param>
    /// <param name="logger">rtok's log.</param>
    /// <param name="handler">The HTTP handler, for tests; null builds a plain one.</param>
    public static Compressor? Create(
        RtokConfig config,
        RtokStore store,
        ILogger logger,
        HttpMessageHandler? handler = null
    )
    {
        var llm = config.Plugins.Compress.Llm;
        if (!llm.Enabled)
        {
            return null;
        }
        var url = llm.Url.Trim();
        var root = url.Length == 0 ? ProxyUrls.Root(config.Proxy) : url.TrimEnd('/');
        var client = handler is null ? new HttpClient() : new HttpClient(handler, false);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(llm.TimeoutS, 1UL, int.MaxValue));
        return new Compressor(llm, store, client, logger, new Uri(root + "/v1/messages"));
    }

    /// <summary>Summarises <paramref name="archiveId"/> unless it already is or is not worth a
    /// call, checks the result against the original's must-keep spans, and stores it. Never
    /// throws: a failure or a rejected summary is warned and reported in the result.</summary>
    public async Task<SummaryResult> RunAsync(
        CompressAuth auth,
        string archiveId,
        CancellationToken cancellationToken = default
    )
    {
        SummaryResult result;
        try
        {
            result = await TryRunAsync(auth, archiveId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
            when (e
                    is HttpRequestException
                        or JsonException
                        or InvalidDataException
                        or ObjectDisposedException
                || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            )
        {
            result = new(SummaryOutcome.Failed, e.Message);
        }
        var id = archiveId[..Math.Min(archiveId.Length, ShortId)];
        if (result.Outcome is SummaryOutcome.Failed)
        {
            _log.Warning("{Message}", $"{id}: {result.Detail}");
        }
        else if (result.Outcome is SummaryOutcome.Rejected)
        {
            _log.Warning("{Message}", $"{id}: summary dropped {result.Detail}");
        }
        return result;
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    /// <summary>A diff or source file: logs and prose summarise well, these lose the exact lines
    /// the agent edits against, so they wait for <c>code = true</c>.</summary>
    internal static bool IsCode(string text)
    {
        var lines = text.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        if (
            lines.Exists(l =>
                l.StartsWith("diff --git ", StringComparison.Ordinal)
                || l.StartsWith("@@ ", StringComparison.Ordinal)
            )
        )
        {
            return true;
        }
        var coded = lines.Count(l => l.TrimEnd() is [.., '{' or '}' or ';']);
        return coded * 100 >= lines.Count * 35;
    }

    private static SummaryResult Skip(string reason) => new(SummaryOutcome.Skipped, reason);

    private static SummaryResult Fail(string what, Exception e) =>
        new(SummaryOutcome.Failed, $"{what}: {e.Message}");

    private async Task<SummaryResult> TryRunAsync(
        CompressAuth auth,
        string archiveId,
        CancellationToken cancellationToken
    )
    {
        if (Original(archiveId, out var bytes, out var text) is { } stop)
        {
            return stop;
        }
        if ((ulong)bytes.Length > _cfg.MaxInputBytes)
        {
            return Skip("too large");
        }
        if (!_cfg.Code && IsCode(text))
        {
            return Skip("diff or source");
        }
        var spans = MustKeep.Spans(text);
        if (spans.Count > _cfg.MaxSpans)
        {
            return Skip("too many must-keep spans");
        }
        var summary = await AskAsync(auth, text, spans, cancellationToken).ConfigureAwait(false);
        // A summary the agent must not rely on is worse than the extractive view: the original
        // is under `expand`, but the agent would not know to look.
        if (summary.Length == 0 || Encoding.UTF8.GetByteCount(summary) >= bytes.Length)
        {
            return Skip("summary not shorter than the original");
        }
        var gone = MustKeep.Dropped(spans, summary);
        if (gone.Count > 0)
        {
            return new(
                SummaryOutcome.Rejected,
                $"{gone.Count} must-keep span(s), not stored: {string.Join(" | ", gone.Select(s => s.Text))}"
            );
        }
        try
        {
            return _store.PutArchiveSummary(archiveId, Version, summary)
                ? new(SummaryOutcome.Stored)
                : new(SummaryOutcome.Cached);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Fail("cache write", e);
        }
    }

    /// <summary>The archived text, or why there is nothing to summarise.</summary>
    private SummaryResult? Original(string archiveId, out byte[] bytes, out string text)
    {
        (bytes, text) = ([], "");
        try
        {
            if (_store.GetArchiveSummary(archiveId, Version) is not null)
            {
                return new(SummaryOutcome.Cached);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Fail("cache read", e);
        }
        try
        {
            if (_store.GetArchive(archiveId) is not { } stored)
            {
                return Skip("archive payload missing");
            }
            bytes = stored;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Fail("archive read", e);
        }
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Skip("not text");
        }
        return null;
    }

    /// <summary>One request on the <c>internal</c> lane; the model's text, trimmed.</summary>
    private async Task<string> AskAsync(
        CompressAuth auth,
        string text,
        List<MustKeepSpan> spans,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new ByteArrayContent(Body(CompressPrompt.Fill(text, spans))),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation(Lanes.Header, Lane.Internal.Name());
        if (
            !auth.Headers.Any(h =>
                h.Key.Equals("anthropic-version", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            request.Headers.TryAddWithoutValidation("anthropic-version", DefaultAnthropicVersion);
        }
        foreach (var (name, value) in auth.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
        using var response = await _client
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        var reply = await response
            .Content.ReadAsByteArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"upstream status {(int)response.StatusCode}");
        }
        using var doc = JsonDocument.Parse(reply);
        var root = doc.RootElement;
        if (root.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidDataException("the reply is not a JSON object");
        }
        if (root.TryGetProperty("stop_reason", out var stop) && stop.ValueEquals("max_tokens"))
        {
            throw new InvalidDataException("summary cut off at max_output_tokens");
        }
        var summary = new StringBuilder();
        if (
            root.TryGetProperty("content", out var content)
            && content.ValueKind is JsonValueKind.Array
        )
        {
            foreach (var block in content.EnumerateArray())
            {
                if (
                    block.ValueKind is JsonValueKind.Object
                    && block.TryGetProperty("type", out var type)
                    && type.ValueEquals("text")
                    && block.TryGetProperty("text", out var part)
                    && part.ValueKind is JsonValueKind.String
                )
                {
                    summary.Append(part.GetString());
                }
            }
        }
        return summary.ToString().Trim();
    }

    private byte[] Body(string user)
    {
        using var buffer = new MemoryStream();
        // Relaxed: the prompt's tags and the tool result go out as written, not as \u003C.
        using (
            var json = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
            )
        )
        {
            json.WriteStartObject();
            json.WriteString("model", _cfg.Model);
            json.WriteNumber("max_tokens", _cfg.MaxOutputTokens);
            json.WriteString("system", CompressPrompt.System);
            json.WriteStartArray("messages");
            json.WriteStartObject();
            json.WriteString("role", "user");
            json.WriteString("content", user);
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }
}
