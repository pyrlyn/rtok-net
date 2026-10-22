using System.Text.Json.Nodes;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary><c>rtok doctor</c> against the binary on a home that has every kind of problem; nothing real is started.</summary>
public sealed class DoctorCommandTests : IDisposable
{
    private readonly TempDir _dir = new("rtok-doctor-");

    private string Home => _dir.Combine("home");

    private string Project => _dir.Combine("home", "proj");

    private static string Q(string text) => JsonValue.Create(text).ToJsonString();

    private static string Hooks(params (string Event, string? Matcher, string Command)[] hooks) =>
        new JsonObject
        {
            ["hooks"] = new JsonObject(
                hooks
                    .GroupBy(h => h.Event, StringComparer.Ordinal)
                    .Select(g =>
                        KeyValuePair.Create<string, JsonNode?>(
                            g.Key,
                            new JsonArray([
                                .. g.Select(h =>
                                {
                                    var group = new JsonObject();
                                    if (h.Matcher is { } m)
                                    {
                                        group["matcher"] = m;
                                    }
                                    group["hooks"] = new JsonArray(
                                        new JsonObject
                                        {
                                            ["type"] = "command",
                                            ["command"] = h.Command,
                                        }
                                    );
                                    return (JsonNode)group;
                                }),
                            ])
                        )
                    )
            ),
        }.ToJsonString() + "\n";

    private void Write(string relative, string text)
    {
        var path = Path.Combine([Home, .. relative.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>A home with one problem of every kind.</summary>
    private void EveryKind()
    {
        Directory.CreateDirectory(Project);
        var script = Path.Combine(Home, "hooks", "plain.sh");
        Write("hooks/plain.sh", "#!/bin/sh\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        Write(
            ".claude/settings.json",
            Hooks(
                    ("PreToolUse", "Bash", "gone-tool --x"),
                    ("Stop", null, script),
                    ("Stop", null, "jq . | head"),
                    ("PostToolUse", "Edit", "echo done")
                )
                .Replace(
                    "{\"hooks\"",
                    "{\"enabledPlugins\": {\"old@market\": true}, \"hooks\"",
                    StringComparison.Ordinal
                )
        );
        Write(".claude/settings.local.json", Hooks(("PostToolUse", "Edit", "echo done")));
        Write(
            ".claude/plugins/installed_plugins.json",
            $$$$"""{"plugins": {"old@market": [{"installPath": {{{{Q(Path.Combine(Home, "gone-plugin"))}}}}}]}}"""
        );
        Write(".cursor/hooks.json", "{ not json");
        var rtok = Q(RtokBinary.Path);
        Write(
            ".claude.json",
            $$$$"""
            {"mcpServers": {
              "docs": {"command": "/no/such/docs", "env": {"KEY": "sk-secret-value"}},
              "pkg": {"command": "/no/such/npx", "args": ["@acme/pkg@1"]},
              "rtok": {"command": {{{{rtok}}}}, "args": ["mcp"]}
            }}
            """
        );
        Write(
            "proj/.mcp.json",
            $$$$"""
            {"mcpServers": {
              "docs-copy": {"command": "/no/such/docs", "env": {"KEY": "sk-secret-value"}},
              "pkg-next": {"command": "/no/such/npx", "args": ["@acme/pkg@2"]},
              "rtok": {"command": {{{{rtok}}}}, "args": ["mcp"]}
            }}
            """
        );
    }

    private (int Code, string Stdout, string Stderr) Doctor(params string[] args)
    {
        Directory.CreateDirectory(Project);
        var result = RtokBinary.Exec(
            ["doctor", .. args],
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["HOME"] = Home,
                ["USERPROFILE"] = Home,
                ["PATH"] = Path.Combine(Home, "bin"),
            },
            workingDirectory: Project
        );
        return (result.Code, Scrub(result.Stdout), result.Stderr);
    }

    private string Scrub(string text) =>
        text.Replace(RtokBinary.Path, "{rtok}", StringComparison.Ordinal)
            .Replace(
                RtokBinary.Path.Replace("\\", "\\\\", StringComparison.Ordinal),
                "{rtok}",
                StringComparison.Ordinal
            )
            // macOS hands the child its working directory through /private.
            .Replace("/private" + Home, "~", StringComparison.Ordinal)
            .Replace(Home, "~", StringComparison.Ordinal)
            .Replace(
                Home.Replace("\\", "\\\\", StringComparison.Ordinal),
                "~",
                StringComparison.Ordinal
            );

    [Fact]
    public Task The_text_names_every_kind_of_problem()
    {
        EveryKind();
        var (code, stdout, stderr) = Doctor();
        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.DoesNotContain("sk-secret-value", stdout, StringComparison.Ordinal);
        return Verify(stdout);
    }

    [Fact]
    public Task The_json_lists_every_problem()
    {
        EveryKind();
        var (code, stdout, _) = Doctor("--json");
        Assert.Equal(0, code);
        Assert.DoesNotContain("sk-secret-value", stdout, StringComparison.Ordinal);
        var problems = JsonNode.Parse(stdout)!["problems"]!.AsArray();
        var kinds = problems
            .Select(p => (string)p!["kind"]!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                "broken-hook",
                "conflicting-mcp",
                "duplicate-hook",
                "duplicate-mcp",
                "own-mcp",
                "stale-plugin",
                "suspect-hook",
                "unreadable-config",
                "unverified-hook",
            ],
            kinds,
            StringComparer.Ordinal
        );
        var rows = string.Join(
            '\n',
            problems.Select(p =>
                string.Join(
                    ' ',
                    (
                        (string[])
                            [
                                "kind",
                                "agent",
                                "source",
                                "path",
                                "detail",
                                "fixable",
                                "selected",
                                "keep",
                            ]
                    ).Select(k => p![k]?.ToString() ?? "-")
                )
            )
        );
        return Verify(rows);
    }

    [Fact]
    public void Fix_yes_only_broken_hooks_exits_0_when_every_selected_entry_went()
    {
        EveryKind();
        var (code, stdout, _) = Doctor("--fix", "--yes", "--only", "broken-hooks");
        Assert.True(code == 0, stdout);
        Assert.Contains("1 entry removed, 0 left", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "gone-tool",
            File.ReadAllText(Path.Combine(Home, ".claude", "settings.json")),
            StringComparison.Ordinal
        );
        Assert.Single(
            Directory.GetFiles(
                Path.Combine(Home, ".claude", "_backup"),
                "*",
                SearchOption.AllDirectories
            )
        );
    }

    [Fact]
    public void Fix_yes_only_broken_hooks_exits_1_when_a_selected_entry_was_not_removed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        EveryKind();
        var claude = Path.Combine(Home, ".claude");
        File.SetUnixFileMode(claude, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var (code, stdout, _) = Doctor("--fix", "--yes", "--only", "broken-hooks");
            Assert.True(code == 1, stdout);
            Assert.Contains("  failed: backup failed: ", stdout, StringComparison.Ordinal);
            Assert.Contains("0 entries removed, 1 left", stdout, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(
                claude,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
    }

    [Fact]
    public void A_dry_run_prints_the_diff_and_writes_nothing()
    {
        EveryKind();
        var settings = Path.Combine(Home, ".claude", "settings.json");
        var before = File.ReadAllText(settings);
        var (code, stdout, _) = Doctor("--fix", "--dry-run");
        Assert.Equal(0, code);
        Assert.StartsWith("dry run: nothing is written.", stdout, StringComparison.Ordinal);
        Assert.Contains("-", stdout, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(settings));
    }

    [Theory]
    [InlineData("--json --fix", "--json and --fix cannot be used together")]
    [InlineData("--yes", "--yes, --dry-run, --stat and --only go with --fix")]
    [InlineData("--agent nope", "unknown host `nope`")]
    [InlineData("--fix --only junk", "unknown problem `junk`")]
    public void Bad_flags_exit_2(string args, string message)
    {
        var (code, _, stderr) = Doctor(args.Split(' '));
        Assert.Equal(2, code);
        Assert.Contains(message, stderr, StringComparison.Ordinal);
    }

    public void Dispose() => _dir.Dispose();
}
