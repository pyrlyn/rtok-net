using Rtok.Agents.Hosts.Mcp;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>
/// Every host on a fake home per OS: install, install again, update, uninstall. The second
/// install and the update change no byte, every app with MCP support ends with exactly one rtok
/// entry per surface that serves, and uninstall leaves no trace of rtok.
/// </summary>
public sealed class InstallMatrixTests
{
    public static TheoryData<McpOs> Systems => [McpOs.Linux, McpOs.MacOS, McpOs.Windows];

    [Theory]
    [MemberData(nameof(Systems))]
    public void Install_twice_update_and_uninstall_every_host(McpOs os)
    {
        var home = new FakeHome(os);
        home.InstallAll();
        home.PutRtokOnPath();
        var ctx = home.Context();
        var run = new HostRun(ctx);
        var present = AgentHosts
            .All.Where(h => h.Variants.Any(ctx.Probe.Present))
            .Select(h => h.Id)
            .ToList();
        Assert.NotEmpty(present);

        var first = run.Run(Request(present, McpMode.Install));
        Assert.DoesNotContain("warning", first, StringComparison.Ordinal);
        var installed = Files(home);
        AssertOneServingEntryPerSurface(ctx);

        var second = run.Run(Request(present, McpMode.Install));
        AssertNotes(second, " — already installed");
        Assert.Equal(installed, Files(home));

        // `agents update` with no host: the hosts rtok is in, which are those with MCP support.
        var withMcp = present
            .Where(id =>
                AgentHosts
                    .Find(id)!
                    .Variants.Any(v =>
                        ctx.Probe.Present(v)
                        && AgentHosts.Find(id)!.Mcp.Of(v.Kind).Level == SupportLevel.Yes
                    )
            )
            .ToList();
        Assert.Equal(withMcp, run.Installed());
        var update = run.Run(Request(run.Installed(), McpMode.Update));
        AssertNotes(update, " — already current");
        Assert.Equal(installed, Files(home));

        run.Run(Request([.. AgentHosts.All.Select(h => h.Id)], McpMode.Remove));
        Assert.Empty(run.Installed());
        Assert.All(
            Files(home),
            f => Assert.DoesNotContain("rtok", f.Value, StringComparison.Ordinal)
        );
    }

    /// <summary>Each present app with MCP support has rtok's entry, one per surface, and it serves.</summary>
    private static void AssertOneServingEntryPerSurface(HostContext ctx)
    {
        foreach (var host in AgentHosts.All)
        {
            foreach (var v in host.Variants.Where(ctx.Probe.Present))
            {
                var mcp = new McpModule(host, v, ctx);
                var expected = host.Mcp.Of(v.Kind).Level == SupportLevel.Yes;
                Assert.True(
                    expected == mcp.Installed(),
                    $"{host.Id} {v.Name}: installed {mcp.Installed()}"
                );
                var rows = mcp.Rows();
                Assert.Equal(
                    rows.Count,
                    rows.Select(r => r.Surface).Distinct(StringComparer.Ordinal).Count()
                );
                Assert.All(
                    rows,
                    r => Assert.True(r.Serves, $"{host.Id} {v.Name} {r.Surface}: {r.Text()}")
                );
            }
        }
    }

    private static HostRequest Request(IReadOnlyList<string> hosts, McpMode mode) =>
        new(hosts, mode, Cli: false, Desktop: false, All: false);

    /// <summary>Each app's header carries <paramref name="note"/>, or says the app is absent or shares the files above.</summary>
    private static void AssertNotes(string text, string note)
    {
        var headers = text.Split('\n')
            .Where(l =>
                l.StartsWith("CLI: ", StringComparison.Ordinal)
                || l.StartsWith("Desktop: ", StringComparison.Ordinal)
            )
            .ToList();
        Assert.NotEmpty(headers);
        Assert.All(
            headers,
            l =>
                Assert.True(
                    l.EndsWith(note, StringComparison.Ordinal)
                        || l.EndsWith(" — not found", StringComparison.Ordinal)
                        || l.EndsWith(" — same files as above", StringComparison.Ordinal),
                    l
                )
        );
    }

    /// <summary>Every host file under the home, by path: everything but the apps themselves.</summary>
    private static SortedDictionary<string, string> Files(FakeHome home)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (
            var path in home.Fs.Directory.EnumerateFiles(
                home.Home,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            if (!path.StartsWith(home.Bin, StringComparison.Ordinal))
            {
                files[path] = home.Fs.File.ReadAllText(path);
            }
        }
        return files;
    }
}
