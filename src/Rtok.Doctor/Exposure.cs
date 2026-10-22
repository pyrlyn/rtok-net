using System.Net;
using Rtok.Mcp.Install;

namespace Rtok.Doctor;

/// <summary>
/// What rtok would expose beyond this machine or user (rtok I-S1): a server bound off
/// loopback, the MCP endpoint without a token or TLS, and <c>~/.rtok</c> readable by others.
/// </summary>
internal static class Exposure
{
    /// <summary>At most this many file mode lines; the rest are counted.</summary>
    private const int MaxModes = 10;

    private const UnixFileMode Group =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute;
    private const UnixFileMode Other =
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    public static List<string> Find(DoctorHome home)
    {
        var config = home.Config;
        var lines = new List<string>();
        if (config.Proxy.Enabled && !Loopback(config.Proxy.Bind))
        {
            lines.Add(
                $"[proxy] bind = {config.Proxy.Bind} is not loopback: anyone who reaches it can spend your API key"
            );
        }
        if (!Loopback(config.Web.Host))
        {
            lines.Add(
                $"[web] host = {config.Web.Host} is not loopback: the dashboard is reachable from the network"
            );
        }
        var http = config.Mcp.Http;
        if (http.Length > 0 && !Loopback(Host(http)))
        {
            var token =
                home.Var("RTOK_MCP_TOKEN")
                ?? (config.Mcp.Token.Length > 0 ? config.Mcp.Token : null);
            if (token is null)
            {
                lines.Add(
                    $"[mcp] http = {http} is not loopback and no token is set: set [mcp] token or RTOK_MCP_TOKEN"
                );
            }
            if (!config.Mcp.PublicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(
                    $"[mcp] http = {http} is not loopback and [mcp] public_url is not https: the bearer token would cross the network in clear"
                );
            }
        }
        if (home.Env.Os != McpOs.Windows)
        {
            lines.AddRange(Modes(home));
        }
        return lines;
    }

    /// <summary>Directories under rtok's home looser than 0700, files looser than 0600.</summary>
    private static IEnumerable<string> Modes(DoctorHome home)
    {
        var root = home.Fs.Path.GetDirectoryName(home.PathOf(home.Config.Core.DbPath));
        if (OperatingSystem.IsWindows() || root is null || !home.Fs.Directory.Exists(root))
        {
            yield break;
        }
        var loose = new List<string>();
        try
        {
            foreach (
                var dir in home
                    .Fs.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                    .Prepend(root)
            )
            {
                if (
                    (home.Fs.DirectoryInfo.New(dir).UnixFileMode & (Group | Other))
                    != UnixFileMode.None
                )
                {
                    loose.Add($"{dir} is a directory open to others: run `chmod 700 {dir}`");
                }
            }
            foreach (
                var file in home.Fs.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            )
            {
                if ((home.Fs.File.GetUnixFileMode(file) & (Group | Other)) != UnixFileMode.None)
                {
                    loose.Add($"{file} is open to others: run `chmod 600 {file}`");
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            loose.Add($"{root} cannot be checked: {e.Message}");
        }
        foreach (var line in loose.Take(MaxModes))
        {
            yield return line;
        }
        if (loose.Count > MaxModes)
        {
            yield return $"… and {loose.Count - MaxModes} more under {root}";
        }
    }

    /// <summary>The host of <c>host:port</c> or <c>[v6]:port</c>.</summary>
    private static string Host(string hostport)
    {
        if (hostport.StartsWith('['))
        {
            var close = hostport.IndexOf(']', StringComparison.Ordinal);
            return close < 0 ? hostport : hostport[1..close];
        }
        var colon = hostport.LastIndexOf(':');
        return colon < 0 ? hostport : hostport[..colon];
    }

    internal static bool Loopback(string host)
    {
        var h = host.Trim('[', ']');
        return string.Equals(h, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(h, out var ip) && IPAddress.IsLoopback(ip));
    }
}
