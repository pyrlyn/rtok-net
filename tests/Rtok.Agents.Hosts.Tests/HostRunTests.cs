using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>What <c>agents install|update|uninstall</c> print and refuse, on a fake home.</summary>
public sealed class HostRunTests
{
    private static readonly HostSettings Plain = new(DryRun: false, Yes: false, Backup: false);

    private static HostRequest Request(string hosts, McpMode mode, bool cli = false) =>
        new(AgentHosts.Parse(hosts), mode, Cli: cli, Desktop: false, All: false);

    private static FakeHome With(McpOs os = McpOs.Linux, params string[] hosts)
    {
        var home = new FakeHome(os);
        foreach (var v in hosts.SelectMany(h => AgentHosts.Find(h)!.Variants))
        {
            home.Install(v);
        }
        return home;
    }

    [Fact]
    public void A_dry_run_reports_the_edit_and_writes_nothing()
    {
        var home = With(McpOs.Linux, "claude");
        var text = home.Run(Plain with { DryRun = true }).Run(Request("claude", McpMode.Install));
        Assert.Contains(
            "CLI: Claude Code — dry run, nothing written\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("mcpServers.rtok", text, StringComparison.Ordinal);
        Assert.Null(home.Read(home.At(".claude.json")));
    }

    [Fact]
    public void A_host_with_no_app_here_is_refused_before_anything_is_written()
    {
        var home = With(McpOs.Linux, "claude");
        var e = Assert.Throws<AgentsException>(() =>
            home.Run().Run(Request("claude,goose", McpMode.Install))
        );
        Assert.Equal("goose is not installed", e.Message);
        Assert.Null(home.Read(home.At(".claude.json")));
    }

    [Fact]
    public void Unknown_hosts_are_refused()
    {
        Assert.Equal(
            "unknown host: nope",
            Assert.Throws<AgentsException>(() => AgentHosts.Resolve(["nope"])).Message
        );
        Assert.Equal(
            "unknown host: ,",
            Assert.Throws<AgentsException>(() => AgentHosts.Parse(",")).Message
        );
    }

    [Fact]
    public void Update_skips_an_app_without_rtok_and_says_how_to_install()
    {
        var home = With(McpOs.Linux, "codex");
        var text = home.Run().Run(Request("codex", McpMode.Update));
        Assert.Equal(
            HostRun.NotOnPath
                + "\nCLI: Codex — not installed\n  skip    nothing of rtok here; run `rtok agents install codex`\n",
            text
        );
    }

    [Fact]
    public void Cli_only_refuses_a_host_with_no_cli_here()
    {
        var home = With(McpOs.MacOS, "warp", "claude");
        var e = Assert.Throws<AgentsException>(() =>
            home.Run().Run(Request("claude,warp", McpMode.Install, cli: true))
        );
        Assert.Equal("warp is not installed", e.Message);
        var text = home.Run().Run(Request("claude", McpMode.Install, cli: true));
        Assert.DoesNotContain("Desktop: Claude Desktop", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_rtok_on_path_install_warns_and_entries_name_rtok()
    {
        var home = With(McpOs.Linux, "claude");
        var text = home.Run().Run(Request("claude", McpMode.Install));
        Assert.StartsWith(HostRun.NotOnPath + "\n", text, StringComparison.Ordinal);
        Assert.Contains(
            "\"command\": \"rtok\"",
            home.Read(home.At(".claude.json")),
            StringComparison.Ordinal
        );

        home.PutRtokOnPath();
        Assert.DoesNotContain(
            "warning",
            home.Run().Run(Request("claude", McpMode.Install)),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void On_windows_without_rtok_on_path_entries_name_the_running_binary()
    {
        var home = With(McpOs.Windows, "goose");
        var text = home.Run().Run(Request("goose", McpMode.Install));
        Assert.StartsWith(HostRun.NotOnPath + "\n", text, StringComparison.Ordinal);
        Assert.Contains(
            "    cmd: \"C:\\\\Program Files\\\\rtok\\\\rtok.exe\"\n",
            home.Read(home.At(".config/goose/config.yaml")),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "  ✓ mcp     installed\n",
            home.Run().List([AgentHosts.Find("goose")!]),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Remove_leaves_an_entry_the_user_changed_unless_yes()
    {
        var home = With(McpOs.Linux, "goose");
        home.Run().Run(Request("goose", McpMode.Install));
        var path = home.At(".config/goose/config.yaml");
        var edited = home.Read(path)!
            .Replace("enabled: true", "enabled: false", StringComparison.Ordinal);
        home.Write(path, edited);

        var left = home.Run().Run(Request("goose", McpMode.Remove));
        Assert.Contains("leave extensions.rtok in " + path, left, StringComparison.Ordinal);
        Assert.Equal(edited, home.Read(path));

        home.Run(Plain with { Yes = true }).Run(Request("goose", McpMode.Remove));
        Assert.DoesNotContain("rtok", home.Read(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Install_without_mcp_writes_nothing()
    {
        var home = With(McpOs.Linux, "claude");
        var text = home.Run(Plain with { Mcp = false }).Run(Request("claude", McpMode.Install));
        Assert.Contains("CLI: Claude Code — already installed\n", text, StringComparison.Ordinal);
        Assert.Null(home.Read(home.At(".claude.json")));
    }

    [Theory]
    [InlineData("claude", ".claude.json", "{\"mcpServers\": ")]
    [InlineData("goose", ".config/goose/config.yaml", "extensions: [\n")]
    [InlineData("reasonix", ".reasonix/config.toml", "[[plugins]\n")]
    public void An_unparseable_host_file_is_refused_by_name_and_left(
        string host,
        string file,
        string text
    )
    {
        var home = With(McpOs.Linux, host);
        var path = home.At(file);
        home.Write(path, text);
        var e = Assert.Throws<InvalidDataException>(() =>
            home.Run().Run(Request(host, McpMode.Install))
        );
        Assert.Contains(path, e.Message, StringComparison.Ordinal);
        Assert.Equal(text, home.Read(path));
    }
}
