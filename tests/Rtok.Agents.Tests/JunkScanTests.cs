using Rtok.Agents.Junk;
using Rtok.Config;

namespace Rtok.Agents.Tests;

public sealed class JunkScanTests : IDisposable
{
    private readonly JunkHome home = new();

    public void Dispose() => home.Dispose();

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    private static JunkAgent Agent(JunkReport report, string name) =>
        report.Agents.Single(a => string.Equals(a.Name, name, StringComparison.Ordinal));

    private static JunkItem Item(JunkAgent agent, string path) =>
        agent.Items.Single(i => string.Equals(i.Path, path, StringComparison.Ordinal));

    [Theory]
    [InlineData("cache", "safe")]
    [InlineData("temp", "safe")]
    [InlineData("crash-dumps", "safe")]
    [InlineData("log", "safe")]
    [InlineData("archive", "safe")]
    [InlineData("logs", "review")]
    [InlineData("deps", "review")]
    [InlineData("stale-worktrees", "review")]
    [InlineData("sessions", "explicit")]
    [InlineData("anything", "never")]
    public void Every_kind_has_the_class_of_the_catalog(string kind, string cls) =>
        Assert.Equal(cls, JunkScan.ClassOf(kind));

    [Fact]
    public void A_section_22_cache_is_safe_and_counted()
    {
        home.File(".claude/paste-cache/a");

        var claude = Agent(home.Report(), "claude");

        var item = Item(claude, home[".claude/paste-cache"]);
        Assert.Equal(
            ("cache", "safe", JunkEvidence.Section22),
            (item.Kind, item.Class, item.Evidence)
        );
        Assert.True(item.Counted);
        Assert.Contains(claude.Folders, f => Same(f.Path, home[".claude"]) && f.Role is "data");
        Assert.Equal(item.Bytes, claude.FreedDefaultBytes);
    }

    [Fact]
    public void A_tagged_cache_goes_only_when_idle()
    {
        var idle = home.Tagged(".claude/plugins/old");
        var fresh = home.Tagged(".claude/plugins/new", home.Now);

        var claude = Agent(home.Report(), "claude");

        Assert.Equal(JunkEvidence.Tag, Item(claude, idle).Evidence);
        Assert.True(Item(claude, idle).Counted);
        Assert.Equal("modified within the idle window", Item(claude, fresh).Kept);
    }

    [Fact]
    public void A_find_without_evidence_is_listed_but_never_counted()
    {
        var lockfile = home.File(".claude/todos/x.lock");

        var item = Item(Agent(home.Report(), "claude"), lockfile);

        Assert.Equal(("locks", JunkEvidence.None), (item.Kind, item.Evidence));
        Assert.Equal(JunkEvidence.NotDocumented, item.Kept);
    }

    [Fact]
    public void An_extra_path_is_cache_with_the_users_evidence()
    {
        home.File("mine/a");
        home.Config.Agents.Junk.Extra.Add(
            new JunkExtra
            {
                Host = "claude",
                Kind = "cache",
                Path = home["mine"],
            }
        );
        home.File(".claude/settings.json", body: "{}");

        var item = Item(Agent(home.Report(), "claude"), home["mine"]);

        Assert.Equal(("cache", JunkEvidence.Extra), (item.Kind, item.Evidence));
        Assert.True(item.Counted);
    }

    [Fact]
    public void The_config_dir_variable_moves_the_claude_folders()
    {
        home.Set("CLAUDE_CONFIG_DIR", home["alt"]);
        home.File("alt/paste-cache/a");
        home.File(".claude/paste-cache/a");

        var claude = Agent(home.Report(), "claude");

        Assert.Contains(claude.Items, i => Same(i.Path, home["alt/paste-cache"]));
        Assert.DoesNotContain(claude.Items, i => Same(i.Path, home[".claude/paste-cache"]));
    }

    [Fact]
    public void The_codex_and_xdg_variables_move_their_roots()
    {
        var roots = new JunkRoots(
            home.Home,
            v =>
                v switch
                {
                    "CODEX_HOME" => home["c"],
                    "XDG_CACHE_HOME" => home["x"],
                    _ => null,
                },
            "linux"
        );

        Assert.Equal(home["c/log"], roots.Resolve("{codex}/log"));
        Assert.Equal(home["x/opencode"], roots.Resolve("{xdg_cache}/opencode"));
        Assert.Equal(home["x/rtok"], roots.Resolve("{rtok_cache}"));
    }

    [Fact]
    public void A_package_lockfile_is_never_a_lock()
    {
        var yarn = home.File(".claude/todos/yarn.lock");
        var cargo = home.File(".claude/todos/Cargo.lock");

        var claude = Agent(home.Report(), "claude");

        Assert.DoesNotContain(claude.Items, i => Same(i.Path, yarn) || Same(i.Path, cargo));
    }

    [Fact]
    public void A_relative_variable_is_ignored()
    {
        var roots = new JunkRoots(
            home.Home,
            v => v is "CLAUDE_CONFIG_DIR" or "XDG_CACHE_HOME" ? "rel" : null,
            "linux"
        );

        Assert.Equal(home[".claude/debug"], roots.Resolve("{claude}/debug"));
        Assert.Equal(home[".cache/rtok"], roots.Resolve("{rtok_cache}"));
        Assert.Equal("{nope}/x", roots.Resolve("{nope}/x"));
    }

    [Fact]
    public void A_folder_two_hosts_share_counts_once()
    {
        home.Hosts.Clear();
        home.Hosts.Add(new("grok", true, [home[".shared/mcp.json"]], []));
        home.Hosts.Add(new("kimi", true, [home[".shared/mcp.json"]], []));
        home.File(".shared/mcp.json", body: "{}");
        var cache = home.Tagged(".shared/cache");

        var report = home.Report();

        var grok = Agent(report, "grok");
        Assert.Equal(["kimi"], grok.Folders.Single().SharedWith);
        Assert.Equal(Item(grok, cache).Bytes, report.FreedDefaultBytes);
        Assert.Equal(grok.TotalBytes, report.TotalBytes);
        var planned = Assert.Single(home.Clear(new() { Kinds = ["cache"] }, false).Items);
        Assert.Equal(cache, planned.Path);
    }

    [Fact]
    public void A_session_in_use_stays_and_an_old_one_is_explicit()
    {
        var old = home.File(".claude/projects/p/0b8e6c1e-1111-4a5b-9c7d-0123456789ab.jsonl");
        var open = home.File(
            ".claude/projects/p/1b8e6c1e-1111-4a5b-9c7d-0123456789ab.jsonl",
            home.Now
        );
        var recent = home.File(
            ".claude/projects/p/2b8e6c1e-1111-4a5b-9c7d-0123456789ab.jsonl",
            home.Now - TimeSpan.FromDays(5)
        );

        var claude = Agent(home.Report(), "claude");

        Assert.Equal(("sessions", "explicit"), (Item(claude, old).Kind, Item(claude, old).Class));
        Assert.True(Item(claude, old).Counted);
        Assert.StartsWith("skipped: in use", Item(claude, open).Kept, StringComparison.Ordinal);
        Assert.Equal("touched within the 30-day threshold", Item(claude, recent).Kept);
        Assert.Contains(claude.Notes, n => n.Contains("30", StringComparison.Ordinal));
    }

    [Fact]
    public void A_host_that_is_not_installed_shows_only_with_all()
    {
        home.Hosts.Add(new("codex", false, [], ["codex"]));
        home.File(".codex/log/a.log");

        Assert.DoesNotContain(home.Report().Agents, a => a.Name is "codex");
        var all = JunkScan.Build(
            new JunkOptions
            {
                Config = home.Config,
                RtokHome = home[".rtok"],
                Roots = home.Roots,
                Hosts = home.Hosts,
                Context = home.Context,
                All = true,
            }
        );
        Assert.False(Agent(all, "codex").Installed);
    }
}
