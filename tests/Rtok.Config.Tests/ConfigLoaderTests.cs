using Pyrlyn.Config.Layered;

namespace Rtok.Config.Tests;

public sealed class ConfigLoaderTests
{
    [Fact]
    public void With_nothing_set_every_value_is_the_default()
    {
        using var box = new Sandbox();
        var config = box.Load();

        Assert.Empty(config.Issues);
        Assert.All(config.Entries, e => Assert.Equal(ConfigSourceKind.Defaults, e.Source.Kind));
        Assert.Equal((ushort)8790, config.Config.Proxy.Port);
    }

    [Fact]
    public void Each_layer_overrides_the_one_before_and_is_named_as_the_source()
    {
        using var box = new Sandbox();
        box.Write(
            "user/.rtok/config.toml",
            "[proxy]\nport = 1001\nmode = \"compress\"\n[web]\nport = 2001\n[log]\nlevel = \"warn\"\nfiles = 3\n"
        );
        box.Write(
            "project/.rtok.toml",
            "[proxy]\nport = 1002\n[web]\nport = 2002\n[log]\nlevel = \"error\"\n"
        );
        box.Write("project/.env", "RTOK_WEB_PORT=2003\nRTOK_LOG_LEVEL=debug\nOTHER=1\n");
        box.Env["RTOK_LOG_LEVEL"] = "info";
        box.Env["RTOK_TUI_TICK_SECS"] = "7";
        box.Flags.Add(new ConfigFlag("tui.tick_secs", 9L, "--tick"));

        var config = box.Load();

        Assert.Empty(config.Issues);
        Assert.Equal("compress", config.Config.Proxy.Mode);
        Assert.Equal((ushort)1002, config.Config.Proxy.Port);
        Assert.Equal((ushort)2003, config.Config.Web.Port);
        Assert.Equal("info", config.Config.Log.Level);
        Assert.Equal(9u, config.Config.Tui.TickSecs);
        Assert.Equal(3u, config.Config.Log.Files);
        Assert.Equal("user", Source(config, "proxy.mode").Layer);
        Assert.Equal(
            ("project", 2),
            (Source(config, "proxy.port").Layer, Source(config, "proxy.port").Line)
        );
        Assert.Equal(
            (".env", "RTOK_WEB_PORT"),
            (Source(config, "web.port").Layer, Source(config, "web.port").Name)
        );
        Assert.Equal(ConfigSourceKind.Environment, Source(config, "log.level").Kind);
        Assert.Equal("--tick", Source(config, "tui.tick_secs").Name);
    }

    [Fact]
    public void The_nearest_env_file_wins_over_the_one_in_the_rtok_home()
    {
        using var box = new Sandbox();
        box.Write("user/.rtok/.env", "RTOK_WEB_PORT=1\nRTOK_PROXY_PORT=2\n");
        box.Write("project/.env", "RTOK_WEB_PORT=3\n");

        var config = box.Load();

        Assert.Equal((ushort)3, config.Config.Web.Port);
        Assert.Equal((ushort)2, config.Config.Proxy.Port);
    }

    [Fact]
    public void The_old_upstream_variables_still_set_the_proxy_upstreams()
    {
        using var box = new Sandbox();
        box.Env["RTOK_UPSTREAM"] = "http://a.test";
        box.Env["RTOK_OPENAI_UPSTREAM"] = "http://b.test";

        var config = box.Load().Config;

        Assert.Equal(
            ("http://a.test", "http://b.test"),
            (config.Proxy.Upstream, config.Proxy.OpenaiUpstream)
        );
    }

    [Fact]
    public void A_legacy_key_sets_its_new_key_with_a_note_until_the_new_key_is_set()
    {
        using var box = new Sandbox();
        box.Write(
            "user/.rtok/config.toml",
            "[core]\nlog_level = \"warn\"\ninject_budget_tokens = 99\n[dashboard]\nport = 4000\nhost = \"127.0.0.1\"\n[plugins.inject]\nbudget_tokens = 500\n"
        );

        var config = box.Load();

        Assert.Empty(config.Issues);
        Assert.Equal("warn", config.Config.Log.Level);
        Assert.Equal(500u, config.Config.Plugins.Inject.BudgetTokens);
        Assert.Equal((ushort)4000, config.Config.Web.Port);
        Assert.Equal(
            ["core.log_level is now log.level (using warn)", "[dashboard] is now [web] (using it)"],
            config.Notes
        );
        Assert.False(config.TryGet("core.log_level", out _));
        Assert.Null(config.Find("dashboard"));
    }

    [Fact]
    public void A_tilde_in_a_path_key_expands_to_the_rtok_home_or_the_user_home()
    {
        using var box = new Sandbox();
        box.Write(
            "user/.rtok/config.toml",
            "[core]\narchive_dir = \"~/elsewhere\"\n[plugins.read]\nallow_paths = [\"~/src\", \"/abs\"]\n"
        );

        var config = box.Load().Config;

        Assert.Equal(Path.Join(box.Home, "rtok.db"), config.Core.DbPath);
        Assert.Equal(Path.Join(box.User, "elsewhere"), config.Core.ArchiveDir);
        Assert.Equal([Path.Join(box.User, "src"), "/abs"], config.Plugins.Read.AllowPaths);
    }

    [Fact]
    public void An_untouched_session_folder_follows_the_agents_own_variable_when_it_is_absolute()
    {
        using var box = new Sandbox();
        box.Env["XDG_DATA_HOME"] = "/data";
        box.Env["COPILOT_HOME"] = "relative";
        box.Env["PI_CODING_AGENT_DIR"] = "/pi";
        box.Write("user/.rtok/config.toml", "[agents.usage.dirs]\nkilo = [\"/mine\"]\n");

        var dirs = box.Load().Config.Agents.Usage.Dirs;

        Assert.Equal([Path.Join("/data", "opencode")], dirs.Opencode);
        Assert.Equal(["/mine"], dirs.Kilo);
        Assert.Equal([Path.Join(box.User, ".copilot", "session-state")], dirs.Copilot);
        Assert.Equal([Path.Join("/pi", "sessions")], dirs.Pi);
    }

    [Fact]
    public void A_bad_value_is_an_issue_and_its_key_keeps_the_default()
    {
        using var box = new Sandbox();
        box.Write("user/.rtok/config.toml", "[proxy]\nport = \"high\"\n[log]\nfiles = 3\n");
        box.Env["RTOK_TUI_TICK_SECS"] = "0";

        var config = box.Load();

        Assert.Equal(
            [
                $"{box.UserFile}:2: proxy.port: expected number",
                "env RTOK_TUI_TICK_SECS: tui.tick_secs must be ≥ 1",
            ],
            config.Issues.Select(i => i.ToString()),
            StringComparer.Ordinal
        );
        Assert.Equal((ushort)8790, config.Config.Proxy.Port);
        Assert.Equal(3u, config.Config.Log.Files);
    }

    [Fact]
    public void A_number_past_its_type_is_out_of_range()
    {
        using var box = new Sandbox();
        box.Write("user/.rtok/config.toml", "[log]\nlines = 99999999999\n");

        var config = box.Load();

        Assert.Equal(
            [$"{box.UserFile}:2: log.lines: out of range"],
            config.Issues.Select(i => i.ToString()),
            StringComparer.Ordinal
        );
        Assert.Equal(200u, config.Config.Log.Lines);
    }

    [Fact]
    public void A_file_that_does_not_parse_is_an_issue_at_its_line()
    {
        using var box = new Sandbox();
        box.Write("user/.rtok/config.toml", "[proxy]\nport = = 1\n");

        var issue = Assert.Single(box.Load().Issues);

        Assert.StartsWith($"{box.UserFile}:2: ", issue.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Model_prices_merge_with_the_shipped_rows()
    {
        using var box = new Sandbox();
        box.Write(
            "user/.rtok/config.toml",
            "[stats.prices.\"my-model\"]\ninput = 1\noutput = 2.5\n"
        );

        var config = box.Load();

        Assert.Empty(config.Issues);
        Assert.Equal(2.5, config.Config.Stats.Prices["my-model"].Output);
        Assert.Equal(1.0, config.Config.Stats.Prices["my-model"].Input);
        Assert.True(config.Config.Stats.Prices.ContainsKey("gpt-5"));
    }

    [Fact]
    public void Validate_of_one_file_reads_neither_the_project_nor_the_environment()
    {
        using var box = new Sandbox();
        var file = box.Write("other.toml", "[web]\nport = 7\n");
        box.Write("project/.rtok.toml", "[web]\nport = 8\n");
        box.Env["RTOK_PROXY_PORT"] = "9";

        var config = ConfigLoader.LoadFile(file, box.Options);

        Assert.Equal((ushort)7, config.Config.Web.Port);
        Assert.Equal((ushort)8790, config.Config.Proxy.Port);
    }

    private static ConfigSource Source(LoadedConfig config, string key)
    {
        Assert.True(config.TryGet(key, out var entry), key);
        return entry.Source;
    }
}
