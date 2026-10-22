using Rtok.Agents.Hosts.Mcp;
using Rtok.Config;

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
}
