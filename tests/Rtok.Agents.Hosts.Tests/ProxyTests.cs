using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Hooks;
using Rtok.Agents.Hosts.Proxy;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>rtok's proxy base URL in each host's own file: set, kept current, taken back only while it is rtok's.</summary>
public sealed class ProxyTests
{
    private const string Url = "http://127.0.0.1:8790";
    private const string OpenAi = Url + "/v1";

    private static readonly Apply Write = new();

    private static OwnedFile File(
        string host,
        HostKind kind = HostKind.Cli,
        HostSettings? settings = null
    )
    {
        var h = AgentHosts.Find(host)!;
        var ctx = new FakeHome().Context(settings);
        return new ProxyModule(h, h.Variants.First(v => v.Kind == kind), ctx).Files[0];
    }

    [Theory]
    [InlineData(OpenAi)]
    [InlineData("http://localhost:8790/v1")]
    [InlineData("https://127.0.0.1:8790")]
    [InlineData("http://127.4.5.6:8790")]
    [InlineData("http://[::1]:8790/v1")]
    [InlineData("  http://LOCALHOST:8790/v1 ")]
    public void A_loopback_url_on_the_proxy_port_is_rtoks(string value) =>
        Assert.True(ProxyOwner.Ours(value, OpenAi, 8790));

    [Theory]
    [InlineData("https://api.example/localhost:8790/v1")]
    [InlineData("https://api.example/v1?base=http://127.0.0.1:8790")]
    [InlineData("https://localhost:8790@api.example/v1")]
    [InlineData("https://127.0.0.1:8790@api.example")]
    [InlineData("https://localhost.example:8790/v1")]
    [InlineData("http://127.0.0.1:87901/v1")]
    [InlineData("http://127.0.0.1:879/v1")]
    [InlineData("ftp://127.0.0.1:8790/v1")]
    [InlineData("http://[::ffff:127.0.0.1]:8790/v1")]
    [InlineData("localhost:8790")]
    [InlineData("not a url localhost:8790")]
    public void Anything_else_is_foreign(string value) =>
        Assert.False(ProxyOwner.Ours(value, OpenAi, 8790));

    [Fact]
    public void Claude_sets_the_base_url_then_takes_it_back_byte_for_byte()
    {
        var file = File("claude");
        const string seed = """{"theme":"dark"}""";
        var (report, text) = file.Install(seed);
        Assert.Equal(
            $"env.ANTHROPIC_BASE_URL: {Url}\nrevert: remove env.ANTHROPIC_BASE_URL",
            report
        );
        Assert.True(file.Holds(text));
        Assert.Equal((Apply.NoChanges, text), file.Install(text));
        Assert.Equal(("- env.ANTHROPIC_BASE_URL", seed), file.Remove(text, Write));
    }

    [Fact]
    public void Install_takes_over_a_foreign_url_and_remove_leaves_one()
    {
        var file = File("claude");
        const string foreign = """{"env":{"ANTHROPIC_BASE_URL":"https://api.example","A":"1"}}""";
        Assert.False(file.Holds(foreign));
        Assert.Equal((Apply.NoChanges, foreign), file.Remove(foreign, Write));
        var (report, text) = file.Install(foreign);
        Assert.EndsWith(
            "\nrevert: set env.ANTHROPIC_BASE_URL to https://api.example",
            report,
            StringComparison.Ordinal
        );
        Assert.Contains("\"A\":\"1\"", text, StringComparison.Ordinal);
        // Another spelling of the proxy on its port is rtok's too.
        const string localhost = """{"env":{"ANTHROPIC_BASE_URL":"http://localhost:8790"}}""";
        Assert.Equal("{}", file.Remove(localhost, Write).Text);
    }

    [Fact]
    public void A_non_object_env_is_refused()
    {
        var refused = Assert.Throws<InvalidDataException>(() =>
            File("claude").Install("""{"env":"x"}""")
        );
        Assert.Contains("is not an object", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HostKind.Cli)]
    [InlineData(HostKind.Desktop)]
    public void OpenCode_takes_openai_base_url_on_both_apps(HostKind kind)
    {
        var file = File("opencode", kind);
        var (report, text) = file.Install("");
        Assert.Equal($"env.OPENAI_BASE_URL: {OpenAi}\nrevert: remove env.OPENAI_BASE_URL", report);
        Assert.Equal(("- env.OPENAI_BASE_URL", "{}\n"), file.Remove(text, Write));
        const string foreign = """{"env":{"OPENAI_BASE_URL":"https://api.openai.com/v1"}}""";
        Assert.Equal((Apply.NoChanges, foreign), file.Remove(foreign, Write));
    }

    [Fact]
    public void Codex_remembers_the_provider_it_replaces_and_puts_it_back()
    {
        var file = File("codex");
        const string before = "# keep me\nmodel_provider = \"openai\"\nmodel = \"o3\"\n";
        var (report, text) = file.Install(before);
        Assert.Equal(
            $"+ [model_providers.rtok]\nbase_url = \"{OpenAi}\"\nrevert: set model_provider to openai",
            report
        );
        Assert.Contains(
            "model_provider = \"rtok\" # rtok: was \"openai\"\n",
            text,
            StringComparison.Ordinal
        );
        Assert.True(file.Holds(text));
        Assert.Equal((Apply.NoChanges, text), file.Install(text));

        // A new port is rewritten in place and still remembers openai, not rtok.
        var moved = File("codex", settings: new HostSettings(ProxyUrl: "http://127.0.0.1:1"));
        var (again, retargeted) = moved.Install(text);
        Assert.EndsWith("revert: set model_provider to openai", again, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:1/v1", retargeted, StringComparison.Ordinal);
        Assert.Contains("# rtok: was \"openai\"", retargeted, StringComparison.Ordinal);

        Assert.Equal(
            ("- [model_providers.rtok]\nrestore model_provider = \"openai\"", before),
            file.Remove(text, Write)
        );
    }

    [Fact]
    public void Codex_without_a_provider_adds_one_and_removes_it()
    {
        var file = File("codex");
        const string before = "# keep me\nmodel = \"o3\"\n\n[tools]\nweb = true\n";
        var (report, text) = file.Install(before);
        Assert.EndsWith("revert: remove [model_providers.rtok]", report, StringComparison.Ordinal);
        Assert.StartsWith(
            "# keep me\nmodel = \"o3\"\nmodel_provider = \"rtok\"\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Equal(("- [model_providers.rtok]", before), file.Remove(text, Write));
        // No root key yet: the provider goes before the first table, not into it.
        var (_, tables) = file.Install("[tools]\nweb = true\n");
        Assert.StartsWith("model_provider = \"rtok\"\n[tools]\n", tables, StringComparison.Ordinal);
    }

    [Fact]
    public void Codex_asks_before_taking_a_provider_table_the_user_edited()
    {
        var file = File("codex");
        var (_, text) = file.Install("model_provider = \"openai\"\n");
        var edited = text.Replace(OpenAi, "http://example.test/v1", StringComparison.Ordinal);
        Assert.NotEqual(Apply.NoChanges, file.Install(edited).Report, StringComparer.Ordinal);
        var (left, same) = file.Remove(edited, Write);
        Assert.StartsWith("leave model_providers.rtok in ", left, StringComparison.Ordinal);
        Assert.Equal(edited, same);
        var (asked, _) = file.Remove(edited, new Apply { DryRun = true });
        Assert.StartsWith("? model_providers.rtok in ", asked, StringComparison.Ordinal);
        Assert.Equal(
            "model_provider = \"openai\"\n",
            file.Remove(edited, new Apply { Yes = true }).Text
        );
    }

    [Fact]
    public void Aider_appends_rewrites_and_keeps_comments_and_line_breaks()
    {
        var file = File("aider");
        const string seed = "# openai-api-base: https://old.example\r\nmodel: x\r\n";
        var (report, text) = file.Install(seed);
        Assert.Equal($"+ openai-api-base: {OpenAi}\nrevert: remove openai-api-base", report);
        Assert.Equal(seed + $"openai-api-base: {OpenAi}\r\n", text);
        Assert.Equal((Apply.NoChanges, text), file.Install(text));
        Assert.Equal(("- openai-api-base", seed), file.Remove(text, Write));

        const string quoted = "  openai-api-base: \"https://old.example\" # mine\n";
        var (taken, rewritten) = file.Install(quoted);
        Assert.EndsWith(
            "revert: set openai-api-base to https://old.example",
            taken,
            StringComparison.Ordinal
        );
        Assert.Equal($"  openai-api-base: {OpenAi} # mine\n", rewritten);
    }

    [Fact]
    public void Aider_remove_keeps_a_foreign_base_url_and_lookalike_keys()
    {
        var file = File("aider");
        const string foreign =
            "openai-api-base: https://foreign.example/v1\nopenai-api-base-x: http://localhost:8790/v1\n";
        Assert.False(file.Holds(foreign));
        Assert.Equal((Apply.NoChanges, foreign), file.Remove(foreign, Write));
        Assert.Equal(
            ("- openai-api-base", "model: x\n"),
            file.Remove("model: x\nopenai-api-base: 'http://[::1]:8790/v1'\n", Write)
        );
    }

    [Fact]
    public void Flag_hosts_write_only_with_proxy_and_update_keeps_it()
    {
        var home = new FakeHome();
        var aider = AgentHosts.Find("aider")!;
        home.Install(aider.Variants[0]);
        home.PutRtokOnPath();
        var conf = home.At(".aider.conf.yml");
        var plain = home.Run().Run(new HostRequest(["aider"], McpMode.Install));
        Assert.Contains("✗ proxy   not installed (--proxy)", plain, StringComparison.Ordinal);
        Assert.Null(home.Read(conf));

        var on = new HostSettings(Backup: false, Proxy: true);
        var text = home.Run(on).Run(new HostRequest(["aider"], McpMode.Install));
        Assert.Contains($"+ openai-api-base: {OpenAi}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("warning", text, StringComparison.Ordinal);

        // update without --proxy keeps a base URL that reads back as rtok's.
        var update = home.Run(new HostSettings(Backup: false))
            .Run(new HostRequest(["aider"], McpMode.Update));
        Assert.Contains(" — already current", update, StringComparison.Ordinal);
        // On another port the old URL is not rtok's any more: install --proxy moves it.
        var port = new HostSettings(Backup: false, ProxyUrl: "http://127.0.0.1:9001");
        var stale = home.Run(port).Run(new HostRequest(["aider"], McpMode.Update));
        Assert.Contains(" — not installed", stale, StringComparison.Ordinal);
        home.Run(port with { Proxy = true }).Run(new HostRequest(["aider"], McpMode.Install));
        Assert.Equal("openai-api-base: http://127.0.0.1:9001/v1\n", home.Read(conf));

        var gone = home.Run(port).Run(new HostRequest(["aider"], McpMode.Remove));
        Assert.Contains("- openai-api-base\n", gone, StringComparison.Ordinal);
        Assert.Equal("", home.Read(conf));
    }

    [Theory]
    [InlineData("http://127.0.0.1:8788", Url)]
    [InlineData("http://localhost:8787/w/claude?x=1", Url + "/w/claude?x=1")]
    [InlineData("http://[::1]:8788/", Url + "/")]
    [InlineData("https://api.example/v1/8788", null)]
    [InlineData("https://api.example:8788/v1", null)]
    [InlineData("https://127.0.0.1:8788@api.example/v1", null)]
    [InlineData("http://127.0.0.1:18788", null)]
    [InlineData(Url, null)]
    [InlineData("not a url 8788", null)]
    public void Replace_moves_only_a_legacy_loopback_proxy(string legacy, string? next)
    {
        var home = new FakeHome();
        var settings = home.At(".claude/settings.json");
        var seed = $$$"""{"env":{"ANTHROPIC_BASE_URL":"{{{legacy}}}"}}""";
        home.Write(settings, seed);
        var ctx = home.Context(new HostSettings(Yes: true, Backup: false));
        var report = ClaudeReplace.Run(ctx, new Apply { Yes = true }, settings);
        if (next is null)
        {
            Assert.Equal("remaining rtok hooks: 0", report);
            Assert.Equal(seed, home.Read(settings));
            return;
        }
        Assert.Equal($"ANTHROPIC_BASE_URL {legacy} -> {next}\nremaining rtok hooks: 0", report);
        Assert.Equal($$$"""{"env":{"ANTHROPIC_BASE_URL":"{{{next}}}"}}""", home.Read(settings));
        Assert.Equal(
            "remaining rtok hooks: 0",
            ClaudeReplace.Run(ctx, new Apply { Yes = true }, settings)
        );
    }
}
