using Rtok.Agents.Hosts.Mcp;

namespace Rtok.Agents.Hosts.Tests;

public sealed class SetupPathsTests
{
    /// <summary>Every key moves a file some app's MCP module edits; a key left unset moves nothing.</summary>
    [Fact]
    public void Every_key_moves_a_file_an_app_edits()
    {
        var home = new FakeHome();
        var paths = SetupPaths.Of(key => "/moved/" + key.Replace('.', '/'), home.Fs);
        Assert.NotEmpty(paths);
        var ctx = home.Context(paths: paths);
        var edited = AgentHosts
            .All.SelectMany(h => h.Variants.SelectMany(v => new McpModule(h, v, ctx).Files))
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(paths, p => Assert.Contains(p.Value, edited));
        Assert.All(
            paths.Keys,
            k => Assert.NotNull(AgentHosts.Find(k[..k.IndexOf('.', StringComparison.Ordinal)]))
        );
        Assert.Empty(SetupPaths.Of(_ => null, home.Fs));
    }

    [Fact]
    public void A_directory_key_takes_the_name_that_exists()
    {
        var home = new FakeHome();
        home.Write("/b/mcp.json", "{}");
        var paths = SetupPaths.Of(k => Dir(k, "/b"), home.Fs);
        Assert.Equal("/b/mcp.json", paths["codebuddy.cli"]);
        Assert.Equal("/c/.mcp.json", SetupPaths.Of(k => Dir(k, "/c"), home.Fs)["codebuddy.cli"]);
    }

    private static string? Dir(string key, string dir) =>
        string.Equals(key, "setup.codebuddy.dir", StringComparison.Ordinal) ? dir : null;
}
