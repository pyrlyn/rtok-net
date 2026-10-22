using System.Net;

namespace Rtok.Web;

/// <summary>
/// The Origin and Host checks every request passes. A page another site serves cannot reach the
/// dashboard: its <c>Origin</c> differs from <c>Host</c>. A DNS-rebinding page sends a matching
/// pair, so <c>Host</c> must also be an IP literal or <c>localhost</c>, the only names it cannot
/// point here. A request without <c>Origin</c> (not a browser) passes the Origin check.
/// </summary>
public static class RequestGuard
{
    /// <summary>Whether the <c>Host</c> header names an IP literal or <c>localhost</c>.</summary>
    /// <param name="host">The header; null when missing.</param>
    /// <returns>True when allowed.</returns>
    public static bool HostAllowed(string? host) =>
        Authority(host) is { } authority && RebindSafe(StripPort(authority));

    /// <summary>Whether <paramref name="origin"/> is this server as <paramref name="host"/> names
    /// it: same host and same port, and the host passes <see cref="HostAllowed"/>.</summary>
    /// <param name="origin">The <c>Origin</c> header; null when missing.</param>
    /// <param name="host">The <c>Host</c> header; null when missing.</param>
    /// <returns>True when allowed.</returns>
    public static bool OriginAllowed(string? origin, string? host)
    {
        if (origin is null)
        {
            return true;
        }
        if (OriginAuthority(origin) is not { } o || Authority(host) is not { } h)
        {
            return false;
        }
        var fallback = DefaultPort(origin);
        var samePort = string.Equals(
            PortOf(o) ?? fallback,
            PortOf(h) ?? fallback,
            StringComparison.Ordinal
        );
        var (oh, hh) = (StripPort(o), StripPort(h));
        return samePort
            && string.Equals(oh, hh, StringComparison.OrdinalIgnoreCase)
            && RebindSafe(hh);
    }

    /// <summary>Whether <paramref name="host"/> (a <c>[web] host</c> value) is a loopback
    /// address or <c>localhost</c>.</summary>
    /// <param name="host">The bind host.</param>
    /// <returns>True for loopback.</returns>
    public static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));

    private static bool RebindSafe(string host) =>
        IPAddress.TryParse(host, out _)
        || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static string DefaultPort(string origin)
    {
        var scheme = origin.Split("://", 2)[0];
        return
            string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "wss", StringComparison.OrdinalIgnoreCase)
            ? "443"
            : "80";
    }

    private static string? OriginAuthority(string origin)
    {
        var at = origin.IndexOf("://", StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }
        var rest = origin[(at + 3)..];
        var end = rest.IndexOfAny(['/', '?', '#']);
        var authority = end < 0 ? rest : rest[..end];
        authority = authority[(authority.LastIndexOf('@') + 1)..];
        return authority.Length == 0 ? null : authority;
    }

    // Several Host values joined by a comma could pass the comparison with a crafted Origin,
    // so such a header is refused.
    private static string? Authority(string? host)
    {
        var trimmed = host?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Contains(',', StringComparison.Ordinal))
        {
            return null;
        }
        var authority = trimmed[(trimmed.LastIndexOf('@') + 1)..];
        return authority.Length == 0 ? null : authority;
    }

    private static string? PortOf(string authority)
    {
        if (authority.StartsWith('['))
        {
            var close = authority.IndexOf(']', StringComparison.Ordinal);
            return close >= 0 && authority.Length > close + 1 && authority[close + 1] == ':'
                ? authority[(close + 2)..]
                : null;
        }
        var colon = authority.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 && colon == authority.LastIndexOf(':') ? authority[(colon + 1)..] : null;
    }

    private static string StripPort(string authority)
    {
        if (authority.StartsWith('['))
        {
            var close = authority.IndexOf(']', StringComparison.Ordinal);
            return close >= 0 ? authority[1..close] : authority;
        }
        var colon = authority.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 && colon == authority.LastIndexOf(':') ? authority[..colon] : authority;
    }
}
