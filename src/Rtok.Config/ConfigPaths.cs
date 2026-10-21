using System.Collections.Frozen;
using Pyrlyn.Config.Layered;
using Rtok.Hook.Contracts;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>
/// Expands <c>~</c> in path keys, and points the agents' session folders at the place their
/// own environment variables moved them to while the key is still at its default.
/// </summary>
internal static class ConfigPaths
{
    private static readonly FrozenSet<string> Plain = ConfigSchema
        .PathKeys.Where(k =>
            !k.EndsWith("[]", StringComparison.Ordinal)
            && !k.EndsWith(".*", StringComparison.Ordinal)
        )
        .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> Lists = ConfigSchema
        .PathKeys.Where(k => k.EndsWith("[]", StringComparison.Ordinal))
        .Select(k => k[..^2])
        .ToFrozenSet(StringComparer.Ordinal);

    private static readonly string[] Maps =
    [
        .. ConfigSchema
            .PathKeys.Where(k => k.EndsWith(".*", StringComparison.Ordinal))
            .Select(k => k[..^1]),
    ];

    /// <summary>The expanded value of an entry, or the entry's own value when nothing changes.</summary>
    public static object Expand(ConfigEntry entry, ConfigLocations at, Func<string, string?> env)
    {
        if (entry.Source.Kind == ConfigSourceKind.Defaults && Followed(entry.Key, env) is { } moved)
        {
            return List(moved);
        }
        if (entry.Value is string path && IsPathKey(entry.Key))
        {
            var expanded = RtokHome.Expand(path, at.Home, at.UserHome);
            return string.Equals(expanded, path, StringComparison.Ordinal) ? path : expanded;
        }
        if (entry.Value is TomlArray items && Lists.Contains(entry.Key))
        {
            var expanded = items
                .Select(i => i is string s ? RtokHome.Expand(s, at.Home, at.UserHome) : i!)
                .ToList();
            return expanded.SequenceEqual(items!) ? items : List(expanded);
        }
        return entry.Value;
    }

    private static bool IsPathKey(string key) =>
        Plain.Contains(key)
        || Maps.Any(m =>
            key.StartsWith(m, StringComparison.Ordinal)
            && KeyPath.Split(key[m.Length..]).Length == 1
        );

    /// <summary>The folder an agent's own variable moved its sessions to; only an absolute value counts.</summary>
    private static string? Followed(string key, Func<string, string?> env)
    {
        string? Abs(string name) =>
            env(name) is { Length: > 0 } v && Path.IsPathRooted(v) ? v : null;
        return key switch
        {
            "agents.usage.dirs.opencode" => Under(Abs("XDG_DATA_HOME"), "opencode"),
            "agents.usage.dirs.kilo" => Under(Abs("XDG_DATA_HOME"), "kilo"),
            "agents.usage.dirs.copilot" => Under(Abs("COPILOT_HOME"), "session-state"),
            "agents.usage.dirs.gemini" => Under(Abs("GEMINI_CLI_HOME"), ".gemini", "tmp"),
            "agents.usage.dirs.pi" => Abs("PI_CODING_AGENT_SESSION_DIR")
                ?? Under(Abs("PI_CODING_AGENT_DIR"), "sessions"),
            "agents.usage.dirs.kimi" => Under(Abs("KIMI_CODE_HOME"), "sessions"),
            "agents.usage.dirs.grok" => Under(Abs("GROK_HOME"), "sessions"),
            _ => null,
        };
    }

    private static string? Under(string? root, params string[] parts) =>
        root is null ? null : Path.Combine([root, .. parts]);

    private static TomlArray List(params IEnumerable<object> items)
    {
        var array = new TomlArray();
        foreach (var item in items)
        {
            array.Add(item);
        }
        return array;
    }
}
