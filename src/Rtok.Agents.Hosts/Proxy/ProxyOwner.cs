using System.Net;
using System.Net.Sockets;

namespace Rtok.Agents.Hosts.Proxy;

/// <summary>
/// Whose a base URL is (rtok <c>loopback_port</c>): rtok's when it is the URL rtok writes, or an
/// <c>http(s)</c> URL whose authority is a loopback host on the proxy's port. The host is read from
/// the parsed authority, so a loopback address in a path, a query or user info is foreign.
/// </summary>
internal static class ProxyOwner
{
    /// <summary>The port of an <c>http(s)</c> URL on <c>localhost</c>, <c>127.0.0.0/8</c> or <c>[::1]</c>; null for anything else.</summary>
    public static int? LoopbackPort(string value)
    {
        if (
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
        )
        {
            return null;
        }
        var local = uri.HostNameType switch
        {
            UriHostNameType.Dns => string.Equals(
                uri.Host,
                "localhost",
                StringComparison.OrdinalIgnoreCase
            ),
            UriHostNameType.IPv4 or UriHostNameType.IPv6 => IPAddress.TryParse(
                uri.Host.Trim('[', ']'),
                out var ip
            )
                && (
                    ip.AddressFamily == AddressFamily.InterNetwork
                        ? IPAddress.IsLoopback(ip)
                        : ip.Equals(IPAddress.IPv6Loopback)
                ),
            _ => false,
        };
        return local ? uri.Port : null;
    }

    /// <summary>Whether <paramref name="value"/> is rtok's: <paramref name="url"/> itself, or a loopback URL on <paramref name="port"/>.</summary>
    public static bool Ours(string value, string url, int port) =>
        string.Equals(value, url, StringComparison.Ordinal) || LoopbackPort(value) == port;
}
