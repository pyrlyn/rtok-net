namespace Rtok.Proxy;

/// <summary>Where a request goes upstream.</summary>
public static class UpstreamUrl
{
    /// <summary>Joins an upstream base (<c>[proxy] upstream</c>, which may carry a path) with the
    /// request path and query. The path's percent-escapes stay as the client sent them, empty
    /// segments collapse so no <c>//</c> appears, and the request's query replaces the base's.</summary>
    /// <param name="baseUrl">The upstream base URL.</param>
    /// <param name="path">The request path, still percent-encoded.</param>
    /// <param name="query">The request query without <c>?</c>; null keeps the base's.</param>
    /// <returns>The absolute upstream URL.</returns>
    /// <exception cref="FormatException">The base is empty or not an http(s) URL.</exception>
    public static string Join(string baseUrl, string path, string? query)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new FormatException("proxy upstream is empty");
        }
        if (
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (
                !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            )
        )
        {
            throw new FormatException($"proxy upstream is not an http(s) URL: {baseUrl}");
        }
        var joined = new System.Text.StringBuilder(uri.GetLeftPart(UriPartial.Authority));
        joined.Append(uri.AbsolutePath.TrimEnd('/'));
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            joined.Append('/').Append(segment);
        }
        if (joined.Length == uri.GetLeftPart(UriPartial.Authority).Length)
        {
            joined.Append('/');
        }
        if (query is not null)
        {
            joined.Append('?').Append(query);
        }
        else if (uri.Query.Length > 0)
        {
            joined.Append(uri.Query);
        }
        return joined.ToString();
    }
}
