using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Hooks;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>rtok's hook entries in each host's own file: written, rewritten when stale, pruned, taken back byte for byte.</summary>
public sealed class HooksTests
{
    private const string Resolver =
        "command -v rtok >/dev/null 2>&1 && exec rtok hook PreToolUse; "
        + "[ -x \"$HOME/.ketch/bin/rtok\" ] && exec \"$HOME/.ketch/bin/rtok\" hook PreToolUse; true; exit 0";

    /// <summary>rtok's update test fixture (rtok tests/agents_update.rs STALE_SETTINGS).</summary>
    private const string Stale = """
        {"theme":"dark","hooks":{
          "PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"/old/store/rtok/v0.1.0/rtok hook PreToolUse","timeout":1}]}],
          "PostToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"rtok hook PostToolUse"}]}],
          "Stop":[{"hooks":[{"type":"command","command":"rtok hook Stop"},{"type":"command","command":"notify-send done"}]}]
        }}
        """;

    private static readonly Apply Write = new();

    private static HookFile File(
        string host,
        McpOs os = McpOs.Linux,
        HostKind kind = HostKind.Cli,
        Action<FakeHome>? setup = null
    )
    {
        var home = new FakeHome(os);
        setup?.Invoke(home);
        var h = AgentHosts.Find(host)!;
        return new HooksModule(h, h.Variants.First(v => v.Kind == kind), home.Context()).Files[0];
    }

    [Fact]
    public void Claude_on_an_empty_file_adds_ten_hooks_with_the_resolver()
    {
        var (report, text) = File("claude").Install("");
        Assert.EndsWith("\n10 additions", report, StringComparison.Ordinal);
        Assert.Contains($"+ PreToolUse Bash {Resolver}\n", report, StringComparison.Ordinal);
        Assert.Contains("+ SessionStart command -v rtok", report, StringComparison.Ordinal);
        Assert.Contains("printf '%s' '{\\\"hookSpecificOutput\\\"", text, StringComparison.Ordinal);
        Assert.Contains("\"timeout\": 5", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stale_claude_file_is_rewritten_pruned_and_then_current()
    {
        var file = File("claude");
        var (report, text) = file.Install(Stale);
        var lines = report.Split('\n');
        Assert.Contains("- PostToolUse Bash rtok hook PostToolUse", lines, StringComparer.Ordinal);
        Assert.Contains("- Stop rtok hook Stop", lines, StringComparer.Ordinal);
        Assert.Contains($"~ PreToolUse Bash {Resolver}", lines, StringComparer.Ordinal);
        Assert.Contains(lines, l => l.StartsWith("+ PostToolUse * ", StringComparison.Ordinal));
        Assert.Equal("9 additions, 1 updates, 2 removals", lines[^1]);
        Assert.Contains("notify-send done", text, StringComparison.Ordinal);
        Assert.Contains("\"theme\":\"dark\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/old/store", text, StringComparison.Ordinal);
        Assert.Equal((Apply.NoChanges, text), file.Install(text));
    }

    [Fact]
    public void Claude_install_then_remove_gives_the_foreign_file_back_byte_for_byte()
    {
        var file = File("claude");
        const string seed = """
            {
              // mine
              "hooks": {"PreToolUse": [{"matcher": "Bash", "hooks": [{"type": "command", "command": "echo other"}]}]}
            }
            """;
        var (_, installed) = file.Install(seed);
        var (report, removed) = file.Remove(installed, Write);
        Assert.Equal("10 removed", report);
        Assert.Equal(seed, removed);
    }

    [Fact]
    public void A_user_chain_ending_in_rtok_stays_theirs()
    {
        const string seed = """
            {"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"notify-send hi && rtok hook PreToolUse"}]}]}}
            """;
        var file = File("claude");
        Assert.Equal((Apply.NoChanges, seed), file.Remove(seed, Write));
        Assert.Contains(
            "notify-send hi && rtok hook PreToolUse",
            file.Install(seed).Text,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_edited_hook_asks_before_it_goes()
    {
        var file = File("claude");
        var (_, installed) = file.Install("");
        var at = installed.IndexOf("\"timeout\": 5", StringComparison.Ordinal);
        var edited = string.Concat(
            installed.AsSpan(0, at),
            "\"timeout\": 9",
            installed.AsSpan(at + 12)
        );
        var (report, kept) = file.Remove(edited, new Apply { DryRun = true });
        Assert.Contains(
            $"? hooks.PreToolUse Bash in {file.Path} (changed by you; remove asks)",
            report,
            StringComparison.Ordinal
        );
        Assert.EndsWith("\n9 removed", report, StringComparison.Ordinal);
        var (yes, gone) = file.Remove(edited, new Apply { Yes = true });
        Assert.Equal("10 removed", yes);
        Assert.DoesNotContain("rtok", gone, StringComparison.Ordinal);
        Assert.Contains("rtok", kept, StringComparison.Ordinal);
    }

    [Fact]
    public void On_windows_without_rtok_on_path_hooks_name_the_quoted_binary()
    {
        var (report, _) = File("claude", McpOs.Windows).Install("");
        Assert.Contains(
            "+ PreToolUse Bash \"C:\\Program Files\\rtok\\rtok.exe\" hook PreToolUse\n",
            report,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Codex_takes_its_bare_hooks_from_before_the_host_flag()
    {
        const string seed = """
            {"hooks":{"PreCompact":[{"hooks":[{"type":"command","command":"rtok hook PreCompact","timeout":5}]}]}}
            """;
        var (report, text) = File("codex").Install(seed);
        Assert.Contains("~ PreCompact command -v rtok", report, StringComparison.Ordinal);
        Assert.Contains("exec rtok hook PreCompact --host codex;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"rtok hook PreCompact\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Zcode_turns_hooks_on_and_writes_milliseconds()
    {
        var (report, text) = File("zcode", kind: HostKind.Desktop).Install("");
        Assert.StartsWith("+ hooks.enabled = true\n", report, StringComparison.Ordinal);
        Assert.Contains("/opt/rtok/bin/rtok hook PreToolUse", text, StringComparison.Ordinal);
        Assert.Contains("\"timeoutMs\": 5000", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Gemini_maps_its_events_and_drops_nothing_foreign()
    {
        var file = File("gemini");
        var (report, text) = file.Install("{}");
        Assert.Contains(
            "+ BeforeTool rtok hook PreToolUse --host gemini\n",
            report,
            StringComparison.Ordinal
        );
        Assert.Contains("\"timeout\":5000", text, StringComparison.Ordinal);
        Assert.Equal((Apply.NoChanges, text), file.Install(text));
        Assert.Equal("{}", file.Remove(text, Write).Text);
    }

    [Fact]
    public void Cursor_braces_the_resolver_and_retires_before_submit_prompt()
    {
        const string seed = """
            {"version":1,"hooks":{"beforeSubmitPrompt":[{"command":"rtok hook UserPromptSubmit --host cursor"}]}}
            """;
        var file = File("cursor");
        var (report, text) = file.Install(seed);
        Assert.StartsWith("- beforeSubmitPrompt\n", report, StringComparison.Ordinal);
        Assert.Contains(
            "+ beforeShellExecution { command -v rtok",
            report,
            StringComparison.Ordinal
        );
        Assert.Contains("--host cursor; true; exit 0; }", text, StringComparison.Ordinal);
        var (removed, back) = file.Remove(text, Write);
        Assert.Contains("- beforeShellExecution", removed, StringComparison.Ordinal);
        Assert.Equal("{\"version\":1}", back.Trim());
    }

    [Fact]
    public void Copilot_owns_its_file_and_deletes_it_on_remove()
    {
        var home = new FakeHome();
        var host = AgentHosts.Find("copilot")!;
        var ctx = home.Context();
        var module = new HooksModule(host, host.Variants[0], ctx);
        var path = home.At(".copilot/hooks/rtok.json");
        Assert.Equal([$"+ {path} (7 events)"], module.Apply(remove: false, Write));
        Assert.Contains(
            "\"powershell\": \"$r = (Get-Command rtok",
            home.Read(path),
            StringComparison.Ordinal
        );
        Assert.Equal([Apply.NoChanges], module.Apply(remove: false, Write));
        Assert.Equal([$"- {path}"], module.Apply(remove: true, Write));
        Assert.Null(home.Read(path));
    }

    [Fact]
    public void Command_code_keeps_one_definition_per_matcher()
    {
        const string seed = """
            {"hooks":{"PreToolUse":[{"matcher":"SHELL|READ|WRITE|EDIT","hooks":[{"type":"command","command":"echo mine"}]}]}}
            """;
        var file = File("commandcode");
        var (report, text) = file.Install(seed);
        Assert.Contains(
            "+ PreToolUse rtok hook PreToolUse --host commandcode",
            report,
            StringComparison.Ordinal
        );
        Assert.Equal(2, text.Split("SHELL|READ|WRITE|EDIT").Length);
        Assert.Equal((Apply.NoChanges, text), file.Install(text));
        Assert.Equal(seed, file.Remove(text, Write).Text);
    }

    [Fact]
    public void Kimi_writes_nine_tables_rewrites_a_stale_one_and_gives_the_file_back()
    {
        const string seed = "model = \"k2\" # mine\n";
        var file = File("kimi");
        var (report, text) = file.Install(seed);
        Assert.EndsWith("\n9 additions", report, StringComparison.Ordinal);
        Assert.Contains(
            "+ [[hooks]] PreToolUse Bash rtok hook PreToolUse\n",
            report,
            StringComparison.Ordinal
        );
        Assert.Equal((Apply.NoChanges, text), file.Install(text));
        var stale = text.Replace("timeout = 5", "timeout = 1 # old", StringComparison.Ordinal);
        var (update, fixedText) = file.Install(stale);
        Assert.EndsWith("\n9 updates", update, StringComparison.Ordinal);
        Assert.Contains("timeout = 5 # old", fixedText, StringComparison.Ordinal);
        Assert.Equal(("9 removed", seed), file.Remove(text, Write));
    }

    [Fact]
    public void Codewhale_adds_one_table_and_keeps_the_users_switch()
    {
        const string seed = "[hooks]\nenabled = false\n";
        var file = File("codewhale");
        var (report, text) = file.Install(seed);
        Assert.Equal(
            "+ [[hooks.hooks]] message_submit rtok hook UserPromptSubmit --host codewhale\n1 addition",
            report
        );
        Assert.Contains("enabled = false", text, StringComparison.Ordinal);
        Assert.Equal((Apply.NoChanges, text), file.Install(text));
        Assert.Equal(("1 removed", seed), file.Remove(text, Write));
        var (_, fresh) = file.Install("");
        Assert.StartsWith(
            "[hooks]\nenabled = true\n\n[[hooks.hooks]]\n",
            fresh,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Replace_needs_yes_and_takes_out_the_legacy_stack()
    {
        var home = new FakeHome();
        home.Install(AgentHosts.Find("claude")!.Variants[0]);
        var settings = home.At(".claude/settings.json");
        home.Write(
            settings,
            """
            {"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"rtk hook claude"},{"type":"command","command":"rtok hook PreToolUse"}]}],
            "Stop":[{"hooks":[{"type":"command","command":"lean-ctx hook stop"}]}]},
            "mcpServers":{"lean-ctx":{"command":"lean-ctx"},"mine":{"command":"x"}}}
            """
        );
        home.Write(
            home.At(".claude.json"),
            """{"mcpServers":{"code-review-graph":{"command":"crg"}}}"""
        );
        var request = new HostRequest(["claude"], McpMode.Install, Cli: true, Replace: true);
        var refused = Assert.Throws<AgentsException>(() => home.Run().Run(request));
        Assert.Equal("--replace requires --yes (or --dry-run)", refused.Message);

        var text = home.Run(new HostSettings(Yes: true, Backup: false)).Run(request);
        Assert.Contains(
            "- rtk hook claude\n- lean-ctx hook stop\ndisabled MCP: lean-ctx\nremaining rtok hooks: 1\ndisabled MCP: code-review-graph\n",
            text,
            StringComparison.Ordinal
        );
        var after = home.Read(settings)!;
        Assert.DoesNotContain("Stop", after, StringComparison.Ordinal);
        Assert.Contains("\"mine\"", after, StringComparison.Ordinal);
        Assert.Contains("rtok hook PreToolUse", after, StringComparison.Ordinal);
        Assert.Equal("""{"mcpServers":{}}""", home.Read(home.At(".claude.json")));
    }
}
