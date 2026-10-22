using Rtok.Plugin.Sdk;

namespace Rtok.Agents.Hosts;

/// <summary>Every host rtok installs into, in <c>agents list</c> order (rtok's host folders @ ef091589).</summary>
internal static class AgentHostTable
{
    public static IReadOnlyList<AgentHost> All { get; } =
    [
        new(
            "claude",
            [
                new(HostKind.Cli, "Claude Code", ["claude"], []),
                new(
                    HostKind.Desktop,
                    "Claude Desktop",
                    [],
                    ["/Applications/Claude.app", "$LOCALAPPDATA/AnthropicClaude/claude.exe"]
                ),
            ],
            Hooks: new(Cli: Support.Yes, Desktop: Support.No("Claude Desktop has no hook events")),
            Mcp: Support.Yes,
            Proxy: new(
                Cli: Support.Flag("--proxy"),
                Desktop: Support.No(
                    "Claude Desktop has no base-URL setting; its requests do not pass through the proxy"
                )
            ),
            Plugin: new(
                Cli: Support.Yes,
                Desktop: Support.No(
                    "Claude Desktop loads MCP from claude_desktop_config.json; there is no plugin directory to link"
                )
            )
        ),
        new(
            "cursor",
            [
                new(HostKind.Cli, "Cursor CLI", ["cursor-agent", "agent"], []),
                new(
                    HostKind.Desktop,
                    "Cursor",
                    ["cursor"],
                    [
                        "/Applications/Cursor.app",
                        "$LOCALAPPDATA/Programs/cursor/Cursor.exe",
                        "/opt/Cursor",
                    ]
                ),
            ],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No("Cursor has no base-URL setting to point at the proxy"),
            Plugin: Support.Yes,
            Shared: true,
            PluginSurfaces: [Surface.Hook]
        ),
        new(
            "codex",
            [new(HostKind.Cli, "Codex", ["codex"], [])],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.Flag("--proxy"),
            Plugin: Support.Yes
        ),
        new(
            "opencode",
            [
                new(HostKind.Cli, "OpenCode", ["opencode"], []),
                new(
                    HostKind.Desktop,
                    "OpenCode Desktop",
                    ["opencode-desktop"],
                    [
                        "/Applications/OpenCode.app",
                        "$LOCALAPPDATA/Programs/@opencode-aidesktop/OpenCode.exe",
                        "$LOCALAPPDATA/Programs/OpenCode/OpenCode.exe",
                    ]
                ),
            ],
            Hooks: Support.No(
                "OpenCode has no shell hook events; the linked plugin filters bash output instead"
            ),
            Mcp: Support.Yes,
            Proxy: Support.Yes,
            Plugin: Support.Yes,
            PluginSurfaces: [Surface.Cli]
        ),
        new(
            "kilo",
            [
                new(HostKind.Cli, "Kilo Code CLI", ["kilo"], []),
                new(
                    HostKind.Desktop,
                    "Kilo Code for VS Code",
                    [],
                    [
                        "/Applications/Visual Studio Code.app",
                        "$LOCALAPPDATA/Programs/Microsoft VS Code/Code.exe",
                    ]
                ),
            ],
            Hooks: Support.No(
                "Kilo Code has no shell hook events; the linked plugin filters bash output instead"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Kilo Code keeps provider base URLs in its provider settings, which setup does not edit"
            ),
            Plugin: Support.Yes,
            Shared: true,
            PluginSurfaces: [Surface.Cli]
        ),
        new(
            "pi",
            [new(HostKind.Cli, "pi", ["pi"], [])],
            Hooks: Support.No("pi has no hook events; the extension owns the bash call path"),
            Mcp: Support.No(
                "pi philosophy is no MCP; registerTool is the plugin path when setup.pi.tools is true"
            ),
            Proxy: Support.No(
                "pi provider base URLs live in its models config, which setup does not edit"
            ),
            Plugin: Support.Yes,
            PluginSurfaces: [Surface.Cli, Surface.Mcp]
        ),
        new(
            "omp",
            [new(HostKind.Cli, "oh my pi", ["omp"], [])],
            Hooks: Support.No(
                "omp hooks are in-process TS modules; the linked pi extension owns that path"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "omp provider base URLs live in models.yml, which setup does not edit"
            ),
            Plugin: Support.Flag("--yes"),
            PluginSurfaces: [Surface.Cli]
        ),
        new(
            "zcode",
            [
                new(
                    HostKind.Desktop,
                    "ZCode",
                    [],
                    ["/Applications/ZCode.app", "$LOCALAPPDATA/Programs/ZCode/ZCode.exe"]
                ),
            ],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No(
                "ZCode providers are per-id tables with their own keys and base URLs; setup does not edit them"
            ),
            Plugin: Support.Yes,
            PluginSurfaces: [Surface.Hook]
        ),
        new(
            "kimi",
            [
                new(HostKind.Cli, "Kimi Code", ["kimi"], []),
                new(
                    HostKind.Desktop,
                    "Kimi Code Desktop",
                    [],
                    [
                        "/Applications/Kimi Code.app",
                        "$LOCALAPPDATA/Programs/Kimi Code/Kimi Code.exe",
                    ]
                ),
            ],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Kimi Code providers are [providers.<name>] tables with their own base_url and keys; setup does not edit them"
            ),
            Plugin: Support.Offer("--yes"),
            Shared: true,
            PluginSurfaces: [Surface.Hook]
        ),
        new(
            "grok",
            [
                new(
                    HostKind.Cli,
                    "Grok Build",
                    ["grok"],
                    ["~/.grok/bin/grok", "$GROK_HOME/bin/grok"]
                ),
            ],
            Hooks: Support.No(
                "Grok fires one hook set — the plugin's, or rtok's Claude hooks through [compat.claude] hooks — and setup adds no second set"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Grok Build providers live in its own settings tables; setup does not edit them"
            ),
            Plugin: Support.Offer("--yes"),
            Shared: true,
            PluginSurfaces: [Surface.Hook]
        ),
        new(
            "vscode",
            [
                new(
                    HostKind.Desktop,
                    "VS Code",
                    ["code"],
                    [
                        "/Applications/Visual Studio Code.app",
                        "$LOCALAPPDATA/Programs/Microsoft VS Code/Code.exe",
                    ],
                    Surface: "stable"
                ),
                new(
                    HostKind.Desktop,
                    "VS Code - Insiders",
                    ["code-insiders"],
                    [
                        "/Applications/Visual Studio Code - Insiders.app",
                        "$LOCALAPPDATA/Programs/Microsoft VS Code Insiders/Code - Insiders.exe",
                    ],
                    Surface: "insiders"
                ),
            ],
            Hooks: Support.No(
                "no direct hooks file rtok writes; the linked plugin's hooks/hooks.json is what VS Code's Local harness runs (chat.useClaudeHooks)"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Copilot in VS Code has no documented base-URL setting to point at the proxy"
            ),
            Plugin: Support.Flag("--yes"),
            Shared: true,
            PluginSurfaces: [Surface.Hook]
        ),
        new(
            "copilot",
            [
                new(HostKind.Cli, "Copilot CLI", ["copilot"], []),
                new(
                    HostKind.Desktop,
                    "GitHub Copilot",
                    [],
                    [
                        "/Applications/GitHub Copilot.app",
                        "$LOCALAPPDATA/Programs/GitHub Copilot/GitHub Copilot.exe",
                    ]
                ),
            ],
            Hooks: new(
                Cli: Support.Yes,
                Desktop: Support.No(
                    "the GitHub Copilot app does not document hooks; the shared hooks/rtok.json is written for the CLI"
                )
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Copilot BYOK is env-only (COPILOT_PROVIDER_BASE_URL); there is no config file to point at the proxy"
            ),
            Plugin: new(
                Cli: Support.Flag("--yes"),
                Desktop: Support.No(
                    "the GitHub Copilot app does not document plugin installs; `copilot plugin` serves the CLI"
                )
            ),
            Shared: true,
            PluginSurfaces: [Surface.Hook]
        ),
        new(
            "commandcode",
            [new(HostKind.Cli, "Command Code CLI", ["command-code", "cmd"], [])],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No("Command Code has no documented base-URL override for the proxy"),
            Plugin: Support.Yes,
            Shared: true,
            PluginSurfaces: [Surface.Hook]
        ),
        new(
            "aider",
            [new(HostKind.Cli, "aider", ["aider"], [])],
            Hooks: Support.No("aider has no hook events; it reads .aider.conf.yml and .env"),
            Mcp: Support.No("aider has no MCP support"),
            Proxy: Support.Flag("--proxy"),
            Plugin: Support.No("aider has no plugin directory to link")
        ),
        new(
            "windsurf",
            [
                new(
                    HostKind.Desktop,
                    "Windsurf",
                    [],
                    ["/Applications/Windsurf.app", "$LOCALAPPDATA/Programs/Windsurf/Windsurf.exe"]
                ),
            ],
            Hooks: Support.No(
                "Cascade hooks speak agent_action_name/tool_info, not hook_event_name/tool_name; rtok hook has no --host windsurf mapping"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Windsurf serves its own models; there is no documented base-URL setting to point at the proxy"
            ),
            Plugin: Support.No(
                "Cascade has no local plugin directory to link; rules and memories live in .windsurf/"
            )
        ),
        new(
            "zed",
            [
                new(HostKind.Cli, "Zed CLI", ["zed"], []),
                new(
                    HostKind.Desktop,
                    "Zed",
                    [],
                    ["/Applications/Zed.app", "$LOCALAPPDATA/Programs/Zed/Zed.exe"]
                ),
            ],
            Hooks: Support.No(
                "Zed has no shell hook events; its agent runs tools itself and takes external agents over ACP"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Zed serves hosted models or provider API keys; there is no documented base-URL setting to point at the proxy"
            ),
            Plugin: Support.No(
                "Zed extensions install from the marketplace; there is no local directory to link"
            ),
            Shared: true
        ),
        new(
            "cline",
            [
                new(HostKind.Cli, "Cline CLI", ["cline"], []),
                new(
                    HostKind.Desktop,
                    "Cline for VS Code",
                    [],
                    [
                        "/Applications/Visual Studio Code.app",
                        "$LOCALAPPDATA/Programs/Microsoft VS Code/Code.exe",
                    ]
                ),
            ],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No(
                "the Anthropic base URL is extension state, not a file rtok may edit"
            ),
            Plugin: Support.Flag("--yes"),
            Shared: true
        ),
        new(
            "gemini",
            [new(HostKind.Cli, "Gemini CLI", ["gemini"], [])],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Gemini CLI has no documented base-URL setting; its own HTTP_PROXY/HTTPS_PROXY covers MCP server transport only, not the model API"
            ),
            Plugin: Support.Flag("--yes"),
            PluginSurfaces: [Surface.Mcp, Surface.Hook]
        ),
        new(
            "codewhale",
            [new(HostKind.Cli, "CodeWhale", ["codewhale", "codew"], [])],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No(
                "CodeWhale providers are [providers.<name>] tables with their own base_url and keys (docs/CONFIGURATION.md); setup does not edit them"
            ),
            Plugin: Support.No(
                "CodeWhale's plugin-bundle system needs a reviewed remote source and exact environment-source references (docs/PLUGIN_BUNDLES.md); there is no local directory to link"
            ),
            PluginSurfaces: [Surface.Mcp, Surface.Hook]
        ),
        new(
            "mimo",
            [
                new(HostKind.Cli, "MiMo Code", ["mimo"], []),
                new(
                    HostKind.Desktop,
                    "MiMo Desktop",
                    [],
                    ["/Applications/Xiaomi MiMo AI.app", "~/Applications/Xiaomi MiMo AI.app"]
                ),
            ],
            Hooks: Support.No(
                "MiMo Code has no shell hook events; the linked plugin filters bash output instead"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "MiMo Code has no documented base-URL override; MIMOCODE_HOME/MIMOCODE_CONFIG relocate config, not the model endpoint"
            ),
            Plugin: Support.Yes,
            Shared: true,
            PluginSurfaces: [Surface.Cli]
        ),
        new(
            "antigravity",
            [
                new(HostKind.Cli, "Antigravity CLI", ["agy"], []),
                new(HostKind.Desktop, "Antigravity", [], ["/Applications/Antigravity.app"]),
            ],
            Hooks: Support.No(
                "Antigravity's PreToolUse cannot rewrite tool input and PostToolUse cannot add context"
            ),
            Mcp: Support.No("rtok's MCP ships inside the plugin, the only install path"),
            Proxy: Support.No("Antigravity documents no model base-URL override"),
            Plugin: new(Cli: Support.Offer("--yes"), Desktop: Support.Flag("--yes")),
            PluginSurfaces: [Surface.Mcp]
        ),
        new(
            "devin",
            [
                new(HostKind.Cli, "Devin CLI", ["devin"], []),
                new(
                    HostKind.Desktop,
                    "Devin",
                    [],
                    ["/Applications/Devin.app", "$LOCALAPPDATA/Programs/Devin/Devin.exe"]
                ),
            ],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Devin's proxy key is an HTTP proxy for CLI traffic, not a model API base URL"
            ),
            Plugin: Support.Offer("--yes"),
            Shared: true,
            PluginSurfaces: [Surface.Hook, Surface.Mcp]
        ),
        new(
            "roo",
            [
                new(
                    HostKind.Desktop,
                    "Roo Code",
                    [],
                    [
                        "/Applications/Visual Studio Code.app",
                        "$LOCALAPPDATA/Programs/Microsoft VS Code/Code.exe",
                    ]
                ),
            ],
            Hooks: Support.No(
                "Roo Code's docs describe mcp_settings.json and .roo/mcp.json, not shell hook events"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Roo Code has no documented model base-URL file; roo-cline.debugProxy is a debug proxy, not the model endpoint"
            ),
            Plugin: Support.No(
                "Roo Code has no documented local plugin directory to link; modes live in the extension"
            )
        ),
        new(
            "qwen",
            [new(HostKind.Cli, "Qwen Code", ["qwen"], [])],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Qwen Code's model.baseUrl is written by the model picker; modelProviders entries carry their own baseUrl, which setup does not edit"
            ),
            Plugin: Support.Flag("--yes"),
            PluginSurfaces: [Surface.Hook]
        ),
        new(
            "codebuddy",
            [
                new(HostKind.Cli, "CodeBuddy Code", ["codebuddy"], []),
                new(
                    HostKind.Desktop,
                    "WorkBuddy",
                    [],
                    [
                        "/Applications/WorkBuddy.app",
                        "~/Applications/WorkBuddy.app",
                        "$LOCALAPPDATA/Programs/WorkBuddy",
                    ]
                ),
            ],
            Hooks: Support.Yes,
            Mcp: Support.Yes,
            Proxy: Support.No(
                "CodeBuddy Code takes its endpoint from the CODEBUDDY_BASE_URL environment variable; there is no config file to point at the proxy"
            ),
            Plugin: Support.Flag("--yes"),
            PluginSurfaces: [Surface.Hook, Surface.Mcp]
        ),
        new(
            "goose",
            [
                new(HostKind.Cli, "Goose CLI", ["goose"], []),
                new(HostKind.Desktop, "Goose", [], ["/Applications/Goose.app"]),
            ],
            Hooks: Support.No(
                "Goose runs hooks from Open Plugins directories with its own event names, tool names and decision shape; rtok has no adapter for that payload yet"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Goose documents its provider endpoint as the GOOSE_PROVIDER__HOST environment variable only; there is no file to point at the proxy"
            ),
            Plugin: Support.No(
                "Goose plugins are Open Plugins directories (plugin.json, hooks/hooks.json); rtok ships no bundle in that format yet"
            )
        ),
        new(
            "droid",
            [new(HostKind.Cli, "Factory Droid", ["droid"], [])],
            Hooks: Support.No(
                "Droid documents hooks, but rtok has no checked mapping of its tool names and PreToolUse output yet"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Droid has no single model base-URL setting; each customModels entry carries its own baseUrl"
            ),
            Plugin: Support.No(
                "Droid plugins install into its own cache through `droid plugin install`; rtok ships no Droid plugin"
            )
        ),
        new(
            "kiro",
            [
                new(HostKind.Cli, "Kiro CLI", ["kiro-cli"], []),
                new(
                    HostKind.Desktop,
                    "Kiro IDE",
                    [],
                    ["/Applications/Kiro.app", "~/Applications/Kiro.app"]
                ),
            ],
            Hooks: Support.No(
                "Kiro's hooks page does not state the exit-code or output-to-context rules of command hooks"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No("Kiro documents no model base-URL setting"),
            Plugin: Support.No("Kiro documents no local plugin directory to link"),
            Shared: true
        ),
        new(
            "junie",
            [new(HostKind.Cli, "Junie CLI", ["junie"], [])],
            Hooks: Support.No(
                "Junie documents only a SessionStart hook, and rtok's hooks need tool-use events"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No("Junie has no documented model base-URL file"),
            Plugin: Support.No("Junie has no documented local plugin directory to link")
        ),
        new(
            "amp",
            [new(HostKind.Cli, "Amp", ["amp"], [])],
            Hooks: Support.No(
                "Amp has no shell hook events; its plugin events (tool.call, tool.result) are TypeScript handlers"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No("amp.url is Amp's own server URL, not a model-provider base URL"),
            Plugin: Support.No(
                "Amp loads .ts and .js plugins from its plugins directory, but rtok ships no plugin for its API yet"
            )
        ),
        new(
            "warp",
            [new(HostKind.Desktop, "Warp", [], ["/Applications/Warp.app"])],
            Hooks: Support.No("Warp documents no hook surface"),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Warp documents no setting that points its agent at another endpoint"
            ),
            Plugin: Support.No("Warp documents no plugin format; rtok ships no Warp plugin")
        ),
        new(
            "continue",
            [
                new(HostKind.Cli, "Continue CLI", ["cn"], []),
                new(
                    HostKind.Desktop,
                    "Continue",
                    [],
                    [
                        "/Applications/Visual Studio Code.app",
                        "$LOCALAPPDATA/Programs/Microsoft VS Code/Code.exe",
                    ]
                ),
            ],
            Hooks: Support.No(
                "Continue's docs describe config.yaml, mcpServers and rules, not shell hook events"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Continue sets a model's apiBase per model in config.yaml; there is no single endpoint file to point at the proxy"
            ),
            Plugin: Support.No(
                "Continue has no documented local plugin directory to link; blocks and rules live in config.yaml and .continue/"
            )
        ),
        new(
            "crush",
            [new(HostKind.Cli, "Crush", ["crush"], [])],
            Hooks: Support.No(
                "Crush documents only a PreToolUse hook, and rtok has no checked mapping of its tool names"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Crush takes provider base URLs per provider entry; rtok has no checked single endpoint to point at the proxy"
            ),
            Plugin: Support.No(
                "Crush has no documented local plugin directory to link; it loads skills, not plugins"
            )
        ),
        new(
            "openhands",
            [new(HostKind.Cli, "OpenHands", ["openhands"], [])],
            Hooks: Support.No(
                "OpenHands documents hooks only as a project `.openhands/hooks.json`; no user-level file is documented"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "rtok has not checked OpenHands's LLM base-URL setting, so it writes none"
            ),
            Plugin: Support.No("rtok ships no OpenHands plugin")
        ),
        new(
            "augment",
            [new(HostKind.Cli, "Augment Auggie CLI", ["auggie"], [])],
            Hooks: Support.No(
                "Augment hooks must be script files and PreToolUse can only deny (no updatedInput), so rtok cannot rewrite a call through them"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Augment documents no model endpoint setting or bring-your-own key to point at the proxy"
            ),
            Plugin: Support.No(
                "Auggie plugins come from marketplaces through `auggie plugin install`; rtok ships no Augment plugin"
            )
        ),
        new(
            "reasonix",
            [new(HostKind.Cli, "Reasonix", ["reasonix", "dsnix"], [])],
            Hooks: Support.No(
                "Reasonix hooks (settings.json) use their own event payload and tool names, which rtok has no checked mapping for"
            ),
            Mcp: Support.Yes,
            Proxy: Support.No(
                "Reasonix has no single endpoint setting; each [[providers]] entry carries its own base_url"
            ),
            Plugin: Support.No(
                "Reasonix plugin packages are its own format; rtok ships no Reasonix plugin"
            )
        ),
    ];

    /// <summary>Hosts rtok was asked for and has no installer for, in <c>agents list</c> order.</summary>
    public static IReadOnlyList<UnsupportedHost> Unsupported { get; } =
    [
        new(
            "trae",
            "Trae",
            HostKind.Desktop,
            new DateOnly(2026, 10, 22),
            ["https://docs.trae.ai/ide/add-mcp-servers"],
            "Trae documents global MCP servers only through its Settings > MCP panel and names no user-level MCP file; the one file it documents is the project's .trae/mcp.json"
        ),
    ];
}
