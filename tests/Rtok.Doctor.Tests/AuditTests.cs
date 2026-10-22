namespace Rtok.Doctor.Tests;

public sealed class AuditTests
{
    private static DoctorFake WithBigServer(string baseUrl)
    {
        var f = new DoctorFake();
        f.Exe("docs");
        f.Write(f.At(".claude.json"), """{"mcpServers": {"docs": {"command": "docs"}}}""");
        f.Tools["docs"] =
        [
            string.Join(' ', Enumerable.Repeat("Reads the documentation of a package.", 40)),
        ];
        f.Var("ANTHROPIC_BASE_URL", baseUrl);
        f.Config.Doctor.ToolsRewriteMinDescTokens = 100;
        return f;
    }

    [Fact]
    public void Tools_rewrite_advice_needs_the_token_floor_and_rtoks_proxy_in_the_chain()
    {
        var through = WithBigServer($"http://127.0.0.1:{WithBigServer("x").Config.Proxy.Port}");
        var report = through.Page();
        Assert.NotNull(report.ToolsRewriteAdvice);
        Assert.StartsWith(
            "mcp descriptions ~",
            report.ToolsRewriteAdvice,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("tools_rewrite", DoctorText.Render(report), StringComparison.Ordinal);

        var elsewhere = WithBigServer("http://10.0.0.5:9000");
        Assert.Null(elsewhere.Page().ToolsRewriteAdvice);

        var small = WithBigServer($"http://127.0.0.1:{through.Config.Proxy.Port}");
        small.Config.Doctor.ToolsRewriteMinDescTokens = 1_000_000;
        Assert.Null(small.Page().ToolsRewriteAdvice);
    }

    [Fact]
    public void Anthropics_own_endpoint_never_counts_as_a_custom_base()
    {
        var f = WithBigServer("https://api.anthropic.com/");
        var report = f.Page();
        Assert.Equal("", report.Proxy);
        Assert.Equal("enabled", report.McpToolSearch.State);
        Assert.Null(report.ToolsRewriteAdvice);
        Assert.Contains("proxy none\n", DoctorText.Render(report), StringComparison.Ordinal);
    }

    [Fact]
    public void The_chain_follows_health_upstreams_and_stops_at_a_repeat()
    {
        var f = new DoctorFake();
        f.Var("ANTHROPIC_BASE_URL", "http://127.0.0.1:8790");
        f.Health["127.0.0.1:8790"] =
            """{"checks": {"upstream": {"url": "http://localhost:9000"}}}""";
        f.Health["localhost:9000"] =
            """{"config": {"anthropic_api_url": "http://127.0.0.1:8790"}}""";
        Assert.Equal("8790→localhost:9000", f.Page().Proxy);
    }

    [Fact]
    public void Instructions_count_claude_md_and_plugin_session_start_and_flag_duplicates()
    {
        var f = new DoctorFake();
        f.Config.Doctor.Instructions = true;
        const string Shared = "Always run the formatter before committing any change to the repo.";
        f.Write(f.At(".claude/CLAUDE.md"), $"# Rules\n{Shared}\n");
        var dir = f.Plugin(
            "ctx@market",
            DoctorFake.Hooks(("SessionStart", null, "cat ${CLAUDE_PLUGIN_ROOT}/context.md"))
        );
        f.Write(f.Fs.Path.Combine(dir, "context.md"), $"{Shared}\nMore context from the plugin.\n");
        f.Exe("cat");

        var audit = f.Page().Instructions!;

        Assert.Contains(audit.Rows, r => r.Name is "claude-user" && r.Tokens > 0);
        Assert.Contains(audit.Rows, r => r.Name is "plugin:ctx@market" && r.Tokens > 0);
        var dup = Assert.Single(audit.Duplicates);
        Assert.StartsWith(dup.Sentence, Shared, StringComparison.Ordinal);
        Assert.Equal(["claude-user", "plugin:ctx@market"], dup.Names);
    }

    [Fact]
    public void Instructions_over_the_budget_warn()
    {
        var f = new DoctorFake();
        f.Config.Doctor.Instructions = true;
        f.Config.Doctor.InstructionWarnTokens = 5;
        f.Write(
            f.InProject("AGENTS.md"),
            string.Join('\n', Enumerable.Repeat("A line of project instructions here.", 10))
        );
        Assert.Contains(f.Page().Instructions!.Rows, r => r.Name is "agents-project" && r.Warn);
        Assert.Contains(" WARN\n", f.Text(), StringComparison.Ordinal);
    }

    [Fact]
    public void Without_the_flag_there_is_no_instruction_audit()
    {
        var f = new DoctorFake();
        f.Write(f.At(".claude/CLAUDE.md"), "rules\n");
        Assert.Null(f.Page().Instructions);
    }

    [Fact]
    public void Skills_flag_long_descriptions_big_bodies_and_duplicates()
    {
        var f = new DoctorFake();
        var longDesc = new string('d', 130);
        f.Write(
            f.At(".claude/skills/review/SKILL.md"),
            $"---\nname: review\ndescription: {longDesc}\n---\nbody\n"
        );
        f.Write(
            f.InProject(".claude/skills/review/SKILL.md"),
            "---\nname: review\ndescription: short\n---\nother body\n"
        );
        f.Write(
            f.At(".claude/skills/big/SKILL.md"),
            "---\ndescription: big one\n---\n" + new string('x', 9000)
        );

        var skills = f.Page().Skills!;

        Assert.Contains(skills.Rows, r => r.Name is "review" && r.Source is "user" && r.WarnDesc);
        Assert.Contains(skills.Rows, r => r.Name is "big" && r.WarnBody && !r.WarnDesc);
        var dup = Assert.Single(skills.Duplicates!);
        Assert.Equal("review", dup.Name);
        Assert.False(dup.Identical);
        Assert.Equal(2, dup.Copies.Count);
        var text = f.Text();
        Assert.Contains(
            "WARN duplicate skill `review`: 2 listings, bodies differ",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("descriptions over 120 chars: 1 skills", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlaps_name_claude_auto_memory_when_recall_is_on()
    {
        var f = new DoctorFake();
        f.Write(f.At(".claude/settings.json"), "{}");
        f.Config.Plugins.Memory.Enabled = true;
        f.Config.Plugins.Memory.RecallTokens = 200;
        Assert.Contains(
            f.Page().Overlaps!,
            l => l.Contains("Claude Code auto-memory", StringComparison.Ordinal)
        );

        f.Config.Plugins.Memory.Enabled = false;
        Assert.DoesNotContain(
            f.Page().Overlaps ?? [],
            l => l.Contains("auto-memory", StringComparison.Ordinal)
        );
    }
}
