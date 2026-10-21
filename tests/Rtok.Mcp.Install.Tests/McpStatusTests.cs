using Rtok.Testing;

namespace Rtok.Mcp.Install.Tests;

public sealed class McpStatusTests
{
    [Theory]
    [InlineData("""{"command":"/usr/local/bin/rtok","args":["mcp","--host","test"]}""", null)]
    [InlineData("""{"command":"rtok","args":["mcp"]}""", null)]
    [InlineData(
        """{"type":"local","command":["rtok","mcp","--host","test"],"enabled":true}""",
        null
    )]
    [InlineData(
        """{"command":"rtok","args":["serve"]}""",
        "runs `rtok serve`, want `rtok mcp --host test`"
    )]
    [InlineData(
        """{"command":"node","args":["rtok.js"]}""",
        "runs `node rtok.js`, want `rtok mcp --host test`"
    )]
    public void An_entry_is_present_when_it_runs_rtok_mcp_for_the_host(string entry, string? diff)
    {
        var spec = Specs.Of(McpFormat.Json);
        var fs = MemFs.Linux();
        fs.Directory.CreateDirectory("/home/u/.config/host");
        // A comment: status reads strict-JSON files leniently, it never writes them.
        fs.File.WriteAllText(
            Specs.File,
            $$"""{ // mine{{"\n"}}"mcpServers": {"rtok": {{entry}}} }"""
        );

        var row = McpStatus.Of(spec, new McpDisk(fs), Specs.IsRtokBin, home: "/home/u");

        Assert.Equal(diff is null ? McpEntryState.Present : McpEntryState.Stale, row.Entry);
        Assert.Equal(diff, row.Diff);
        Assert.Equal("~/.config/host/config", row.File);
    }

    [Theory]
    [InlineData(McpEntryState.Present, null, null, "entry ~/.claude.json")]
    [InlineData(
        McpEntryState.Stale,
        "runs `x`, want `rtok mcp`",
        null,
        "stale (runs `x`, want `rtok mcp`)"
    )]
    [InlineData(McpEntryState.Missing, null, null, "missing (.claude.json has no \"rtok\")")]
    [InlineData(
        McpEntryState.Missing,
        "~/.claude.json: unreadable",
        null,
        "missing (~/.claude.json: unreadable)"
    )]
    [InlineData(McpEntryState.Missing, null, "rtok", "plugin rtok (serves \"rtok\")")]
    [InlineData(
        McpEntryState.Present,
        null,
        "rtok",
        "entry ~/.claude.json, plugin rtok (serves \"rtok\")"
    )]
    public void The_row_text_says_what_serves_the_surface(
        McpEntryState entry,
        string? diff,
        string? plugin,
        string text
    )
    {
        var row = new McpRow("cli", "~/.claude.json", entry, diff, plugin);

        Assert.Equal(text, row.Text());
        Assert.Equal(entry == McpEntryState.Present || plugin is not null, row.Serves);
    }
}
