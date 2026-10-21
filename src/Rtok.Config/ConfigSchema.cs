using System.Collections.Frozen;
using Pyrlyn.Config.Layered;
using Tomlyn;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>What the typed schema says about keys: defaults, paths, secrets, legacy names, free-form maps.</summary>
public static class ConfigSchema
{
    private static readonly Lazy<TomlTable> LazyDefaults = new(() => ToTable(new RtokConfig()));
    private static readonly Lazy<string[]> LazyLeafKeys = new(() =>
        [.. Leaves(Defaults, "").Select(l => l.Key).Order(StringComparer.Ordinal)]
    );

    /// <summary>The defaults of every key as a tree. Shared: never change it.</summary>
    public static TomlTable Defaults => LazyDefaults.Value;

    /// <summary>Every dotted key with a default, sorted.</summary>
    public static IReadOnlyList<string> LeafKeys => LazyLeafKeys.Value;

    /// <summary>Keys holding a credential: shown as set, never with their value.</summary>
    public static FrozenSet<string> SecretKeys { get; } =
        FrozenSet.Create(StringComparer.Ordinal, "otel.headers", "mcp.token");

    /// <summary>
    /// Keys holding a path, where <c>~</c> expands. <c>[]</c> marks a list of paths,
    /// <c>.*</c> a map whose values are paths.
    /// </summary>
    public static IReadOnlyList<string> PathKeys { get; } =
    [
        "core.db_path",
        "core.archive_dir",
        "log.path",
        "demon.state_dir",
        "stats.transcripts_dir",
        "stats.codex_dir",
        "report.out",
        "bench.tasks",
        "bench.configs.*",
        "doctor.settings_path",
        "doctor.claude_json",
        "doctor.mcp_json",
        "setup.claude.settings_path",
        "setup.cursor.hooks_path",
        "setup.codex.config_path",
        "setup.opencode.config_path",
        "setup.kilo.config_path",
        "setup.pi.extensions_path",
        "setup.omp.extensions_path",
        "setup.omp.mcp_path",
        "setup.zcode.config_path",
        "setup.kimi.config_path",
        "setup.grok.config_path",
        "setup.vscode.code_user_dir",
        "setup.vscode.insiders_user_dir",
        "setup.copilot.dir",
        "setup.commandcode.dir",
        "setup.aider.config_path",
        "setup.windsurf.config_path",
        "setup.zed.config_path",
        "setup.cline.hooks_path",
        "setup.cline.mcp_path",
        "setup.gemini.dir",
        "setup.codewhale.dir",
        "setup.mimo.config_path",
        "setup.antigravity.plugins_path",
        "setup.antigravity.cli_plugins_path",
        "setup.devin.config_path",
        "setup.roo.mcp_path",
        "setup.junie.mcp_path",
        "setup.qwen.dir",
        "setup.codebuddy.dir",
        "setup.codebuddy.workbuddy_dir",
        "setup.goose.config_path",
        "setup.droid.mcp_path",
        "setup.kiro.mcp_path",
        "setup.amp.settings_path",
        "setup.warp.mcp_path",
        "setup.continue.config_path",
        "setup.crush.config_path",
        "setup.openhands.mcp_path",
        "setup.augment.settings_path",
        "setup.reasonix.dir",
        "worktree.root",
        "plugins.cmd.rules",
        "plugins.cmd.rules_dir",
        "plugins.read.allow_paths[]",
        "plugins.inject.modes_dir",
        "plugins.wasm.dir",
        "tasks.disk.dir",
        "agents.usage.dirs.opencode[]",
        "agents.usage.dirs.kilo[]",
        "agents.usage.dirs.copilot[]",
        "agents.usage.dirs.gemini[]",
        "agents.usage.dirs.droid[]",
        "agents.usage.dirs.pi[]",
        "agents.usage.dirs.kimi[]",
        "agents.usage.dirs.grok[]",
        "agents.usage.dirs.zcode[]",
        "agents.usage.dirs.antigravity[]",
    ];

    /// <summary>
    /// Old keys a file may still hold, and the key each became. Read with a warning while the
    /// new key is at its default, then dropped.
    /// </summary>
    public static IReadOnlyList<(string Old, string New)> LegacyKeys { get; } =
    [
        ("core.log_file", "log.path"),
        ("core.log_level", "log.level"),
        ("core.log_to_db", "log.to_db"),
        ("core.inject_budget_tokens", "plugins.inject.budget_tokens"),
        ("dashboard.host", "web.host"),
        ("dashboard.port", "web.port"),
    ];

    /// <summary>Old environment variable names and the key each sets.</summary>
    public static IReadOnlyList<(string Name, string Key)> EnvAliases { get; } =
    [("RTOK_UPSTREAM", "proxy.upstream"), ("RTOK_OPENAI_UPSTREAM", "proxy.openai_upstream")];

    /// <summary>Keys without a default that a file may set.</summary>
    internal static FrozenDictionary<string, object> OptionalKeys { get; } =
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["setup.source"] = "",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Maps keyed by free names (a model, a suffix), with the default of each value.</summary>
    internal static FrozenDictionary<string, object> OpenMaps { get; } =
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["stats.prices"] = TomlText.Parse(
                TomlSerializer.Serialize(new ModelPrice(), RtokConfigContext.Default.ModelPrice)
            ),
            ["bench.configs"] = "",
            ["plugins.graph.extensions"] = "",
            ["plugins.graph.backend_by_language"] = "",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The default of a dotted key, a table for a section, or null when the schema has no such key.</summary>
    /// <param name="key">The dotted key.</param>
    /// <returns>The default.</returns>
    public static object? DefaultOf(string key) => Lookup(KeyPath.Split(key), out _);

    /// <summary>Serializes a config to a tree with the source-generated metadata.</summary>
    internal static TomlTable ToTable(RtokConfig config) =>
        TomlText.Parse(TomlSerializer.Serialize(config, RtokConfigContext.Default.RtokConfig));

    /// <summary>
    /// The schema node for <paramref name="path"/>: a default leaf, a table, or null. Below an
    /// open map, every name takes the map's value default; <paramref name="open"/> says so.
    /// </summary>
    internal static object? Lookup(string[] path, out bool open)
    {
        open = false;
        object node = Defaults;
        for (var i = 0; i < path.Length; i++)
        {
            var prefix = string.Join('.', path[..i]);
            if (i > 0 && OpenMaps.TryGetValue(prefix, out var value))
            {
                open = true;
                node = value;
                continue;
            }
            if (node is not TomlTable table || !table.TryGetValue(path[i], out node!))
            {
                return
                    i == path.Length - 1
                    && OptionalKeys.TryGetValue(string.Join('.', path), out var optional)
                    ? optional
                    : null;
            }
        }
        return node;
    }

    internal static IEnumerable<(string Key, object Value)> Leaves(TomlTable table, string prefix)
    {
        foreach (var (key, value) in table)
        {
            var dotted =
                prefix.Length == 0 ? KeyPath.Segment(key) : $"{prefix}.{KeyPath.Segment(key)}";
            if (value is TomlTable child)
            {
                foreach (var leaf in Leaves(child, dotted))
                {
                    yield return leaf;
                }
            }
            else
            {
                yield return (dotted, value);
            }
        }
    }
}
