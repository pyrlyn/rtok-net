using Rtok.Config;

namespace Rtok.Cli.Tests;

/// <summary>
/// Every CLI flag is a config key: each long option of every command maps to a dotted key with a
/// default, unless it is an action of one call rather than a setting.
/// </summary>
public sealed class FlagCoverageTests
{
    /// <summary>Flags that are actions on any command, so they get no config key.</summary>
    private static readonly HashSet<string> ActionFlags = new(StringComparer.Ordinal)
    {
        "config", // which file to load, not a setting inside it
        "json", // how one call prints
        "force", // overwrite on one call
        "sources", // annotates `config show`
    };

    /// <summary>Keys a flag maps to that are one call's choice rather than a stored setting.</summary>
    private static readonly HashSet<string> ActionKeys = new(StringComparer.Ordinal);

    [Fact]
    public void Every_flag_maps_to_a_config_key()
    {
        var keys = ConfigSchema.LeafKeys.ToHashSet(StringComparer.Ordinal);

        var missing = Flags()
            .Where(f =>
                !ActionFlags.Contains(f.Name)
                && !ActionKeys.Contains(f.Key)
                && !keys.Contains(f.Key)
            )
            .Select(f => $"{f.Command} --{f.Name} maps to {f.Key}, which is not a config key")
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_exception_is_still_used()
    {
        var flags = Flags().ToList();

        var stale = ActionFlags
            .Where(name =>
                !flags.Exists(f => string.Equals(f.Name, name, StringComparison.Ordinal))
            )
            .Concat(
                ActionKeys.Where(key =>
                    !flags.Exists(f => string.Equals(f.Key, key, StringComparison.Ordinal))
                )
            )
            .ToList();

        Assert.Empty(stale);
    }

    [Theory]
    [InlineData("", "log-level", "log.level")]
    [InlineData("", "verbose", "verbose")]
    [InlineData("proxy", "port", "proxy.port")]
    [InlineData("proxy", "mode", "proxy.mode")]
    [InlineData("bench", "mode", "bench.modes")]
    [InlineData("mcp ping", "timeout", "mcp.ping.timeout_s")]
    [InlineData("bench", "compare", "bench.baseline")]
    [InlineData("agents install", "dry-run", "setup.dry_run")]
    [InlineData("agents junk clear", "yes", "junk.yes")]
    [InlineData("agents usage", "host", "agents.usage.hosts")]
    [InlineData("agents usage", "monthly", "agents.usage.period")]
    [InlineData("agents usage", "since", "agents.usage.since")]
    [InlineData("task init", "prefix", "tasks.prefix")]
    [InlineData("dashboard", "port", "web.port")]
    [InlineData("mcp", "host", "hook.host")]
    [InlineData("run", "shell", "plugins.cmd.shell")]
    [InlineData("filter", "no-trailer", "plugins.cmd.trailer_min_lines")]
    [InlineData("filter", "cmd", "filter.cmd")]
    [InlineData("run", "trailer", "plugins.cmd.trailer")]
    [InlineData("config set", "dry-run", "config.set.dry_run")]
    public void A_flag_maps_by_the_naming_rules(string command, string flag, string key) =>
        Assert.Equal(key, KeyOf(command, flag));

    private static IEnumerable<(string Command, string Name, string Key)> Flags() =>
        from command in RtokBinary.Model.Commands
        from option in command.Options
        from spelling in option.Names
        where spelling.StartsWith("--", StringComparison.Ordinal)
        let name = spelling[2..]
        select (command.Path, name, KeyOf(command.Path, name));

    /// <summary>
    /// The key a flag sets: <c>--a-b</c> on <c>x y</c> is <c>x.y.a_b</c>, with the renames and the
    /// tables some command groups keep.
    /// </summary>
    private static string KeyOf(string command, string flag)
    {
        string[] path = command.Length == 0 ? [] : command.Split(' ');
        var name = flag.Replace('-', '_') switch
        {
            "timeout" => "timeout_s",
            "compare" => "baseline",
            "mode" when path is ["proxy"] => "mode",
            "mode" => "modes",
            var other => other,
        };
        return path switch
        {
            // `agents junk` has its own table, never `[setup]`, whose `yes` confirms another action.
            ["agents", "junk", ..] => $"junk.{name}",
            // `agents usage` has `[agents.usage]`: `--host` is the `hosts` list, `--daily` and
            // `--monthly` both set `period`.
            ["agents", "usage"] => name switch
            {
                "host" => "agents.usage.hosts",
                "daily" or "monthly" => "agents.usage.period",
                _ => $"agents.usage.{name}",
            },
            // `agents install|remove` keep the `[setup]` table they had as `setup`.
            ["agents", ..] => $"setup.{name}",
            ["task", "init"] => $"tasks.{name}",
            // `dashboard` is the hidden old spelling of `web`.
            ["dashboard", ..] => $"web.{name}",
            // `mcp --host` overlays `[hook] host`, as `hook --host` does.
            ["mcp"] when name is "host" => "hook.host",
            ["run", ..] or ["filter", ..] => name switch
            {
                "shell" => "plugins.cmd.shell",
                "no_trailer" => "plugins.cmd.trailer_min_lines",
                "cmd" => "filter.cmd",
                _ => $"plugins.cmd.{name}",
            },
            [] when name is "log_level" => "log.level",
            [] => name,
            _ => $"{string.Join('.', path)}.{name}",
        };
    }
}
