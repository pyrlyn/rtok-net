using Rtok.Agents.Hosts.Mcp;
using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

public sealed class AgentsCommandsTests
{
    /// <summary>rtok's aliases reach the same commands: <c>agent</c>, <c>setup</c>, <c>remove</c>.</summary>
    [Theory]
    [InlineData("agent install", "agents install")]
    [InlineData("agents setup", "agents install")]
    [InlineData("agent setup", "agents install")]
    [InlineData("agents remove", "agents uninstall")]
    [InlineData("agent list", "agents list")]
    public void An_alias_runs_its_command(string alias, string command) =>
        Assert.Contains(
            $"Usage: {command} ",
            RtokBinary.Run([.. alias.Split(' '), "--help"]),
            StringComparison.Ordinal
        );

    [Fact]
    public void Every_path_key_is_a_config_key() =>
        Assert.Empty(
            SetupPaths
                .Keys.Concat(SetupPaths.HookKeys)
                .Concat(SetupPaths.ProxyKeys)
                .Except(ConfigSchema.LeafKeys, StringComparer.Ordinal)
        );

    /// <summary>Trae names no user-level MCP file: install says so and leaves the home empty.</summary>
    [Fact]
    public async Task Install_trae_is_not_supported_and_writes_nothing()
    {
        using var home = new TempDir("rtok-trae-");
        var run = RtokBinary.Exec(
            ["agents", "install", "trae"],
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["HOME"] = home.Path,
                ["USERPROFILE"] = home.Path,
                ["RTOK_HOME"] = home.Combine(".rtok"),
                ["XDG_CONFIG_HOME"] = home.Combine(".config"),
            }
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(home.Path, "*", SearchOption.AllDirectories)
        );
        await Verify(
            new
            {
                run.Code,
                run.Stdout,
                run.Stderr,
            }
        );
    }
}
