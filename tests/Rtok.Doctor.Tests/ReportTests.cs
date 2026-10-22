namespace Rtok.Doctor.Tests;

public sealed class ReportTests
{
    [Fact]
    public void Hooks_are_counted_by_event_and_tool_and_mcp_servers_by_tools_and_tokens()
    {
        var f = new DoctorFake();
        f.Exe("fmt");
        f.Write(
            f.At(".claude/settings.json"),
            DoctorFake.Hooks(
                ("PreToolUse", "Bash", "fmt"),
                ("PreToolUse", "Edit|Write", "fmt --check"),
                ("Stop", null, "fmt")
            )
        );
        f.Write(
            f.At(".claude.json"),
            """{"mcpServers": {"docs": {"command": "fmt", "args": ["serve"]}}}"""
        );
        f.Write(
            f.InProject(".mcp.json"),
            """{"mcpServers": {"web": {"type": "http", "url": "https://example.com/mcp"}}}"""
        );
        f.Tools["docs"] = ["Reads the docs of a package.", "Searches every page."];

        var report = f.Page();

        Assert.Equal(3, report.HooksTotal);
        Assert.Equal(2, report.HooksByEvent["PreToolUse"]);
        Assert.Equal(1, report.HooksByEvent["Stop"]);
        Assert.Equal(1, report.HooksByTool["PreToolUse"]["Bash"]);
        var docs = Assert.Single(report.Mcp, s => s.Name is "docs");
        Assert.Equal(2, docs.Tools);
        Assert.True(docs.DescTokens > 0);
        Assert.Contains(report.Mcp, s => s.Name is "web");
        Assert.Contains("hooks 3\n  PreToolUse 2\n", f.Text(), StringComparison.Ordinal);
    }
}
