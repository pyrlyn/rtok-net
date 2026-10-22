using Rtok.Testing;

namespace Rtok.Doctor.Tests;

public sealed class FixTests
{
    private static readonly IReadOnlyList<string> Broken = [ProblemKinds.BrokenHook];
    private static readonly IReadOnlyList<string> DupHooks = [ProblemKinds.DuplicateHook];
    private static readonly IReadOnlyList<string> DupMcp = [ProblemKinds.DuplicateMcp];

    [Fact]
    public void A_broken_hook_goes_and_every_other_byte_stays()
    {
        var f = new DoctorFake();
        f.Exe("fmt");
        var path = f.At(".claude/settings.json");
        const string Before = """
            {
              // the user's own notes
              "model": "opus",
              "hooks": {
                "Stop": [
                  {"hooks": [{"type": "command", "command": "fmt"}]},
                  {"hooks": [{"type": "command", "command": "gone-tool --x"}]}
                ]
              }
            }

            """;
        f.Write(path, Before);

        var (text, code) = DoctorFix.Run(f.Options(), Broken, apply: true, stat: false);

        Assert.Equal(0, code);
        var after = f.Read(path)!;
        Assert.DoesNotContain("gone-tool", after, StringComparison.Ordinal);
        Assert.Contains("// the user's own notes", after, StringComparison.Ordinal);
        Assert.Contains(
            "{\"hooks\": [{\"type\": \"command\", \"command\": \"fmt\"}]}",
            after,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "  remove Stop `gone-tool --x`: `gone-tool` not on PATH\n",
            text,
            StringComparison.Ordinal
        );
        Assert.EndsWith("1 entry removed, 0 left\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dry_run_prints_the_diff_and_writes_nothing()
    {
        var f = new DoctorFake();
        var path = f.At(".claude/settings.json");
        var before = DoctorFake.Hooks(("Stop", null, "gone-tool"));
        f.Write(path, before);

        var (text, code) = DoctorFix.Run(f.Options(), null, apply: false, stat: false);

        Assert.Equal(0, code);
        Assert.Equal(before, f.Read(path));
        Assert.StartsWith(
            "dry run: nothing is written. Add --yes to remove the entries below.\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("  would remove Stop `gone-tool`", text, StringComparison.Ordinal);
        Assert.Contains("-", text, StringComparison.Ordinal);
        Assert.Contains("1 file changed", text, StringComparison.Ordinal);
        Assert.EndsWith("1 entry would be removed\n", text, StringComparison.Ordinal);
        Assert.False(f.Fs.Directory.Exists(f.At(".claude/_backup")));
    }

    [Fact]
    public void Applying_takes_one_backup_generation_per_edited_file()
    {
        using var dir = new TempDir();
        var f = new DoctorFake(dir);
        var user = f.At(".claude/settings.json");
        var local = f.At(".claude/settings.local.json");
        f.Write(user, DoctorFake.Hooks(("Stop", null, "gone-a"), ("Stop", null, "gone-b")));
        f.Write(local, DoctorFake.Hooks(("Stop", null, "gone-c")));
        var real = f.Options() with { Write = DoctorWrites.Disk(backup: true, keep: 5) };

        var (text, code) = DoctorFix.Run(real, null, apply: true, stat: false);

        Assert.Equal(0, code);
        Assert.Equal("3 entries removed, 0 left\n", text.Split('\n')[^2] + "\n");
        var backups = f.Fs.Directory.GetFiles(
            f.At(".claude/_backup"),
            "*",
            SearchOption.AllDirectories
        );
        Assert.Equal(2, backups.Length);
        Assert.Equal(
            2,
            text.Split('\n').Count(l => l.StartsWith("  backup ", StringComparison.Ordinal))
        );
    }

    [Fact]
    public void Plugin_files_managed_files_and_toml_hooks_are_refused_not_edited()
    {
        var f = new DoctorFake();
        var managed = f.At("managed/managed-settings.json");
        f.Managed.Add(managed);
        f.Write(managed, DoctorFake.Hooks(("Stop", null, "gone-managed")));
        var plugin = f.Plugin("p@m", DoctorFake.Hooks(("Stop", null, "gone-plugin")));
        f.Write(
            f.At(".codex/config.toml"),
            "[[hooks.Stop]]\n[[hooks.Stop.hooks]]\ntype = \"command\"\ncommand = \"gone-codex\"\n"
        );
        var files = new[]
        {
            managed,
            f.Fs.Path.Combine(plugin, "hooks", "hooks.json"),
            f.At(".codex/config.toml"),
        }.ToDictionary(p => p, f.Read, StringComparer.Ordinal);

        var (text, _) = DoctorFix.Run(f.Options(), Broken, apply: true, stat: false);

        foreach (var (path, before) in files)
        {
            Assert.Equal(before, f.Read(path));
        }
        Assert.Contains("not removed: Stop `gone-managed`", text, StringComparison.Ordinal);
        Assert.Contains("not removed: Stop `gone-plugin`", text, StringComparison.Ordinal);
        Assert.Contains(": not a file of yours to edit\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_kept_copy_of_a_duplicate_is_never_removed()
    {
        var f = new DoctorFake();
        f.Exe("fmt");
        var hooks = DoctorFake.Hooks(("Stop", null, "fmt"));
        f.Write(f.At(".claude/settings.json"), hooks);
        f.Write(f.At(".claude/settings.local.json"), hooks);
        f.Plugin("fmt@m", hooks);

        var (_, code) = DoctorFix.Run(f.Options(), DupHooks, apply: true, stat: false);

        Assert.Equal(0, code);
        var report = f.Page();
        Assert.Equal(1, report.HooksTotal);
        Assert.DoesNotContain(report.Problems ?? [], p => p.Kind is ProblemKinds.DuplicateHook);
    }

    [Fact]
    public void Rtoks_own_mcp_entry_stays_while_a_hand_written_copy_under_another_name_goes()
    {
        var f = new DoctorFake();
        var claudeJson = f.At(".claude.json");
        var rtok = DoctorFake.Quote(f.Self);
        var entry = "{\"command\": " + rtok + ", \"args\": [\"mcp\"]}";
        f.Write(
            claudeJson,
            "{\"mcpServers\": {\"rtok\": " + entry + ", \"rtok-dev\": " + entry + "}}"
        );
        f.Write(f.InProject(".mcp.json"), "{\"mcpServers\": {\"rtok\": " + entry + "}}");

        var plan = DoctorFix.Plan(f.Options(), new FixRequest { Kinds = DupMcp });

        var item = Assert.Single(plan.Items);
        Assert.Equal("mcp mcpServers.rtok-dev", item.Label);
        Assert.True(item.Selected);
        var (_, code) = DoctorFix.Run(f.Options(), DupMcp, apply: true, stat: false);
        Assert.Equal(0, code);
        var after = f.Read(claudeJson)!;
        Assert.DoesNotContain("rtok-dev", after, StringComparison.Ordinal);
        Assert.Contains("\"rtok\":", after, StringComparison.Ordinal);
        Assert.Contains("\"rtok\":", f.Read(f.InProject(".mcp.json"))!, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_project_files_start_unselected_and_a_toggle_selects_them()
    {
        var f = new DoctorFake();
        var shared = f.InProject(".claude/settings.json");
        f.Write(shared, DoctorFake.Hooks(("Stop", null, "gone")));

        var plan = DoctorFix.Plan(f.Options(), new FixRequest());
        var item = Assert.Single(plan.Items);
        Assert.True(item.Shared);
        Assert.False(item.Selected);
        Assert.Equal("", plan.Diff);

        var toggled = DoctorFix.Plan(
            f.Options(),
            new FixRequest { Toggled = [new FixRef(item.Source, item.Path)] }
        );
        Assert.True(Assert.Single(toggled.Items).Selected);
        Assert.NotEmpty(toggled.Diff);
    }

    [Fact]
    public void Keeping_another_copy_swaps_which_one_goes()
    {
        var f = new DoctorFake();
        f.Exe("fmt");
        var hooks = DoctorFake.Hooks(("Stop", null, "fmt"));
        var user = f.At(".claude/settings.json");
        var local = f.At(".claude/settings.local.json");
        f.Write(user, hooks);
        f.Write(local, hooks);
        var first = DoctorFix.Plan(f.Options(), new FixRequest { Kinds = DupHooks });
        var extra = Assert.Single(first.Items);
        Assert.Equal(local, extra.Source);
        Assert.Equal(user, extra.KeptIn);
        Assert.True(extra.CanKeep);

        var request = new FixRequest
        {
            Kinds = DupHooks,
            Keep = [new FixRef(extra.Source, extra.Path)],
        };
        var swapped = DoctorFix.Plan(f.Options(), request);
        Assert.Equal(user, Assert.Single(swapped.Items).Source);

        var result = DoctorFix.Apply(f.Options(), request, swapped.Token);
        Assert.Equal(0, result.Code);
        Assert.Null(result.Replan);
        Assert.Contains("fmt", f.Read(local)!, StringComparison.Ordinal);
        Assert.DoesNotContain("fmt", f.Read(user)!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_changed_after_the_preview_returns_a_fresh_plan_and_writes_nothing()
    {
        var f = new DoctorFake();
        var path = f.At(".claude/settings.json");
        f.Write(path, DoctorFake.Hooks(("Stop", null, "gone-a")));
        var plan = DoctorFix.Plan(f.Options(), new FixRequest());
        var changed = DoctorFake.Hooks(("Stop", null, "gone-a"), ("Stop", null, "gone-b"));
        f.Write(path, changed);

        var result = DoctorFix.Apply(f.Options(), new FixRequest(), plan.Token);

        Assert.Equal(1, result.Code);
        Assert.NotNull(result.Replan);
        Assert.NotEqual(plan.Token, result.Replan.Token, StringComparer.Ordinal);
        Assert.Equal(changed, f.Read(path));
        Assert.StartsWith(
            "The files changed since the preview",
            result.Text,
            StringComparison.Ordinal
        );

        var applied = DoctorFix.Apply(f.Options(), new FixRequest(), result.Replan.Token);
        Assert.Equal(0, applied.Code);
        Assert.DoesNotContain("gone", f.Read(path)!, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_files_give_the_same_token()
    {
        var f = new DoctorFake();
        f.Write(f.At(".claude/settings.json"), DoctorFake.Hooks(("Stop", null, "gone")));
        Assert.Equal(
            DoctorFix.Plan(f.Options(), new FixRequest()).Token,
            DoctorFix.Plan(f.Options(), new FixRequest()).Token
        );
    }

    [Fact]
    public void A_failed_write_exits_1_and_reports_why()
    {
        var f = new DoctorFake();
        var path = f.At(".claude/settings.json");
        var before = DoctorFake.Hooks(("Stop", null, "gone"));
        f.Write(path, before);
        f.FailWrites = "write failed: disk full";

        var (text, code) = DoctorFix.Run(f.Options(), Broken, apply: true, stat: false);

        Assert.Equal(1, code);
        Assert.Contains("  failed: write failed: disk full\n", text, StringComparison.Ordinal);
        Assert.EndsWith("0 entries removed, 1 left\n", text, StringComparison.Ordinal);
        Assert.Equal(before, f.Read(path));
    }

    [Fact]
    public void Nothing_to_fix_says_so()
    {
        var f = new DoctorFake();
        Assert.Equal(
            ("nothing to remove\n", 0),
            DoctorFix.Run(f.Options(), null, apply: true, stat: false)
        );
    }

    [Fact]
    public void A_toml_duplicate_server_goes_with_its_table_only()
    {
        var f = new DoctorFake();
        f.Exe("docs");
        var path = f.At(".codex/config.toml");
        const string Before = """
            # my codex config
            model = "gpt-5"

            [mcp_servers.docs]
            command = "docs"

            [mcp_servers.docs-copy]
            command = "docs"

            [profiles.fast]
            model = "gpt-5-mini"

            """;
        f.Write(path, Before);

        var (_, code) = DoctorFix.Run(f.Options(), DupMcp, apply: true, stat: false);

        Assert.Equal(0, code);
        Assert.Equal(
            Before.Replace(
                "[mcp_servers.docs-copy]\ncommand = \"docs\"\n",
                "",
                StringComparison.Ordinal
            ),
            f.Read(path)
        );
    }
}
