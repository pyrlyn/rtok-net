namespace Rtok.Agents.Junk;

/// <summary>
/// The host junk map of research.md §22, encoded once: per host the temp, log and cache folders
/// §22 documents, its data home, and an Electron app's platform folders read-only. A folder that
/// does not exist is absent from the report.
/// </summary>
public static class JunkMap
{
    /// <summary>
    /// Hosts with no row on purpose: §22 reads "not documented" for every path they write (the
    /// config folders their own files sit in are still listed).
    /// </summary>
    public static IReadOnlyList<string> NoRows { get; } =
    [
        "kilo",
        "aider",
        "roo",
        "droid",
        "goose",
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

    /// <summary>The Electron cache subfolders of an app's data folder (research.md §22.2); the rest of it is app data.</summary>
    private static readonly string[] ElectronCaches =
    [
        "Cache",
        "Code Cache",
        "GPUCache",
        "CachedData",
        "DawnCache",
    ];

    /// <summary>
    /// Settings and credentials no removal may hold or touch, beside the MCP files each host's
    /// entry names: the protected list of the re-check.
    /// </summary>
    public static IReadOnlyList<string> Protected { get; } =
    [
        "{claude}/settings.json",
        "{claude}/settings.local.json",
        "{claude}/.credentials.json",
        "{home}/.claude.json",
        "{codex}/config.toml",
        "{codex}/auth.json",
        "{home}/.gemini/settings.json",
        "{home}/.gemini/oauth_creds.json",
        "{home}/.copilot/config.json",
    ];

    /// <summary>The §22 rows and desktop-app folders of <paramref name="host"/>.</summary>
    /// <param name="host">A host id.</param>
    public static IReadOnlyList<JunkSpec> Specs(string host) =>
        Rows.TryGetValue(host, out var rows) ? rows : [];

    private static readonly Dictionary<string, JunkSpec[]> Rows = new(StringComparer.Ordinal)
    {
        ["claude"] =
        [
            Doc(JunkRole.Data, "{claude}"),
            Doc(JunkRole.Temp, "{claude}/shell-snapshots"),
            Doc(JunkRole.Logs, "{claude}/debug"),
            Doc(JunkRole.Cache, "{claude}/paste-cache"),
        ],
        ["codex"] = [Doc(JunkRole.Data, "{codex}"), Doc(JunkRole.Logs, "{codex}/log")],
        ["opencode"] =
        [
            Doc(JunkRole.Data, "{xdg_data}/opencode"),
            Doc(JunkRole.Logs, "{xdg_data}/opencode/log"),
            Doc(JunkRole.Cache, "{xdg_cache}/opencode"),
        ],
        ["pi"] =
        [
            Doc(JunkRole.Data, "{home}/.pi/agent"),
            Doc(JunkRole.Logs, "{home}/.pi/agent/pi-debug.log"),
        ],
        ["omp"] =
        [
            Doc(JunkRole.Data, "{home}/.omp"),
            Doc(JunkRole.Cache, "{home}/.omp/cache/github-cache.db"),
        ],
        ["zcode"] =
        [
            Doc(JunkRole.Data, "{home}/.zcode"),
            Doc(JunkRole.Temp, "{home}/.zcode/cli/exec"),
        ],
        ["kimi"] =
        [
            Doc(JunkRole.Data, "{home}/.kimi-code"),
            Doc(JunkRole.Logs, "{home}/.kimi-code/logs"),
            Doc(JunkRole.Cache, "{home}/.kimi-code/bin"),
            Doc(JunkRole.Cache, "{home}/.kimi-code/updates/latest.json"),
        ],
        ["grok"] = [Doc(JunkRole.Data, "{home}/.grok"), Doc(JunkRole.Logs, "{home}/.grok/logs")],
        ["copilot"] =
        [
            Doc(JunkRole.Data, "{home}/.copilot"),
            Doc(JunkRole.Logs, "{home}/.copilot/logs"),
            Doc(JunkRole.Cache, "{copilot_cache}"),
        ],
        ["zed"] =
        [
            Doc(JunkRole.Data, "{home}/Library/Application Support/Zed"),
            Doc(JunkRole.Logs, "{home}/Library/Logs/Zed/Zed.log"),
            Doc(JunkRole.Logs, "{xdg_data}/zed/logs/Zed.log"),
            Doc(JunkRole.Cache, "{home}/Library/Caches/Zed"),
            Doc(JunkRole.Cache, "{xdg_cache}/zed"),
        ],
        ["gemini"] = [Doc(JunkRole.Data, "{home}/.gemini")],
        ["codewhale"] =
        [
            Doc(JunkRole.Data, "{home}/.codewhale"),
            Doc(JunkRole.Cache, "{home}/.codewhale/update-check.json"),
        ],
        ["commandcode"] = [Doc(JunkRole.Data, "{home}/.commandcode")],
        ["cline"] =
        [
            Doc(JunkRole.Data, "{home}/.cline"),
            Doc(JunkRole.Logs, "{home}/.cline/data/logs"),
        ],
        ["mimo"] =
        [
            Doc(JunkRole.Data, "{home}/.config/mimocode"),
            Doc(JunkRole.Data, "{home}/.local/share/mimocode"),
            Doc(JunkRole.Logs, "{home}/.local/share/mimocode/log"),
            Doc(JunkRole.Cache, "{home}/.cache/mimocode"),
        ],
        ["devin"] =
        [
            Doc(JunkRole.Data, "{home}/.config/devin"),
            Doc(JunkRole.Logs, "{home}/.local/share/devin/cli/logs"),
        ],
        ["qwen"] = [Doc(JunkRole.Data, "{home}/.qwen")],
        ["codebuddy"] =
        [
            Doc(JunkRole.Data, "{home}/.codebuddy"),
            Doc(JunkRole.Data, "{home}/.workbuddy"),
        ],
        // Before `Electron`, which lists the same folders read-only: the first spec wins.
        ["vscode"] =
        [
            Doc(JunkRole.Data, "{xdg_config}/Code"),
            new(JunkRole.Cache, "{home}/Library/Application Support/Code/CachedData", true, true),
            new(JunkRole.Cache, "{xdg_config}/Code/CachedData", true, true),
            .. Electron("Code"),
        ],
        ["cursor"] = Electron("Cursor"),
        ["windsurf"] = Electron("Windsurf"),
        ["antigravity"] = Electron("Antigravity"),
    };

    private static JunkSpec Doc(JunkRole role, string path) => new(role, path, true);

    /// <summary>Where the platform keeps an Electron app's data and caches; §22 has no row, so every folder is read-only.</summary>
    private static JunkSpec[] Electron(string app)
    {
        string[] data = [$"{{home}}/Library/Application Support/{app}", $"{{xdg_config}}/{app}"];
        return
        [
            .. data.Select(d => new JunkSpec(JunkRole.Data, d, false)),
            new(JunkRole.Cache, $"{{home}}/Library/Caches/{app}", false),
            new(JunkRole.Cache, $"{{xdg_cache}}/{app}", false),
            .. data.SelectMany(d =>
                ElectronCaches.Select(sub => new JunkSpec(JunkRole.Cache, $"{d}/{sub}", false))
            ),
        ];
    }
}
