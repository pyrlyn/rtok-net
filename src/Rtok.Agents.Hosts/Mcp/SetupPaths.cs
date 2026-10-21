using System.IO.Abstractions;

namespace Rtok.Agents.Hosts.Mcp;

/// <summary>
/// The <c>[setup.&lt;host&gt;]</c> path keys the user set, as the MCP file each one moves, keyed
/// <c>&lt;host&gt;.&lt;surface&gt;</c> (rtok <c>mcp::surfaces</c>). A key left at its default moves
/// nothing: the host table's own default stands.
/// </summary>
public static class SetupPaths
{
    /// <summary>How one key names the MCP file: the file itself, a file in the directory, or a file beside it.</summary>
    private enum Derive
    {
        File,
        Dir,
        Sibling,
    }

    /// <summary>Applied in order: a later row wins, so a host's own key beats the VS Code directory.</summary>
    private static readonly (
        string Key,
        string Host,
        string[] Surfaces,
        Derive How,
        string[] Names
    )[] Rows =
    [
        ("setup.vscode.code_user_dir", "vscode", ["stable"], Derive.Dir, ["mcp.json"]),
        ("setup.vscode.insiders_user_dir", "vscode", ["insiders"], Derive.Dir, ["mcp.json"]),
        (
            "setup.vscode.code_user_dir",
            "cline",
            ["desktop"],
            Derive.Dir,
            ["globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json"]
        ),
        (
            "setup.vscode.code_user_dir",
            "roo",
            ["desktop"],
            Derive.Dir,
            ["globalStorage/rooveterinaryinc.roo-cline/settings/mcp_settings.json"]
        ),
        ("doctor.claude_json", "claude", ["cli"], Derive.File, []),
        ("setup.amp.settings_path", "amp", ["cli"], Derive.File, []),
        ("setup.augment.settings_path", "augment", ["cli"], Derive.File, []),
        ("setup.cline.mcp_path", "cline", ["cli"], Derive.File, []),
        ("setup.codebuddy.dir", "codebuddy", ["cli"], Derive.Dir, [".mcp.json", "mcp.json"]),
        (
            "setup.codebuddy.workbuddy_dir",
            "codebuddy",
            ["desktop"],
            Derive.Dir,
            [".mcp.json", "mcp.json"]
        ),
        ("setup.codewhale.dir", "codewhale", ["cli"], Derive.Dir, ["mcp.json"]),
        ("setup.codex.config_path", "codex", ["cli"], Derive.File, []),
        ("setup.commandcode.dir", "commandcode", ["cli"], Derive.Dir, ["mcp.json"]),
        ("setup.copilot.dir", "copilot", ["cli", "desktop"], Derive.Dir, ["mcp-config.json"]),
        ("setup.crush.config_path", "crush", ["cli"], Derive.File, []),
        ("setup.cursor.hooks_path", "cursor", ["cli", "desktop"], Derive.Sibling, ["mcp.json"]),
        (
            "setup.devin.config_path",
            "devin",
            ["cli", "desktop"],
            Derive.Sibling,
            ["mcp_config.json"]
        ),
        ("setup.droid.mcp_path", "droid", ["cli"], Derive.File, []),
        ("setup.gemini.dir", "gemini", ["cli"], Derive.Dir, ["settings.json"]),
        ("setup.grok.config_path", "grok", ["cli"], Derive.File, []),
        ("setup.junie.mcp_path", "junie", ["cli"], Derive.File, []),
        ("setup.kilo.config_path", "kilo", ["cli", "desktop"], Derive.File, []),
        ("setup.kimi.config_path", "kimi", ["cli", "desktop"], Derive.Sibling, ["mcp.json"]),
        ("setup.kiro.mcp_path", "kiro", ["cli", "desktop"], Derive.File, []),
        ("setup.mimo.config_path", "mimo", ["cli", "desktop"], Derive.File, []),
        ("setup.omp.mcp_path", "omp", ["cli"], Derive.File, []),
        ("setup.opencode.config_path", "opencode", ["cli"], Derive.File, []),
        ("setup.openhands.mcp_path", "openhands", ["cli"], Derive.File, []),
        ("setup.qwen.dir", "qwen", ["cli"], Derive.Dir, ["settings.json"]),
        ("setup.roo.mcp_path", "roo", ["desktop"], Derive.File, []),
        ("setup.warp.mcp_path", "warp", ["desktop"], Derive.File, []),
        ("setup.windsurf.config_path", "windsurf", ["desktop"], Derive.File, []),
        ("setup.zcode.config_path", "zcode", ["desktop"], Derive.File, []),
        ("setup.zed.config_path", "zed", ["cli", "desktop"], Derive.File, []),
        ("setup.goose.config_path", "goose", ["cli", "desktop"], Derive.File, []),
        ("setup.continue.config_path", "continue", ["cli", "desktop"], Derive.File, []),
        ("setup.reasonix.dir", "reasonix", ["cli"], Derive.Dir, ["config.toml"]),
    ];

    /// <summary>Every config key that can move an MCP file.</summary>
    public static IReadOnlyList<string> Keys { get; } =
    [.. Rows.Select(r => r.Key).Distinct(StringComparer.Ordinal)];

    /// <summary>The MCP files the user moved.</summary>
    /// <param name="userSet">A path key's expanded value when the user set it; null when it is at its default.</param>
    /// <param name="fs">Paths are joined with its rules; a key with two candidate names takes the one that exists.</param>
    public static IReadOnlyDictionary<string, string> Of(
        Func<string, string?> userSet,
        IFileSystem fs
    )
    {
        ArgumentNullException.ThrowIfNull(userSet);
        ArgumentNullException.ThrowIfNull(fs);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, host, surfaces, how, names) in Rows)
        {
            if (userSet(key) is not { Length: > 0 } value)
            {
                continue;
            }
            var file = how switch
            {
                Derive.File => value,
                Derive.Sibling => Join(fs, fs.Path.GetDirectoryName(value) ?? "", names[0]),
                _ => names
                    .Select(n => Join(fs, value, n))
                    .Where(fs.File.Exists)
                    .DefaultIfEmpty(Join(fs, value, names[0]))
                    .First(),
            };
            foreach (var surface in surfaces)
            {
                paths[$"{host}.{surface}"] = file;
            }
        }
        return paths;
    }

    private static string Join(IFileSystem fs, string root, string relative) =>
        fs.Path.Combine([root, .. relative.Split('/')]);
}
