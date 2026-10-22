namespace Rtok.Agents.Junk;

/// <summary>
/// The roots the §22 templates start from, read from the environment once so a test builds its
/// own. An empty or relative value is ignored, as the XDG spec says for its own variables.
/// </summary>
public sealed class JunkRoots
{
    private readonly Dictionary<string, string> _tokens;

    /// <summary>Roots for <paramref name="home"/>, with <paramref name="variable"/> answering the environment.</summary>
    /// <param name="home">The user's home.</param>
    /// <param name="variable">An environment variable, or null when unset.</param>
    /// <param name="os">Whose cache layout: <c>macos</c>, <c>windows</c> or <c>linux</c>.</param>
    public JunkRoots(string home, Func<string, string?> variable, string os)
    {
        ArgumentException.ThrowIfNullOrEmpty(home);
        ArgumentNullException.ThrowIfNull(variable);
        string Dir(string name, string fallback) =>
            variable(name) is { Length: > 0 } v && Path.IsPathRooted(v) ? v : fallback;
        Home = home;
        var xdgCache = Dir("XDG_CACHE_HOME", Path.Combine(home, ".cache"));
        var mac = os is "macos";
        _tokens = new(StringComparer.Ordinal)
        {
            ["home"] = home,
            ["claude"] = Dir("CLAUDE_CONFIG_DIR", Path.Combine(home, ".claude")),
            ["codex"] = Dir("CODEX_HOME", Path.Combine(home, ".codex")),
            ["xdg_cache"] = xdgCache,
            ["xdg_config"] = Dir("XDG_CONFIG_HOME", Path.Combine(home, ".config")),
            ["xdg_data"] = Dir("XDG_DATA_HOME", Path.Combine(home, ".local", "share")),
            ["copilot_cache"] = Dir(
                "COPILOT_CACHE_HOME",
                mac
                    ? Path.Combine(home, "Library", "Caches", "copilot")
                    : Path.Combine(xdgCache, "copilot")
            ),
            ["rtok_cache"] =
                mac ? Path.Combine(home, "Library", "Caches", "rtok")
                : os is "windows"
                    ? Path.Combine(
                        Dir("LOCALAPPDATA", Path.Combine(home, "AppData", "Local")),
                        "rtok",
                        "cache"
                    )
                : Path.Combine(xdgCache, "rtok"),
        };
    }

    /// <summary>The user's home.</summary>
    public string Home { get; }

    /// <summary>The roots of this process.</summary>
    public static JunkRoots FromEnvironment(string home) =>
        new(home, Environment.GetEnvironmentVariable, CurrentOs());

    /// <summary>The OS word <see cref="JunkRoots(string, Func{string, string?}, string)"/> takes.</summary>
    public static string CurrentOs() =>
        OperatingSystem.IsMacOS() ? "macos"
        : OperatingSystem.IsWindows() ? "windows"
        : "linux";

    /// <summary><c>{token}</c> or <c>{token}/rest</c> as a path; a template without a known token as written.</summary>
    /// <param name="template">A §22 template.</param>
    public string Resolve(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var close = template.IndexOf('}', StringComparison.Ordinal);
        if (
            !template.StartsWith('{')
            || close < 0
            || !_tokens.TryGetValue(template[1..close], out var root)
        )
        {
            return template;
        }
        var rest = template[(close + 1)..];
        return rest.StartsWith('/')
            ? Path.Combine(root, rest[1..].Replace('/', Path.DirectorySeparatorChar))
            : root;
    }
}
