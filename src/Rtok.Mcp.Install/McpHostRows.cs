namespace Rtok.Mcp.Install;

/// <summary>The rows of <see cref="McpHosts"/>, as rtok wrote each host's entry (rtok @ ef091589).</summary>
internal static class McpHostRows
{
    private const string Servers = "mcpServers";

    public static IReadOnlyList<McpHostRow> All { get; } =
    [
        new(
            "amp",
            "cli",
            McpClient.Cli,
            p =>
                p.Existing(
                    p.Home(".config/amp/settings.json"),
                    p.Home(".config/amp/settings.jsonc")
                ),
            "amp.mcpServers",
            EntryShape.Command
        )
        {
            Jsonc = true,
        },
        new(
            "augment",
            "cli",
            McpClient.Cli,
            p => p.Home(".augment/settings.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "claude",
            "cli",
            McpClient.Cli,
            p => p.Home(".claude.json"),
            Servers,
            EntryShape.TypedCommand
        ),
        new(
            "claude",
            "desktop",
            McpClient.Desktop,
            p => p.AppData("Claude/claude_desktop_config.json"),
            Servers,
            EntryShape.TypedCommand,
            Absolute: true
        ),
        new(
            "cline",
            "cli",
            McpClient.Cli,
            p => p.Home(".cline/data/settings/cline_mcp_settings.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "cline",
            "desktop",
            McpClient.Ide,
            p =>
                p.Code(
                    false,
                    "globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json"
                ),
            Servers,
            EntryShape.Command
        ),
        new(
            "codebuddy",
            "cli",
            McpClient.Cli,
            p =>
                p.Existing(
                    p.Home(".codebuddy/.mcp.json"),
                    p.Home(".codebuddy/mcp.json"),
                    p.Home(".codebuddy.json")
                ),
            Servers,
            EntryShape.TypedCommand
        )
        {
            Jsonc = true,
        },
        new(
            "codebuddy",
            "desktop",
            McpClient.Desktop,
            p => p.Existing(p.Home(".workbuddy/.mcp.json"), p.Home(".workbuddy/mcp.json")),
            Servers,
            EntryShape.TypedCommand
        )
        {
            Jsonc = true,
        },
        new(
            "codewhale",
            "cli",
            McpClient.Cli,
            p => p.Home(".codewhale/mcp.json"),
            Servers,
            EntryShape.TypedCommand
        ),
        new(
            "codex",
            "cli",
            McpClient.Cli,
            p => p.Home(".codex/config.toml"),
            "mcp_servers",
            EntryShape.CommandWritesApproved
        ),
        new(
            "commandcode",
            "cli",
            McpClient.Cli,
            p => p.Home(".commandcode/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "copilot",
            "cli",
            McpClient.Cli,
            p => p.Home(".copilot/mcp-config.json"),
            Servers,
            EntryShape.LocalCommand
        ),
        new(
            "copilot",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".copilot/mcp-config.json"),
            Servers,
            EntryShape.LocalCommand
        ),
        new(
            "crush",
            "cli",
            McpClient.Cli,
            p => p.Home(".config/crush/crush.json"),
            "mcp",
            EntryShape.Command
        ),
        new(
            "cursor",
            "cli",
            McpClient.Cli,
            p => p.Home(".cursor/mcp.json"),
            Servers,
            EntryShape.TypedCommand
        ),
        new(
            "cursor",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".cursor/mcp.json"),
            Servers,
            EntryShape.TypedCommand
        ),
        new("devin", "cli", McpClient.Cli, p => p.Devin(), Servers, EntryShape.Command),
        new("devin", "desktop", McpClient.Desktop, p => p.Devin(), Servers, EntryShape.Command),
        new(
            "droid",
            "cli",
            McpClient.Cli,
            p => p.Home(".factory/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "gemini",
            "cli",
            McpClient.Cli,
            p => p.Home(".gemini/settings.json"),
            Servers,
            EntryShape.Command,
            Duplicates: DuplicateName.OverridesByScope,
            PluginServes: "rtok"
        ),
        new(
            "grok",
            "cli",
            McpClient.Cli,
            p => p.Home(".grok/config.toml"),
            "mcp_servers",
            EntryShape.Command
        ),
        new(
            "junie",
            "cli",
            McpClient.Cli,
            p => p.Home(".junie/mcp/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "kilo",
            "cli",
            McpClient.Cli,
            p => p.Home(".config/kilo/kilo.json"),
            "mcp",
            EntryShape.LocalArgv
        ),
        new(
            "kilo",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".config/kilo/kilo.json"),
            "mcp",
            EntryShape.LocalArgv
        ),
        new(
            "kimi",
            "cli",
            McpClient.Cli,
            p => p.Home(".kimi-code/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "kimi",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".kimi-code/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "kiro",
            "cli",
            McpClient.Cli,
            p => p.Home(".kiro/settings/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "kiro",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".kiro/settings/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "mimo",
            "cli",
            McpClient.Cli,
            p =>
                p.Existing(
                    p.Home(".config/mimocode/mimocode.json"),
                    p.Home(".config/mimocode/mimocode.jsonc")
                ),
            "mcp",
            EntryShape.LocalArgv
        ),
        new(
            "mimo",
            "desktop",
            McpClient.Desktop,
            p =>
                p.Existing(
                    p.Home(".config/mimocode/mimocode.json"),
                    p.Home(".config/mimocode/mimocode.jsonc")
                ),
            "mcp",
            EntryShape.LocalArgv
        ),
        new(
            "omp",
            "cli",
            McpClient.Cli,
            p => p.Home(".omp/agent/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "opencode",
            "cli",
            McpClient.Cli,
            p => p.Home(".config/opencode/opencode.json"),
            "mcp",
            EntryShape.LocalArgv
        ),
        new(
            "opencode",
            "desktop",
            McpClient.Desktop,
            p => p.AppData("ai.opencode.desktop/opencode.json"),
            "mcp",
            EntryShape.LocalArgv
        ),
        new(
            "openhands",
            "cli",
            McpClient.Cli,
            p => p.Home(".openhands/mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "qwen",
            "cli",
            McpClient.Cli,
            p => p.Home(".qwen/settings.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "roo",
            "desktop",
            McpClient.Ide,
            p =>
                p.Code(
                    false,
                    "globalStorage/rooveterinaryinc.roo-cline/settings/mcp_settings.json"
                ),
            Servers,
            EntryShape.Command
        ),
        new(
            "vscode",
            "stable",
            McpClient.Ide,
            p => p.Code(false, "mcp.json"),
            "servers",
            EntryShape.TypedCommand
        ),
        new(
            "vscode",
            "insiders",
            McpClient.Ide,
            p => p.Code(true, "mcp.json"),
            "servers",
            EntryShape.TypedCommand
        ),
        new(
            "warp",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".warp/.mcp.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "windsurf",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".codeium/windsurf/mcp_config.json"),
            Servers,
            EntryShape.Command
        ),
        new(
            "zcode",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".zcode/cli/config.json"),
            "mcp.servers",
            EntryShape.Command,
            Absolute: true
        ),
        new(
            "zed",
            "cli",
            McpClient.Cli,
            p => p.Home(".config/zed/settings.json"),
            "context_servers",
            EntryShape.Command
        )
        {
            Jsonc = true,
        },
        new(
            "zed",
            "desktop",
            McpClient.Desktop,
            p => p.Home(".config/zed/settings.json"),
            "context_servers",
            EntryShape.Command
        )
        {
            Jsonc = true,
        },
    ];
}
