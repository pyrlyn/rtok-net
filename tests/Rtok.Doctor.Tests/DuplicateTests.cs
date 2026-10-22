using System.Text.Json;
using CsCheck;

namespace Rtok.Doctor.Tests;

public sealed class DuplicateTests
{
    [Fact]
    public void Duplicate_hooks_match_on_event_matcher_and_command_spelling_aside()
    {
        var f = new DoctorFake();
        f.Exe("fmt");
        f.Write(
            f.At(".claude/settings.json"),
            DoctorFake.Hooks(
                ("PreToolUse", "Write|Edit", "fmt   --check"),
                ("PreToolUse", "Bash", "fmt --check")
            )
        );
        f.Write(
            f.At(".claude/settings.local.json"),
            DoctorFake.Hooks(("PreToolUse", "Edit | Write", "fmt --check"))
        );
        f.Write(
            f.InProject(".claude/settings.json"),
            DoctorFake.Hooks(("PostToolUse", "Edit|Write", "fmt --check"))
        );

        var dups = f.Page().Problems!.Where(p => p.Kind is ProblemKinds.DuplicateHook).ToList();

        Assert.Equal(2, dups.Count);
        Assert.Single(dups, p => p.Keep);
        Assert.All(dups, p => Assert.Equal("PreToolUse", p.Event));
        Assert.Contains(
            "duplicate hooks\n  claude PreToolUse [Write|Edit] `fmt   --check`: runs 2 times\n",
            f.Text(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void The_kept_copy_follows_plugin_then_project_then_user_then_local()
    {
        var f = new DoctorFake();
        f.Exe("fmt");
        var hooks = DoctorFake.Hooks(("Stop", null, "fmt"));
        f.Write(f.At(".claude/settings.local.json"), hooks);
        f.Write(f.At(".claude/settings.json"), hooks);
        f.Write(f.InProject(".claude/settings.json"), hooks);
        Assert.Equal(f.InProject(".claude/settings.json"), Kept(f).Source);
        Assert.EndsWith(
            "keep this copy (shared project file)",
            Kept(f).Detail,
            StringComparison.Ordinal
        );

        f.Plugin("fmt@market", hooks);
        var kept = Kept(f);
        Assert.EndsWith("hooks.json", kept.Source, StringComparison.Ordinal);
        Assert.EndsWith("(owned by a plugin)", kept.Detail, StringComparison.Ordinal);
        var extras = f.Page()
            .Problems!.Where(p => p.Kind is ProblemKinds.DuplicateHook && !p.Keep)
            .ToList();
        Assert.Equal(3, extras.Count);
        Assert.All(extras, p => Assert.True(p.Fixable));
    }

    private static Problem Kept(DoctorFake f) =>
        f.Page().Problems!.Single(p => p.Kind is ProblemKinds.DuplicateHook && p.Keep);

    private static readonly Gen<string> Secret = Gen.Char["ABCDEFGHJKLMNPQRSTUVWXYZ23456789"]
        .Array[12, 20]
        .Select(c => "sk" + new string(c));

    /// <summary>Two spellings of one launch, written under different names in two files.</summary>
    private static (string User, string Project) Spellings(
        DoctorFake f,
        int variant,
        string secret
    ) =>
        variant switch
        {
            0 => (
                $$$"""{"command": {{{DoctorFake.Quote(f.Fs.Path.Combine(f.Bin, "docs"))}}}, "env": {"KEY": "{{{secret}}}"}}""",
                $$$"""{"command": "docs", "env": {"KEY": "{{{secret}}}"}}"""
            ),
            1 => (
                $$$"""{"command": {{{DoctorFake.Quote(f.At("link/docs"))}}}, "env": {"KEY": "{{{secret}}}"}}""",
                $$$"""{"command": "docs", "env": {"KEY": "{{{secret}}}"}}"""
            ),
            2 => (
                $$$"""{"command": "npx", "args": ["-y", "@acme/docs@1.2.0"], "env": {"KEY": "{{{secret}}}"}}""",
                $$$"""{"command": "npx", "args": ["@acme/docs@1.2.0"], "env": {"KEY": "{{{secret}}}"}}"""
            ),
            3 => (
                $$$"""{"type": "http", "url": "https://Docs.Example.COM/mcp", "headers": {"Authorization": "Bearer {{{secret}}}"}}""",
                """{"type": "http", "url": "https://docs.example.com/mcp"}"""
            ),
            _ => (
                """{"type": "http", "url": "https://docs.example.com/mcp/"}""",
                $$$"""{"type": "http", "url": "https://docs.example.com/mcp", "headers": {"X-Key": "{{{secret}}}"}}"""
            ),
        };

    [Fact]
    public void Mcp_entries_that_differ_only_in_spelling_are_one_duplicate_and_no_secret_is_printed() =>
        Gen.Select(Gen.Int[0, 4], Secret, Gen.Char["abcdefgh"].Array[3, 8])
            .Sample(t =>
            {
                var (variant, secret, nameChars) = t;
                var name = new string(nameChars);
                var f = new DoctorFake();
                f.Exe("docs");
                f.Fs.Directory.CreateDirectory(f.At("link"));
                f.Fs.File.CreateSymbolicLink(f.At("link/docs"), f.Fs.Path.Combine(f.Bin, "docs"));
                var (user, project) = Spellings(f, variant, secret);
                f.Write(
                    f.At(".claude.json"),
                    $$$"""{"mcpServers": {"{{{name}}}-a": {{{user}}}}}"""
                );
                f.Write(
                    f.InProject(".mcp.json"),
                    $$$"""{"mcpServers": {"{{{name}}}-b": {{{project}}}}}"""
                );

                var report = f.Page();
                var dups = report
                    .Problems!.Where(p => p.Kind is ProblemKinds.DuplicateMcp)
                    .ToList();

                Assert.Equal(2, dups.Count);
                Assert.Single(dups, p => p.Keep);
                Assert.Single(dups.Select(p => p.Group).Distinct());
                Assert.DoesNotContain(secret, DoctorText.Render(report), StringComparison.Ordinal);
                Assert.DoesNotContain(
                    secret,
                    JsonSerializer.Serialize(report, DoctorJson.Default.DoctorReport),
                    StringComparison.Ordinal
                );
            });

    [Fact]
    public void One_name_in_two_scopes_names_the_copy_the_host_uses()
    {
        var f = new DoctorFake();
        f.Exe("docs");
        f.Write(f.At(".claude.json"), """{"mcpServers": {"docs": {"command": "docs"}}}""");
        f.Write(
            f.InProject(".mcp.json"),
            """{"mcpServers": {"docs": {"command": "docs", "args": ["--v2"]}}}"""
        );
        var dups = f.Page().Problems!.Where(p => p.Kind is ProblemKinds.DuplicateMcp).ToList();
        var used = Assert.Single(dups, p => p.Keep);
        Assert.Equal(f.InProject(".mcp.json"), used.Source);
        Assert.EndsWith("the host uses this one", used.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void One_package_at_two_versions_is_a_conflict_not_a_duplicate()
    {
        var f = new DoctorFake();
        f.Write(
            f.At(".claude.json"),
            """{"mcpServers": {"a": {"command": "npx", "args": ["pkg@1"]}}}"""
        );
        f.Write(
            f.InProject(".mcp.json"),
            """{"mcpServers": {"b": {"command": "npx", "args": ["pkg@2"]}}}"""
        );
        var problems = f.Page().Problems!;
        Assert.DoesNotContain(problems, p => p.Kind is ProblemKinds.DuplicateMcp);
        Assert.Equal(2, problems.Count(p => p.Kind is ProblemKinds.ConflictingMcp));
    }
}
