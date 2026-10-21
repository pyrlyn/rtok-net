using System.Text.Json;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary><c>agents info</c> as text and JSON: the same apps, files and module states.</summary>
public sealed class ListTests
{
    private static (FakeHome Home, IReadOnlyList<AgentHost> Hosts) Installed()
    {
        var home = new FakeHome(McpOs.MacOS);
        home.PutRtokOnPath();
        var hosts = AgentHosts.Resolve(["claude", "goose", "aider"]);
        home.Install(hosts[0].Variants[0]);
        home.Install(hosts[1].Variants[0]);
        home.Install(hosts[2].Variants[0]);
        home.Install(hosts[0].Variants[1]);
        home.Run().Run(new HostRequest(["claude", "goose"], McpMode.Install, false, false, false));
        // Claude Desktop then holds an rtok entry from an older rtok: a stale row with its diff.
        home.Write(
            home.At("Library/Application Support/Claude/claude_desktop_config.json"),
            "{\"mcpServers\": {\"rtok\": {\"command\": \"rtok\", \"args\": [\"serve\"]}}}\n"
        );
        return (home, hosts);
    }

    [Fact]
    public Task Info_text()
    {
        var (home, hosts) = Installed();
        return Verify(home.Run().List(hosts));
    }

    [Fact]
    public Task Info_json()
    {
        var (home, hosts) = Installed();
        return VerifyJson(
            JsonSerializer.Serialize(
                home.Run().Rows(hosts),
                AgentsJson.Default.IReadOnlyListAgentListRow
            )
        );
    }

    [Fact]
    public void List_names_every_app_in_table_order()
    {
        var home = new FakeHome();
        var rows = home.Run().Rows(AgentHosts.All);
        Assert.Equal(AgentHosts.All.Sum(h => h.Variants.Count), rows.Count);
        Assert.Equal(
            AgentHosts.All.SelectMany(h => h.Variants.Select(v => $"{h.Id} {v.Name}")),
            rows.Select(r => $"{r.Host} {r.Name}"),
            StringComparer.Ordinal
        );
        Assert.All(rows, r => Assert.False(r.Present));
    }
}
