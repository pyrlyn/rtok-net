using System.Net;
using Rtok.Config;

namespace Rtok.Proxy;

/// <summary>The base URLs agents use to reach the proxy (<c>rtok wrap</c>, the host installers).</summary>
public static class ProxyUrls
{
    /// <summary>The address a client dials: the bind address, or loopback when the proxy binds
    /// every interface.</summary>
    /// <param name="proxy">The <c>[proxy]</c> section.</param>
    /// <returns>For example <c>http://127.0.0.1:8790</c>.</returns>
    public static string Root(ProxySection proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        var host = proxy.Bind.Trim();
        if (IPAddress.TryParse(host, out var ip))
        {
            host =
                ip.Equals(IPAddress.Any) ? "127.0.0.1"
                : ip.Equals(IPAddress.IPv6Any) ? "[::1]"
                : ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{ip}]"
                : ip.ToString();
        }
        return $"http://{host}:{proxy.Port}";
    }

    /// <summary><c>ANTHROPIC_BASE_URL</c>: the root; the SDK adds <c>/v1/messages</c>.</summary>
    /// <param name="proxy">The <c>[proxy]</c> section.</param>
    /// <returns>The base URL.</returns>
    public static string Anthropic(ProxySection proxy) => Root(proxy);

    /// <summary><c>OPENAI_BASE_URL</c>: the root plus <c>/v1</c>, which the OpenAI SDKs expect.</summary>
    /// <param name="proxy">The <c>[proxy]</c> section.</param>
    /// <returns>The base URL.</returns>
    public static string OpenAi(ProxySection proxy) => Root(proxy) + "/v1";
}
