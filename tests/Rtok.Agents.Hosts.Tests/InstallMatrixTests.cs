using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Pyrlyn.Config.Layered;
using Rtok.Agents.Hosts.Hooks;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>
/// Every host on a fake home per OS: install, install again, update, uninstall. The second
/// install and the update change no byte, every app with hooks has rtok's, every app with MCP
/// support ends with exactly one rtok entry per surface that serves, and uninstall leaves no
/// trace of rtok.
/// </summary>
public sealed partial class InstallMatrixTests
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
        AssertOneHookPerEvent(installed);

        // `agents update` with no host: the hosts rtok is in, those with hooks or MCP support.
        var withRtok = present
            .Where(id =>
                AgentHosts
                    .Find(id)!
                    .Variants.Any(v => ctx.Probe.Present(v) && Writes(AgentHosts.Find(id)!, v, ctx))
            )
            .ToList();
        Assert.Equal(withRtok, run.Installed());
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
                var hooks = new HooksModule(host, v, ctx);
                Assert.True(
                    WritesHooks(host, v, ctx) == hooks.Installed(),
                    $"{host.Id} {v.Name}: hooks installed {hooks.Installed()}"
                );
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

    /// <summary>No file holds two rtok hooks for one (event, matcher) pair.</summary>
    private static void AssertOneHookPerEvent(SortedDictionary<string, string> files)
    {
        foreach (var (path, text) in files)
        {
            var hooks =
                path.EndsWith(".json", StringComparison.Ordinal) ? Hooks(JsonNode.Parse(text), "")
                : path.EndsWith(".toml", StringComparison.Ordinal)
                    ? Hooks(TomlText.ToJson(TomlText.Parse(text, path)), "")
                : [];
            var twice = hooks
                .GroupBy(h => h, StringComparer.Ordinal)
                .Where(g => g.Skip(1).Any())
                .Select(g => $"{path}: {g.Key}")
                .ToList();
            Assert.Empty(twice);
        }
    }

    /// <summary>The (rtok event, matcher) of every hook command under <paramref name="node"/>.</summary>
    private static IEnumerable<string> Hooks(JsonNode? node, string matcher)
    {
        switch (node)
        {
            case JsonObject o:
                var m =
                    o["matcher"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : matcher;
                foreach (var (key, value) in o)
                {
                    if (
                        key is "command" or "bash"
                        && value is JsonValue c
                        && c.TryGetValue<string>(out var cmd)
                        && RtokHook().Match(cmd) is { Success: true } hit
                    )
                    {
                        yield return $"{hit.Groups["ev"].Value} {m}";
                        continue;
                    }
                    foreach (var h in Hooks(value, m))
                    {
                        yield return h;
                    }
                }
                break;
            case JsonArray a:
                foreach (var h in a.SelectMany(x => Hooks(x, matcher)))
                {
                    yield return h;
                }
                break;
        }
    }

    [GeneratedRegex(
        @"rtok(?:\.exe)?""? hook (?<ev>\w+)",
        RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex RtokHook();

    private static bool Writes(AgentHost host, HostVariant v, HostContext ctx) =>
        WritesHooks(host, v, ctx) || host.Mcp.Of(v.Kind).Level == SupportLevel.Yes;

    /// <summary>Cline's hooks are plugin links, which come with T35.5.</summary>
    private static bool WritesHooks(AgentHost host, HostVariant v, HostContext ctx) =>
        host.Hooks.Of(v.Kind).Level == SupportLevel.Yes
        && new HooksModule(host, v, ctx).Files.Count > 0;

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
