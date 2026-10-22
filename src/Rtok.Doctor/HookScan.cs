using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.Config.Layered;
using Rtok.Mcp.Install;

namespace Rtok.Doctor;

/// <summary>
/// Every hook doctor can see: Claude Code's settings (managed, user, local, project), the other
/// hosts' documented hook files, and the hooks of enabled Claude plugins. Files that cannot be
/// read are findings; a missing file is silent.
/// </summary>
internal sealed class HookScan
{
    /// <summary>Other hosts' hook files under the home, by documented shape.</summary>
    private static readonly (string Agent, string File, HookShape Shape)[] OtherHosts =
    [
        ("cursor", ".cursor/hooks.json", HookShape.Json),
        ("codex", ".codex/hooks.json", HookShape.Json),
        ("codex", ".codex/config.toml", HookShape.Codex),
        ("gemini", ".gemini/settings.json", HookShape.Json),
        ("kimi", ".kimi-code/config.toml", HookShape.Kimi),
        ("codewhale", ".codewhale/config.toml", HookShape.CodeWhale),
    ];

    private readonly DoctorHome _home;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _enabled = new(StringComparer.Ordinal);

    public HookScan(DoctorHome home)
    {
        _home = home;
        var settings = home.PathOf(home.Config.Doctor.SettingsPath);
        foreach (var managed in Managed())
        {
            Load(new HookSource("claude", managed, Scope.Managed, HookShape.Json));
        }
        Load(new HookSource("claude", settings, Scope.User, HookShape.Json));
        Load(new HookSource("claude", Sibling(settings), Scope.Local, HookShape.Json));
        var project = home.Under(home.Cwd, ".claude");
        Load(
            new HookSource(
                "claude",
                home.Under(project, "settings.json"),
                Scope.Project,
                HookShape.Json
            )
        );
        Load(
            new HookSource(
                "claude",
                home.Under(project, "settings.local.json"),
                Scope.Local,
                HookShape.Json
            )
        );
        foreach (var (agent, file, shape) in OtherHosts)
        {
            Load(new HookSource(agent, home.InHome(file), Scope.User, shape));
        }
        LoadPlugins();
    }

    /// <summary>The command hooks, in load order: managed, user, local, project, other hosts, plugins.</summary>
    public List<HookEntry> Entries { get; } = [];

    /// <summary>Unreadable files and stale plugins.</summary>
    public List<Problem> Problems { get; } = [];

    /// <summary>Enabled plugins whose directory is there: id and directory, the first per id.</summary>
    public List<(string Id, string Dir)> Plugins { get; } = [];

    /// <summary>The installed plugins index beside the user settings.</summary>
    public string PluginIndex =>
        _home.Under(
            _home.Fs.Path.GetDirectoryName(_home.PathOf(_home.Config.Setup.Claude.SettingsPath))
                ?? _home.Home,
            "plugins/installed_plugins.json"
        );

    /// <summary>The managed settings files: <see cref="DoctorOptions.Managed"/>, else the OS's place.</summary>
    private IReadOnlyList<string> Managed() =>
        _home.Options.Managed
        ?? (
            _home.Env.Os switch
            {
                McpOs.MacOS => ["/Library/Application Support/ClaudeCode/managed-settings.json"],
                McpOs.Windows => [@"C:\ProgramData\ClaudeCode\managed-settings.json"],
                _ => ["/etc/claude-code/managed-settings.json"],
            }
        );

    private string Sibling(string settings) =>
        _home.Fs.Path.Combine(
            _home.Fs.Path.GetDirectoryName(settings) ?? "",
            "settings.local.json"
        );

    private void Load(HookSource source)
    {
        if (!_seen.Add(source.Path) || !_seen.Add("canon:" + _home.Canonical(source.Path)))
        {
            return;
        }
        if (Parse(source) is not { } root)
        {
            return;
        }
        if (
            string.Equals(source.Agent, "claude", StringComparison.Ordinal)
            && root["enabledPlugins"] is JsonObject plugins
        )
        {
            foreach (var (id, on) in plugins)
            {
                if (on is JsonValue v && v.GetValueKind() == JsonValueKind.True)
                {
                    _enabled.Add(id);
                }
            }
        }
        Entries.AddRange(Read(source, root));
    }

    /// <summary>The file's tree; null when it is missing or reported unreadable.</summary>
    private JsonNode? Parse(HookSource source)
    {
        try
        {
            if (_home.Read(source.Path) is not { } text)
            {
                return null;
            }
            return source.Toml
                ? DoctorHome.ParseToml(text, source.Path)
                : DoctorHome.ParseJsonc(text);
        }
        catch (Exception e)
            when (e is IOException or InvalidDataException or JsonException or ConfigFileException)
        {
            Problems.Add(
                new Problem(
                    ProblemKinds.UnreadableConfig,
                    source.Agent,
                    source.Path,
                    "",
                    $"cannot read {source.Path}: {e.Message}",
                    Fixable: false
                )
            );
            return null;
        }
    }

    internal static IEnumerable<HookEntry> Read(HookSource source, JsonNode root) =>
        source.Shape switch
        {
            HookShape.Kimi => Tables(source, root["hooks"] as JsonArray, "hooks"),
            HookShape.CodeWhale => Tables(
                source,
                root["hooks"]?["hooks"] as JsonArray,
                "hooks.hooks"
            ),
            _ => Groups(source, root["hooks"] as JsonObject),
        };

    /// <summary><c>hooks.&lt;Event&gt;[g].hooks[h]</c>, or the flat <c>hooks.&lt;Event&gt;[g]</c>.</summary>
    private static IEnumerable<HookEntry> Groups(HookSource source, JsonObject? hooks)
    {
        if (hooks is null)
        {
            yield break;
        }
        foreach (var (ev, node) in hooks)
        {
            if (node is not JsonArray groups)
            {
                continue;
            }
            for (var g = 0; g < groups.Count; g++)
            {
                var group = groups[g] as JsonObject;
                var matcher = Matcher(group?["matcher"]);
                var at = string.Create(CultureInfo.InvariantCulture, $"hooks.{ev}[{g}]");
                if (group?["hooks"] is JsonArray inner)
                {
                    for (var h = 0; h < inner.Count; h++)
                    {
                        if (Command(inner[h]) is { } cmd)
                        {
                            yield return new HookEntry(
                                source,
                                string.Create(CultureInfo.InvariantCulture, $"{at}.hooks[{h}]"),
                                ev,
                                matcher,
                                cmd
                            );
                        }
                    }
                }
                else if (Command(group) is { } flat)
                {
                    yield return new HookEntry(source, at, ev, matcher, flat);
                }
            }
        }
    }

    /// <summary>TOML tables of <c>{event, matcher, command}</c>, keyed by array index.</summary>
    private static IEnumerable<HookEntry> Tables(HookSource source, JsonArray? tables, string key)
    {
        for (var i = 0; tables is not null && i < tables.Count; i++)
        {
            if (
                tables[i] is JsonObject t
                && DoctorHome.Str(t["event"]) is { } ev
                && DoctorHome.Str(t["command"]) is { } cmd
            )
            {
                yield return new HookEntry(
                    source,
                    string.Create(CultureInfo.InvariantCulture, $"{key}[{i}]"),
                    ev,
                    Matcher(t["matcher"]),
                    cmd
                );
            }
        }
    }

    private static string? Matcher(JsonNode? node) =>
        DoctorHome.Str(node) is { Length: > 0 } m ? m : null;

    /// <summary>A hook's command when it is a command hook: <c>type</c> absent or <c>command</c>.</summary>
    private static string? Command(JsonNode? node) =>
        node is JsonObject o
        && (
            o["type"] is null
            || string.Equals(DoctorHome.Str(o["type"]), "command", StringComparison.Ordinal)
        )
            ? DoctorHome.Str(o["command"])
            : null;

    /// <summary>
    /// The enabled plugins in the installed index: a gone directory is stale, a present one is
    /// recorded and its <c>hooks/hooks.json</c> read with its root.
    /// </summary>
    private void LoadPlugins()
    {
        var index = PluginIndex;
        if (_home.TryJson(index)?["plugins"] is not JsonObject plugins)
        {
            return;
        }
        foreach (var (id, installs) in plugins)
        {
            if (!_enabled.Contains(id) || installs is not JsonArray list)
            {
                continue;
            }
            var first = true;
            for (var i = 0; i < list.Count; i++)
            {
                if (DoctorHome.Str(list[i]?["installPath"]) is not { } dir)
                {
                    continue;
                }
                if (!_home.Fs.Directory.Exists(dir))
                {
                    Problems.Add(
                        new Problem(
                            ProblemKinds.StalePlugin,
                            "claude",
                            index,
                            string.Create(CultureInfo.InvariantCulture, $"plugins.{id}[{i}]"),
                            $"`{id}` is enabled but its install directory {dir} is gone",
                            Fixable: false
                        )
                    );
                    continue;
                }
                if (first)
                {
                    first = false;
                    Plugins.Add((id, dir));
                    Load(
                        new HookSource(
                            "claude",
                            _home.Under(dir, "hooks/hooks.json"),
                            Scope.Plugin,
                            HookShape.Json,
                            dir
                        )
                    );
                }
            }
        }
    }
}
