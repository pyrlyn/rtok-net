using System.Globalization;
using Pyrlyn.Config.Layered;
using Pyrlyn.Durations;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>
/// Checks every value against the schema: unknown keys, types, ranges and fixed choices. Each
/// issue names where the value was set, so a file error points at its line.
/// </summary>
public static class ConfigValidator
{
    /// <summary>The host ids <c>rtok agents</c> installs; <c>[agents.junk] extra</c> names one of them or <c>rtok</c>.</summary>
    public static IReadOnlyList<string> Hosts { get; } =
    [
        "claude",
        "cursor",
        "codex",
        "opencode",
        "kilo",
        "pi",
        "omp",
        "zcode",
        "kimi",
        "grok",
        "vscode",
        "copilot",
        "commandcode",
        "aider",
        "windsurf",
        "zed",
        "cline",
        "gemini",
        "codewhale",
        "mimo",
        "antigravity",
        "devin",
        "roo",
        "qwen",
        "codebuddy",
        "goose",
        "droid",
        "kiro",
        "junie",
        "amp",
        "warp",
        "continue",
        "crush",
        "openhands",
        "augment",
        "reasonix",
    ];

    private static readonly string[] JunkKinds = ["cache", "temp", "logs", "crash-dumps"];

    /// <summary>The values <c>tasks.adapter</c> takes.</summary>
    public static IReadOnlyList<string> TaskAdapters { get; } = ["disk", "github", "gitlab"];
    private static readonly string[] GraphBackends = ["tags", "lsp", "auto", "text"];
    private static readonly string[] GraphGrammars =
    [
        "rust",
        "ts",
        "tsx",
        "js",
        "mjs",
        "cjs",
        "py",
        "dart",
        "c",
        "h",
        "go",
    ];

    private static readonly Dictionary<string, string[]> Choices = new(StringComparer.Ordinal)
    {
        ["log.tspin"] = ["auto", "always", "off"],
        ["plugins.graph.map_rank"] = ["refs", "pagerank"],
        ["plugins.graph.backend"] = GraphBackends,
        ["proxy.flex.on_429"] = ["none", "backoff", "default"],
        // Any other value would turn the semantic tier on with the placeholder hash embedding.
        ["plugins.proxy.semantic_cache.embed_backend"] = ["hash"],
    };

    /// <summary>Checks merged values. Values from the defaults layer are taken as valid.</summary>
    /// <param name="entries">The leaves and their sources.</param>
    /// <returns>The issues, in entry order; empty when every value is valid.</returns>
    public static IReadOnlyList<ConfigIssue> Check(IEnumerable<ConfigEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var issues = new List<ConfigIssue>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        var legacy = ConfigSchema.LegacyKeys.Select(l => l.Old).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Source.Kind == ConfigSourceKind.Defaults || legacy.Contains(entry.Key))
            {
                continue;
            }
            var where = ConfigIssue.Locate(entry.Source);
            if (Expected(entry.Key, where, issues, reported) is { } expected)
            {
                CheckLeaf(entry.Key, entry.Value, expected, where, issues);
            }
        }
        return issues;
    }

    /// <summary>Checks a table a file names without setting anything in it.</summary>
    /// <param name="key">The dotted key of the table.</param>
    /// <param name="where">Where it was named.</param>
    /// <param name="issues">Where a problem is added.</param>
    internal static void CheckTable(string key, string where, List<ConfigIssue> issues)
    {
        if (ConfigSchema.LegacyKeys.Any(l => l.Old.StartsWith(key + ".", StringComparison.Ordinal)))
        {
            return;
        }
        if (Expected(key, where, issues, []) is { } node and not TomlTable)
        {
            issues.Add(new ConfigIssue(key, where, $"{key}: expected {KindName(node)}"));
        }
    }

    /// <summary>The schema default a leaf is checked against, or null after reporting why there is none.</summary>
    private static object? Expected(
        string key,
        string where,
        List<ConfigIssue> issues,
        HashSet<string> reported
    )
    {
        var path = KeyPath.Split(key);
        for (var i = 1; i <= path.Length; i++)
        {
            var prefix = KeyPath.Join(path[..i]);
            var node = ConfigSchema.Lookup(path[..i], out _);
            if (node is null)
            {
                if (reported.Add(prefix))
                {
                    issues.Add(new ConfigIssue(key, where, $"unknown key: {prefix}"));
                }
                return null;
            }
            if (i < path.Length && node is not TomlTable)
            {
                if (reported.Add(prefix))
                {
                    issues.Add(new ConfigIssue(key, where, $"{prefix}: expected {KindName(node)}"));
                }
                return null;
            }
            if (i == path.Length)
            {
                return node;
            }
        }
        return null;
    }

    private static void CheckLeaf(
        string key,
        object value,
        object expected,
        string where,
        List<ConfigIssue> issues
    )
    {
        if (string.Equals(key, "agents.junk.extra", StringComparison.Ordinal))
        {
            CheckJunkExtra(value, where, issues);
            return;
        }
        if (TypeError(key, value, expected) is { } typeError)
        {
            issues.Add(new ConfigIssue(key, where, typeError));
            return;
        }
        var rule = value switch
        {
            long n => IntegerRule(key, n),
            double d => RatioRule(key, d),
            string s => StringRule(key, s),
            TomlArray items => ArrayRule(key, items),
            _ => null,
        };
        if (value is long whole && RatioRule(key, whole) is { } ratio)
        {
            rule ??= ratio;
        }
        if (rule is not null)
        {
            issues.Add(new ConfigIssue(key, where, rule));
        }
    }

    private static string? TypeError(string key, object value, object expected) =>
        expected switch
        {
            TomlTable when value is not TomlTable => $"{key}: expected table",
            string when value is not string => $"{key}: expected string",
            bool when value is not bool => $"{key}: expected bool",
            long when value is not long => $"{key}: expected number",
            long when (long)value < 0 => $"{key} must be ≥ 0",
            double when value is not (long or double) => $"{key}: expected number",
            TomlArray when value is not TomlArray => $"{key}: expected array",
            TomlArray list when value is TomlArray items => ElementError(key, items, list),
            _ => null,
        };

    private static string? ElementError(string key, TomlArray items, TomlArray defaults)
    {
        var sample = defaults.Count > 0 ? defaults[0]! : "";
        for (var i = 0; i < items.Count; i++)
        {
            if (TypeError($"{key}[{i}]", items[i]!, sample) is { } error)
            {
                return error;
            }
        }
        return null;
    }

    private static string? IntegerRule(string key, long n) =>
        key switch
        {
            "proxy.port" or "web.port" when n is < 1 or > 65535 => $"{key} out of range (1–65535)",
            "plugins.archive.keep_turns" when n < 1 => $"{key} must be ≥ 1",
            // Each retry holds the client's connection open; past a handful it is a hang.
            "proxy.flex.retries" when n > 5 => $"{key} must be ≤ 5",
            "plugins.graph.live_heat_window_s"
            or "plugins.graph.live_max_events_per_s"
            or "plugins.graph.live_feed_rows"
            or "tui.tick_secs" when n < 1 => $"{key} must be ≥ 1",
            "plugins.graph.live_feed_rows" when n > 1000 => $"{key} must be ≤ 1000",
            // `read` never trims its 79-character archive marker: the id keeps a cut lossless.
            "plugins.read.max_chars" when n < 100 => $"{key} must be ≥ 100",
            // Below this every line rotates: renames on the hook path.
            "log.max_bytes" when n < 1024 => $"{key} must be ≥ 1024",
            "log.files" when n > 20 => $"{key} must be ≤ 20",
            "agents.junk.keep_logs_days"
            or "agents.junk.stale_session_days"
            or "agents.junk.stale_worktree_days"
            or "agents.junk.crash_dump_min_age_days" when n > 3650 => $"{key} must be ≤ 3650",
            "agents.junk.temp_min_age_hours" when n > 87_600 => $"{key} must be ≤ 87600",
            _ => null,
        };

    /// <summary>
    /// <c>threshold ≤ 0</c> makes every cached entry a semantic hit; a delta ratio outside
    /// <c>(0, 1]</c> disables deltas or sends diffs larger than the file.
    /// </summary>
    private static string? RatioRule(string key, double x) =>
        key is "plugins.proxy.semantic_cache.threshold" or "plugins.read.delta_max_ratio"
        && !(x > 0 && x <= 1)
            ? $"{key} must be in (0, 1]"
            : null;

    private static string? StringRule(string key, string s)
    {
        if (
            Choices.TryGetValue(key, out var choices)
            && !choices.Contains(s, StringComparer.Ordinal)
        )
        {
            return $"{key}: expected one of {string.Join(" | ", choices)}";
        }
        return key switch
        {
            "proxy.mode" when s is not ("passthrough" or "compress") =>
                $"{key} must be passthrough or compress",
            "plugins.read.default_mode"
                when s is not ("full" or "lines" or "map" or "signatures") =>
                $"{key} must be full, lines, map, or signatures",
            "plugins.graph.watch" when s is not ("off" or "notify") =>
                $"{key} must be off or notify",
            "tasks.adapter" when !TaskAdapters.Contains(s, StringComparer.Ordinal) =>
                $"{key} must be one of {string.Join(", ", TaskAdapters)}",
            "tasks.prefix" when s.Length > 0 && (s.Length > 8 || !s.All(char.IsAsciiLetter)) =>
                $"{key}: task prefix \"{s}\" must be 1 to 8 ASCII letters",
            "stats.since" or "report.since" => SinceRule(key, s),
            "log.level" when s.ToLowerInvariant() is not ("error" or "warn" or "info" or "debug") =>
                $"{key} must be error, warn, info, or debug",
            "plugins.compress.llm.url" when !IsHttpUrlOrEmpty(s) =>
                $"{key} must be empty or an http(s) URL",
            _ when key.StartsWith("proxy.lanes.", StringComparison.Ordinal)
                    && key.EndsWith(".upstream", StringComparison.Ordinal)
                    && !IsHttpUrlOrEmpty(s) => $"{key} must be empty or an http(s) URL",
            _ when key.StartsWith("plugins.graph.extensions.", StringComparison.Ordinal)
                    && !GraphGrammars.Contains(s, StringComparer.Ordinal) =>
                $"{key} must be one of {string.Join(", ", GraphGrammars)} (got {s})",
            _ when key.StartsWith("plugins.graph.backend_by_language.", StringComparison.Ordinal)
                    && !GraphBackends.Contains(s, StringComparer.Ordinal) =>
                $"{key} must be one of {string.Join(", ", GraphBackends)} (got {s})",
            _ => null,
        };
    }

    private static string? SinceRule(string key, string s)
    {
        try
        {
            HumanDuration.ParseDaysOrHours(s);
            return null;
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            return $"{key}: {e.Message}";
        }
    }

    /// <summary>A glob that does not compile would protect nothing it was meant to.</summary>
    private static string? ArrayRule(string key, TomlArray items)
    {
        if (!string.Equals(key, "agents.junk.exclude", StringComparison.Ordinal))
        {
            return null;
        }
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is string glob && GlobError(glob) is { } error)
            {
                return $"{key}[{i}]: {error}";
            }
        }
        return null;
    }

    private static string? GlobError(string glob)
    {
        int brackets = 0,
            braces = 0;
        for (var i = 0; i < glob.Length; i++)
        {
            switch (glob[i])
            {
                case '\\':
                    i++;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']' when brackets > 0:
                    brackets--;
                    break;
                case '{':
                    braces++;
                    break;
                case '}' when braces > 0:
                    braces--;
                    break;
            }
        }
        return brackets > 0 ? "unclosed character class; missing ']'"
            : braces > 0 ? "unclosed alternate group; missing '}'"
            : null;
    }

    private static bool IsHttpUrlOrEmpty(string s) =>
        s.Trim().Length == 0
        || (
            Uri.TryCreate(s.Trim(), UriKind.Absolute, out var uri)
            && (
                string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            )
        );

    /// <summary>Every entry a table of a known host (or <c>rtok</c>), a junk kind and a path.</summary>
    private static void CheckJunkExtra(object value, string where, List<ConfigIssue> issues)
    {
        const string key = "agents.junk.extra";
        IEnumerable<object?> items = value switch
        {
            TomlTableArray tables => tables,
            TomlArray array => array,
            _ => [],
        };
        if (value is not (TomlTableArray or TomlArray))
        {
            issues.Add(new ConfigIssue(key, where, $"{key}: expected array"));
            return;
        }
        var i = 0;
        foreach (var item in items)
        {
            var at = $"{key}[{i++}]";
            if (item is not TomlTable t)
            {
                issues.Add(new ConfigIssue(key, where, $"{at}: expected table"));
                continue;
            }
            foreach (var k in t.Keys.Where(k => k is not ("host" or "kind" or "path")))
            {
                issues.Add(new ConfigIssue(key, where, $"unknown key: {at}.{k}"));
            }
            var host = t.TryGetValue("host", out var h) ? h as string : null;
            if (
                host is null
                || (host is not "rtok" && !Hosts.Contains(host, StringComparer.Ordinal))
            )
            {
                issues.Add(
                    new ConfigIssue(
                        key,
                        where,
                        $"{at}.host must be rtok or a host id (see `rtok agents list`)"
                    )
                );
            }
            if (
                !t.TryGetValue("kind", out var kind)
                || kind is not string k2
                || !JunkKinds.Contains(k2, StringComparer.Ordinal)
            )
            {
                issues.Add(
                    new ConfigIssue(
                        key,
                        where,
                        $"{at}.kind must be one of {string.Join(", ", JunkKinds)}"
                    )
                );
            }
            if (!t.TryGetValue("path", out var path) || path is not string { Length: > 0 })
            {
                issues.Add(new ConfigIssue(key, where, $"{at}.path must be a non-empty path"));
            }
        }
    }

    private static string KindName(object node) =>
        node switch
        {
            string => "string",
            bool => "bool",
            long or double => "number",
            TomlArray => "array",
            _ => "table",
        };
}
