using System.Text.Json.Nodes;

namespace Rtok.Agent.Sdk.Tests;

public sealed class ServerEntriesTests : IDisposable
{
    private static readonly JsonObject Ours = ServerEntries.McpEntry("rtok", ["mcp"]);

    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    private string Path => dir["mcp.json"];

    private string Install(Apply apply) =>
        ServerEntries.RegisterMcp(apply, Path, "rtok", "rtok", ["mcp"]);

    private string Uninstall(Apply apply) =>
        ServerEntries.Unregister(apply, Path, "mcpServers", "rtok", Ours, Scratch.IsRtok);

    [Fact]
    public void InstallTwiceLeavesOneEntryAndTheSecondRunChangesNothing()
    {
        dir.Write("mcp.json", "{\"mcpServers\":{\"other\":{\"command\":\"node\"}}}");
        var run = new BackupRun(5);
        var apply = new Apply { Backup = true, Run = run };

        Assert.Equal("mcpServers.rtok: rtok mcp", Install(apply));
        run.Finish(changed: true);
        var once = dir.Read("mcp.json");
        Assert.Equal(Apply.NoChanges, Install(apply));
        run.Finish(changed: false);

        Assert.Equal(once, dir.Read("mcp.json"));
        var servers = JsonNode.Parse(once)!["mcpServers"]!.AsObject();
        Assert.Equal("other,rtok", string.Join(',', servers.Select(kv => kv.Key)));
        Assert.Single(dir.Entries("_backup"));
    }

    [Fact]
    public void AnEntryWithoutTheDefaultTypeIsCurrent()
    {
        dir.Write(
            "mcp.json",
            "{\"mcpServers\":{\"rtok\":{\"command\":\"rtok\",\"args\":[\"mcp\"]}}}"
        );
        Assert.Equal(Apply.NoChanges, Install(new Apply()));
    }

    [Fact]
    public void AStaleEntryIsRewritten()
    {
        dir.Write(
            "mcp.json",
            "{\"mcpServers\":{\"rtok\":{\"command\":\"rtok\",\"args\":[\"serve\"]}}}"
        );
        Assert.Equal("mcpServers.rtok: rtok mcp", Install(new Apply()));
        Assert.Equal(Apply.NoChanges, Install(new Apply()));
    }

    [Fact]
    public void DryRunInstallReportsAndWritesNothing()
    {
        Assert.Equal("mcpServers.rtok: rtok mcp", Install(new Apply { DryRun = true }));
        Assert.Empty(dir.Entries(""));
    }

    [Fact]
    public void RemovingRtoksExactEntryDropsTheEmptyMap()
    {
        dir.Write("mcp.json", "{\"keep\":1}");
        Install(new Apply());
        Assert.Equal("- mcpServers.rtok", Uninstall(new Apply()));
        Assert.Equal("{\n  \"keep\": 1\n}\n", dir.Read("mcp.json"));
        Assert.Equal(Apply.NoChanges, Uninstall(new Apply()));
    }

    [Theory]
    [InlineData("{\"command\":\"/usr/local/bin/rtok\",\"args\":[\"mcp\"]}")]
    [InlineData(
        "{\"type\":\"stdio\",\"command\":\"C:\\\\bin\\\\rtok.exe\",\"args\":[\"mcp\",\"--host\",\"cursor\"]}"
    )]
    public void AnyRtokPathAndTheHostArgCountAsRtoksOwn(string entry)
    {
        dir.Write("mcp.json", $"{{\"mcpServers\":{{\"rtok\":{entry},\"other\":{{}}}}}}");
        Assert.Equal("- mcpServers.rtok", Uninstall(new Apply()));
        Assert.Equal("{\n  \"mcpServers\": {\n    \"other\": {}\n  }\n}\n", dir.Read("mcp.json"));
    }

    [Fact]
    public void AForeignEntryIsLeftByteForByte()
    {
        const string Body =
            "{\"mcpServers\":{\"rtok\":{\"command\":\"node\",\"args\":[\"rtok.js\"]}}}";
        dir.Write("mcp.json", Body);
        Assert.Equal(
            $"leave mcpServers.rtok in {Path} (not rtok's; remove by hand)",
            Uninstall(new Apply { Yes = true })
        );
        Assert.Equal(Body, dir.Read("mcp.json"));
    }

    private const string Edited =
        "{\"mcpServers\":{\"rtok\":{\"command\":\"rtok\",\"args\":[\"mcp\"],\"env\":{\"A\":\"1\"}}}}";

    [Fact]
    public void AnEditedEntryAsksOnADryRun()
    {
        dir.Write("mcp.json", Edited);
        Assert.Equal(
            $"? mcpServers.rtok in {Path} (changed by you; remove asks)",
            Uninstall(new Apply { DryRun = true })
        );
        Assert.Equal(Edited, dir.Read("mcp.json"));
    }

    [Fact]
    public void AnEditedEntryIsLeftWithoutATerminal()
    {
        dir.Write("mcp.json", Edited);
        Assert.Equal(
            $"leave mcpServers.rtok in {Path} (changed by you; remove by hand)",
            Uninstall(new Apply())
        );
        Assert.Equal(Edited, dir.Read("mcp.json"));
    }

    [Fact]
    public void AnEditedEntryGoesOnYesOrAnAcceptedPrompt()
    {
        dir.Write("mcp.json", Edited);
        string? asked = null;
        var apply = new Apply
        {
            Ask = (q, d) =>
            {
                asked = q;
                return !d;
            },
        };
        Assert.Equal("- mcpServers.rtok", Uninstall(apply));
        Assert.Equal($"remove mcpServers.rtok in {Path}? you changed it", asked);

        dir.Write("mcp.json", Edited);
        Assert.Equal("- mcpServers.rtok", Uninstall(new Apply { Yes = true }));
    }

    [Fact]
    public void ADottedKeyDropsOnlyTheLastLevel()
    {
        var ours = ServerEntries.McpEntry("rtok", ["mcp"]);
        Assert.Equal(
            "mcp.servers.rtok: rtok mcp",
            ServerEntries.Register(new Apply(), Path, "mcp.servers", "rtok", ours, "rtok mcp")
        );
        Assert.Equal(
            "- mcp.servers.rtok",
            ServerEntries.Unregister(new Apply(), Path, "mcp.servers", "rtok", ours, Scratch.IsRtok)
        );
        Assert.Equal("{\n  \"mcp\": {}\n}\n", dir.Read("mcp.json"));
    }

    [Fact]
    public void RemovingFromAMissingFileChangesNothing()
    {
        Assert.Equal(Apply.NoChanges, Uninstall(new Apply()));
        Assert.Empty(dir.Entries(""));
    }
}
