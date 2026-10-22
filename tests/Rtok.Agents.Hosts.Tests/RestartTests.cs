using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Restart;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>A running desktop app is closed around the write and reopened after (rtok <c>restart.rs</c> tests).</summary>
public sealed class RestartTests
{
    private readonly FakeHome _home = new(McpOs.MacOS);
    private readonly List<string> _warnings = [];

    public RestartTests() => _home.PutRtokOnPath();

    [Fact]
    public void A_running_app_whose_config_changes_is_closed_written_then_reopened()
    {
        var apps = Running("Windsurf");

        Install(apps, ["windsurf"]);

        Assert.Equal(["quit:Windsurf", "write", "open:Windsurf"], Squash(apps.Calls));
        Assert.Empty(_warnings);
    }

    [Fact]
    public void A_running_app_whose_config_stays_is_left_alone()
    {
        PutApp("Windsurf");
        Install(new FakeApps(), ["windsurf"]);
        var apps = Running("Windsurf");

        var text = Install(apps, ["windsurf"]);

        Assert.Contains("already installed", text, StringComparison.Ordinal);
        Assert.Empty(apps.Calls);
    }

    [Fact]
    public void An_app_that_does_not_run_is_only_written()
    {
        PutApp("Windsurf");
        var apps = new FakeApps();

        Install(apps, ["windsurf"]);

        Assert.Equal(["write"], Squash(apps.Calls));
    }

    [Fact]
    public void A_dry_run_says_it_would_close_and_closes_nothing()
    {
        var apps = Running("Windsurf");

        var text = Install(apps, ["windsurf"], dryRun: true);

        Assert.StartsWith(
            "windsurf: would close/reopen Windsurf\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Empty(apps.Calls);
    }

    [Fact]
    public void No_restart_only_writes()
    {
        var apps = Running("Windsurf");

        Install(apps, ["windsurf"], noRestart: true);

        Assert.Equal(["write"], Squash(apps.Calls));
        Assert.Empty(_warnings);
    }

    [Fact]
    public void A_quit_that_fails_warns_and_still_writes()
    {
        var apps = Running("Windsurf", new FakeApps { FailQuit = true });

        Install(apps, ["windsurf"]);

        Assert.Equal(["quit:Windsurf", "write"], Squash(apps.Calls));
        Assert.Equal(
            ["warning: could not quit Windsurf: boom; restart it manually to load the new config"],
            _warnings
        );
    }

    [Fact]
    public void An_app_that_never_quits_is_written_but_not_reopened()
    {
        var apps = Running("Windsurf", new FakeApps { StuckQuit = true });

        Install(apps, ["windsurf"]);

        Assert.Equal(["quit:Windsurf", "write"], Squash(apps.Calls));
        Assert.Equal(
            [
                "warning: Windsurf did not quit within 0.3 s; restart it manually to load the new config",
            ],
            _warnings
        );
    }

    [Fact]
    public void A_reopen_that_fails_warns_instead_of_failing()
    {
        var apps = Running("Windsurf", new FakeApps { FailOpen = true });

        Install(apps, ["windsurf"]);

        Assert.Equal(["quit:Windsurf", "write", "open:Windsurf"], Squash(apps.Calls));
        Assert.Equal(
            [
                "warning: could not reopen Windsurf: open -a Windsurf: exit status: 1; open it manually",
            ],
            _warnings
        );
    }

    [Fact]
    public void A_write_that_fails_still_reopens_then_throws()
    {
        var apps = Running("Windsurf");

        var e = Assert.Throws<IOException>(() => Install(apps, ["windsurf"], failWrite: true));

        Assert.Equal("disk full", e.Message);
        Assert.Equal(["quit:Windsurf", "write", "open:Windsurf"], apps.Calls);
    }

    [Theory]
    [InlineData("windsurf")]
    [InlineData("cursor")]
    [InlineData("devin")]
    public void A_failing_dry_run_on_any_host_closes_nothing_and_writes_nothing(string broken)
    {
        var apps = Running("Windsurf", Running("Cursor", Running("Devin")));
        var host = AgentHosts.Resolve([broken])[0];
        var file = new HostRun(Context(new FakeApps()))
            .Rows([host])
            .First(r => r.Present && r.Config.Count > 0)
            .Config[0];
        _home.Fs.Directory.CreateDirectory(_home.Fs.Path.GetDirectoryName(file)!);
        _home.Fs.File.WriteAllBytes(file, [0xFF, 0xFE, 0x00]);

        var e = Assert.Throws<InvalidDataException>(() =>
            Install(apps, ["windsurf", "cursor", "devin"])
        );

        Assert.Contains(file, e.Message, StringComparison.Ordinal);
        Assert.Empty(apps.Calls);
    }

    [Fact]
    public void Two_running_apps_are_both_closed_then_both_reopened()
    {
        var apps = Running("Windsurf", Running("Devin"));

        Install(apps, ["windsurf", "devin"]);

        Assert.Equal(
            ["quit:Windsurf", "quit:Devin", "write", "open:Windsurf", "open:Devin"],
            Squash(apps.Calls)
        );
    }

    [Fact]
    public void A_running_cli_is_named_and_never_closed()
    {
        _home.Install(AgentHosts.Resolve(["claude"])[0].Variants[0]);
        var apps = new FakeApps();
        apps.Running.Add("claude");

        var text = Install(apps, ["claude"]);

        Assert.StartsWith(
            "claude: restart your claude session to load the new config\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Equal(["write"], Squash(apps.Calls));
    }

    [Fact]
    public void An_app_that_is_not_installed_is_never_asked()
    {
        _home.Install(AgentHosts.Resolve(["claude"])[0].Variants[0]);
        var apps = new FakeApps();
        apps.Running.Add("Claude");

        Install(apps, ["claude"]);

        Assert.Empty(apps.Asked);
        Assert.Equal(["write"], Squash(apps.Calls));
    }

    /// <summary>The name each desktop app runs under on macOS, from its first bundle (rtok <c>expected_desktop_name</c>).</summary>
    [Fact]
    public void Every_desktop_app_of_every_host_resolves_its_known_name()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["antigravity/Antigravity"] = "Antigravity",
            ["claude/Claude Desktop"] = "Claude",
            ["copilot/GitHub Copilot"] = "GitHub Copilot",
            ["cursor/Cursor"] = "Cursor",
            ["devin/Devin"] = "Devin",
            ["cline/Cline for VS Code"] = "Visual Studio Code",
            ["kilo/Kilo Code for VS Code"] = "Visual Studio Code",
            ["roo/Roo Code"] = "Visual Studio Code",
            ["continue/Continue"] = "Visual Studio Code",
            ["vscode/VS Code"] = "Visual Studio Code",
            ["vscode/VS Code - Insiders"] = "Visual Studio Code - Insiders",
            ["kimi/Kimi Code Desktop"] = "Kimi Code",
            ["kiro/Kiro IDE"] = "Kiro",
            ["mimo/MiMo Desktop"] = "Xiaomi MiMo AI",
            ["opencode/OpenCode Desktop"] = "OpenCode",
            ["goose/Goose"] = "Goose",
            ["warp/Warp"] = "Warp",
            ["windsurf/Windsurf"] = "Windsurf",
            ["codebuddy/WorkBuddy"] = "WorkBuddy",
            ["zcode/ZCode"] = "ZCode",
            ["zed/Zed"] = "Zed",
        };
        var seen = new List<string>();
        foreach (var host in AgentHosts.All)
        {
            foreach (var v in host.Variants.Where(v => v.Kind == HostKind.Desktop))
            {
                var key = $"{host.Id}/{v.Name}";
                Assert.True(
                    expected.TryGetValue(key, out var name),
                    $"desktop app {key} has no expected-name row: add one"
                );
                Assert.NotEmpty(v.Apps);
                Assert.Equal(name, DesktopRestart.AppName(v.Apps[0], v.Name));
                seen.Add(key);
            }
        }
        Assert.Equal(
            expected.Keys.Order(StringComparer.Ordinal),
            seen.Order(StringComparer.Ordinal)
        );
    }

    /// <summary>Installs rtok into <paramref name="hosts"/> with <paramref name="apps"/> as the process table; the disk logs each write in its calls.</summary>
    private string Install(
        FakeApps apps,
        string[] hosts,
        bool dryRun = false,
        bool noRestart = false,
        bool failWrite = false
    ) =>
        new HostRun(Context(apps, dryRun, failWrite)).Run(
            new HostRequest(hosts, McpMode.Install, NoRestart: noRestart)
        );

    private HostContext Context(FakeApps apps, bool dryRun = false, bool failWrite = false) =>
        new(
            _home.Env,
            new McpDisk(
                _home.Fs,
                (apply, path, body, report) =>
                {
                    if (!apply.Writes(report))
                    {
                        return;
                    }
                    apps.Calls.Add("write");
                    if (failWrite)
                    {
                        throw new IOException("disk full");
                    }
                    _home.Write(path, body);
                }
            ),
            (_, _, _) => ("1.0.0 (fake)", ""),
            new RtokBinary(_home.Self),
            new HostSettings(DryRun: dryRun, Backup: false, Warn: _warnings.Add),
            apps: apps,
            quitWait: TimeSpan.FromMilliseconds(300)
        );

    /// <summary>The app <paramref name="name"/> installed under <c>/Applications</c> and running in <paramref name="apps"/>.</summary>
    private FakeApps Running(string name, FakeApps? apps = null)
    {
        apps ??= new FakeApps();
        PutApp(name);
        apps.Running.Add(name);
        return apps;
    }

    private void PutApp(string name) =>
        _home.Fs.Directory.CreateDirectory(_home.At($"Applications/{name}.app"));

    /// <summary>The calls with each run of writes as one <c>write</c>: one app can have several files.</summary>
    private static List<string> Squash(List<string> calls) =>
        [.. calls.Where((c, i) => !IsWrite(c) || i == 0 || !IsWrite(calls[i - 1]))];

    private static bool IsWrite(string call) =>
        string.Equals(call, "write", StringComparison.Ordinal);
}
