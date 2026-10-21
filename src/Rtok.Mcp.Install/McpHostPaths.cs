using System.IO.Abstractions;

namespace Rtok.Mcp.Install;

/// <summary>Default config paths, built with the file system's own path rules.</summary>
internal sealed class McpHostPaths(McpHostEnv env, IFileSystem fs)
{
    /// <summary>A path under the home directory; <c>/</c> separates the parts.</summary>
    public string Home(string relative) => fs.Path.Combine([env.Home, .. relative.Split('/')]);

    /// <summary>The OS's per-user app-data directory: <c>~/Library/Application Support</c>, <c>%APPDATA%</c> or <c>~/.config</c>.</summary>
    public string AppData(string relative) =>
        env.Os switch
        {
            McpOs.MacOS => Home("Library/Application Support/" + relative),
            McpOs.Windows => fs.Path.Combine([
                env.AppData ?? Home("AppData/Roaming"),
                .. relative.Split('/'),
            ]),
            _ => Home(".config/" + relative),
        };

    /// <summary>VS Code's user directory, stable or Insiders, joined with <paramref name="relative"/>.</summary>
    public string Code(bool insiders, string relative) =>
        AppData((insiders ? "Code - Insiders/User/" : "Code/User/") + relative);

    /// <summary>The first of <paramref name="candidates"/> that exists, else the first.</summary>
    public string Existing(params string[] candidates) =>
        candidates.FirstOrDefault(fs.File.Exists) ?? candidates[0];

    /// <summary>
    /// Devin keeps <c>~/.config/devin</c> on Unix and <c>%APPDATA%\devin</c> on Windows when
    /// <c>APPDATA</c> is set.
    /// </summary>
    public string Devin() =>
        env is { Os: McpOs.Windows, AppData: { Length: > 0 } appData }
            ? fs.Path.Combine(appData, "devin", "mcp_config.json")
            : Home(".config/devin/mcp_config.json");

    public string Extension(string path) => fs.Path.GetExtension(path).ToLowerInvariant();
}
