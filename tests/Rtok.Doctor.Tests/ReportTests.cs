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

    /// <summary>
    /// A config whose root or a member on the way is not an object has nothing of rtok's in
    /// it: the report still renders (fuzz seed <c>doctor-config/06</c>: a settings file of <c>0</c>).
    /// </summary>
    [Theory]
    [InlineData(".claude/settings.json", "0")]
    [InlineData(".claude/settings.json", "[1]")]
    [InlineData(".claude/settings.json", """{"env": 5, "hooks": 1, "enabledPlugins": []}""")]
    [InlineData(".claude.json", """{"mcpServers": {"rtok": 3}}""")]
    public void A_config_of_another_shape_still_renders(string file, string text)
    {
        var f = new DoctorFake();
        f.Write(f.At(file), text);

        Assert.NotEmpty(f.Text());
    }
}
