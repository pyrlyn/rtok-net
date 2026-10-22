using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Primitives;

namespace Rtok.Web;

/// <summary>
/// Serves the client: a path is decoded once per segment (400 when it names no file); a file
/// goes out in the precompressed form the browser accepts; fingerprinted <c>_framework</c> files
/// are immutable for a year and everything else revalidates; a path with no file and no
/// extension is a page route and gets <c>index.html</c>. Every response carries the CSP.
/// </summary>
internal sealed partial class AssetServer(ClientFiles files)
{
    /// <summary>The CSP (N13): scripts and styles from this origin only, WebAssembly
    /// compilation, and inline style attributes for Radzen.</summary>
    public const string Csp =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; "
        + "style-src-attr 'unsafe-inline'; img-src 'self' data:; font-src 'self'; "
        + "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'none'; "
        + "frame-ancestors 'none'";

    private const string Immutable = "public, max-age=31536000, immutable";
    private const string Index = "index.html";

    private static readonly FileExtensionContentTypeProvider Types = ContentTypes();

    public async Task ServeAsync(HttpContext context)
    {
        var response = context.Response;
        Secure(response.Headers);
        if (
            !HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
        )
        {
            response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }
        var raw =
            context.Features.Get<IHttpRequestFeature>()?.RawTarget
            ?? context.Request.Path.Value
            ?? "/";
        var query = raw.IndexOf('?', StringComparison.Ordinal);
        if (AssetPath.Decode(query < 0 ? raw : raw[..query]) is not { } path)
        {
            response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        if (path.Length == 0)
        {
            path = Index;
        }
        if (!Has(path))
        {
            if (Path.HasExtension(path) || path.StartsWith("ws/", StringComparison.Ordinal))
            {
                response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            path = Index;
        }
        await SendAsync(context, path).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="path"/> sits under <c>_framework/</c> with a content hash
    /// in its name, so its bytes never change under that name.</summary>
    /// <param name="path">A decoded relative path.</param>
    /// <returns>True when fingerprinted.</returns>
    public static bool Fingerprinted(string path) =>
        path.StartsWith("_framework/", StringComparison.Ordinal) && Hash().IsMatch(path);

    private bool Has(string path) =>
        files.Exists(path) || files.Exists(path + ".br") || files.Exists(path + ".gz");

    private async Task SendAsync(HttpContext context, string path)
    {
        var response = context.Response;
        var accept = context.Request.Headers.AcceptEncoding;
        var (file, encoding) = Pick(path, accept);
        response.ContentType = Types.TryGetContentType(path, out var type)
            ? type
            : "application/octet-stream";
        response.Headers.CacheControl =
            string.Equals(path, Index, StringComparison.Ordinal) ? "no-store"
            : Fingerprinted(path) ? Immutable
            : "no-cache";
        if (files.Exists(path + ".br") || files.Exists(path + ".gz"))
        {
            response.Headers.Vary = "Accept-Encoding";
        }
        if (encoding is not null)
        {
            response.Headers.ContentEncoding = encoding;
        }
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }
        var stream = files.Open(file);
        await using (stream.ConfigureAwait(false))
        {
            if (encoding is null && !string.Equals(file, path, StringComparison.Ordinal))
            {
                // Only the gzip copy is embedded and the client takes neither coding.
                var plain = new GZipStream(stream, CompressionMode.Decompress);
                await using (plain.ConfigureAwait(false))
                {
                    await plain
                        .CopyToAsync(response.Body, context.RequestAborted)
                        .ConfigureAwait(false);
                }
                return;
            }
            response.ContentLength = stream.CanSeek ? stream.Length : null;
            await stream.CopyToAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private (string File, string? Encoding) Pick(string path, StringValues accept)
    {
        if (Accepts(accept, "br") && files.Exists(path + ".br"))
        {
            return (path + ".br", "br");
        }
        if (Accepts(accept, "gzip") && files.Exists(path + ".gz"))
        {
            return (path + ".gz", "gzip");
        }
        return files.Exists(path) ? (path, null) : (path + ".gz", null);
    }

    /// <summary>Whether the <c>Accept-Encoding</c> values take <paramref name="coding"/>;
    /// <c>q=0</c> refuses it.</summary>
    internal static bool Accepts(StringValues accept, string coding)
    {
        foreach (var value in accept)
        {
            foreach (var item in (value ?? "").Split(','))
            {
                var parts = item.Split(';');
                if (!string.Equals(parts[0].Trim(), coding, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var refused = parts
                    .Skip(1)
                    .Select(p => p.Trim().Replace(" ", "", StringComparison.Ordinal))
                    .Any(p => p is "q=0" or "q=0.0" or "q=0.00" or "q=0.000");
                return !refused;
            }
        }
        return false;
    }

    private static void Secure(IHeaderDictionary headers)
    {
        headers.ContentSecurityPolicy = Csp;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
    }

    private static FileExtensionContentTypeProvider ContentTypes()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".wasm"] = "application/wasm";
        provider.Mappings[".dat"] = "application/octet-stream";
        provider.Mappings[".js"] = "text/javascript";
        return provider;
    }

    [GeneratedRegex(
        @"\.[0-9a-z]{10}\.[^/.]+$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100
    )]
    private static partial Regex Hash();
}
