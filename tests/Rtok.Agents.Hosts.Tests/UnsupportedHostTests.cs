namespace Rtok.Agents.Hosts.Tests;

public sealed class UnsupportedHostTests
{
    [Fact]
    public void Trae_is_dated_sourced_and_says_why()
    {
        var trae = AgentHosts.Unsupported.Single(h =>
            string.Equals(h.Id, "trae", StringComparison.Ordinal)
        );

        Assert.Equal(("Trae", HostKind.Desktop), (trae.Name, trae.Kind));
        Assert.Equal(new DateOnly(2026, 10, 22), trae.Checked);
        Assert.All(
            trae.Sources,
            s => Assert.Equal("docs.trae.ai", new Uri(s, UriKind.Absolute).Host)
        );
        Assert.NotEmpty(trae.Sources);
        Assert.Contains("no user-level MCP file", trae.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unsupported_host_is_no_installable_host() =>
        Assert.Empty(
            AgentHosts
                .Unsupported.Select(h => h.Id)
                .Intersect(AgentHosts.All.Select(h => h.Id), StringComparer.Ordinal)
        );

    [Fact]
    public void Resolving_trae_says_it_is_not_supported_and_why()
    {
        var trae = AgentHosts.Unsupported.Single(h =>
            string.Equals(h.Id, "trae", StringComparison.Ordinal)
        );

        Assert.Equal(
            $"trae is not supported: {trae.Reason}",
            Assert.Throws<AgentsException>(() => AgentHosts.Resolve(["claude", "trae"])).Message
        );
    }
}
