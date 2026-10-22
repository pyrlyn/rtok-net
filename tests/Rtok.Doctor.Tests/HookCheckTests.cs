using Rtok.Mcp.Install;

namespace Rtok.Doctor.Tests;

public sealed class HookCheckTests
{
    private static (Verdict Verdict, string Detail) Classify(
        DoctorFake f,
        string command,
        string agent = "claude",
        string? pluginRoot = null
    )
    {
        var home = new DoctorHome(f.Options());
        var source = new HookSource(
            agent,
            f.At(".claude/settings.json"),
            pluginRoot is null ? Scope.User : Scope.Plugin,
            HookShape.Json,
            pluginRoot
        );
        var c = new CommandCheck(home).Classify(command, source);
        return (c.Verdict, c.Detail);
    }

    [Fact]
    public void Tilde_home_and_variables_expand_before_the_lookup()
    {
        var f = new DoctorFake();
        var script = f.At("hooks/run.sh");
        f.Write(script, "#!/bin/sh\n");
        f.Mode(script, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        f.Var("HOOKS", f.At("hooks"));

        Assert.Equal(Verdict.Ok, Classify(f, "~/hooks/run.sh").Verdict);
        Assert.Equal(Verdict.Ok, Classify(f, "$HOME/hooks/run.sh --fast").Verdict);
        Assert.Equal(Verdict.Ok, Classify(f, "${HOOKS}/run.sh").Verdict);
        Assert.Equal(
            (Verdict.Broken, $"file not found: {f.At("hooks/gone.sh")}"),
            Classify(f, "~/hooks/gone.sh")
        );
        Assert.Equal(Verdict.Unverified, Classify(f, "$NOPE/run.sh").Verdict);
    }

    [Fact]
    public void Host_variables_resolve_the_project_and_the_plugin_root()
    {
        var f = new DoctorFake();
        f.Exe("node");
        f.Write(f.InProject("scripts/check.js"), "");
        var plugin = f.At("plugins/p");
        f.Write(f.At("plugins/p/hook.py"), "");
        f.Exe("python3");

        Assert.Equal(
            Verdict.Ok,
            Classify(f, "node \"$CLAUDE_PROJECT_DIR/scripts/check.js\"").Verdict
        );
        Assert.Equal(
            Verdict.Ok,
            Classify(f, "python3 ${CLAUDE_PLUGIN_ROOT}/hook.py", pluginRoot: plugin).Verdict
        );
        Assert.Equal(
            Verdict.Unverified,
            Classify(f, "python3 ${CLAUDE_PLUGIN_ROOT}/hook.py").Verdict
        );
    }

    [Theory]
    [InlineData("bash ~/missing.sh")]
    [InlineData("node ~/missing.sh")]
    [InlineData("python ~/missing.sh")]
    [InlineData("npx tsx ~/missing.sh")]
    [InlineData("uv run ~/missing.sh")]
    public void Interpreters_are_judged_by_their_script(string command)
    {
        var f = new DoctorFake();
        foreach (var exe in new[] { "bash", "node", "python", "npx", "uv", "tsx" })
        {
            f.Exe(exe);
        }
        Assert.Equal(
            (Verdict.Broken, $"file not found: {f.At("missing.sh")}"),
            Classify(f, command)
        );
    }

    [Fact]
    public void A_bare_program_is_looked_up_on_path()
    {
        var f = new DoctorFake();
        f.Exe("jq");
        Assert.Equal(Verdict.Ok, Classify(f, "jq .").Verdict);
        Assert.Equal(Verdict.Ok, Classify(f, "echo hi").Verdict);
        Assert.Equal((Verdict.Broken, "`nope` not on PATH"), Classify(f, "nope --x"));
    }

    [Theory]
    [InlineData("jq . | head")]
    [InlineData("echo $(date)")]
    [InlineData("echo `date`")]
    [InlineData("eval \"$CMD\"")]
    [InlineData("a && b")]
    public void Shell_syntax_is_never_guessed_at(string command)
    {
        var f = new DoctorFake();
        var (verdict, detail) = Classify(f, command);
        Assert.Equal(Verdict.Unverified, verdict);
        Assert.EndsWith("cannot be checked without running it", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_script_that_is_not_executable_is_suspect_only_when_run_directly()
    {
        var f = new DoctorFake();
        f.Exe("bash");
        var script = f.At("hooks/plain.sh");
        f.Write(script, "");
        f.Mode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Assert.Equal(Verdict.Suspect, Classify(f, script).Verdict);
        Assert.Equal(Verdict.Ok, Classify(f, $"bash {script}").Verdict);
    }

    [Fact]
    public void A_relative_script_is_only_resolved_for_claude()
    {
        var f = new DoctorFake();
        f.Write(f.InProject("hook.sh"), "");
        f.Exe("bash");
        Assert.Equal(Verdict.Ok, Classify(f, "bash ./hook.sh").Verdict);
        Assert.Equal(Verdict.Unverified, Classify(f, "bash ./hook.sh", agent: "cursor").Verdict);
    }

    [Fact]
    public void Windows_applies_pathext_and_the_cmd_and_powershell_rules()
    {
        var f = new DoctorFake(McpOs.Windows);
        f.Exe("fmt.cmd");
        f.Exe("cmd.exe");
        f.Exe("pwsh.exe");
        f.Write(f.At("hooks/check.ps1"), "");

        Assert.Equal(Verdict.Ok, Classify(f, "fmt --check").Verdict);
        Assert.Equal(Verdict.Ok, Classify(f, "cmd /c fmt").Verdict);
        Assert.Equal(Verdict.Ok, Classify(f, "cmd /c dir").Verdict);
        Assert.Equal((Verdict.Broken, "`gone` not on PATH"), Classify(f, "cmd /c gone"));
        Assert.Equal(Verdict.Ok, Classify(f, $"pwsh -File {f.At("hooks/check.ps1")}").Verdict);
        Assert.Equal(Verdict.Unverified, Classify(f, "pwsh -Command Get-Date").Verdict);
        Assert.Equal(
            Verdict.Ok,
            Classify(
                f,
                @"%USERPROFILE%\hooks\check.ps1".Replace(
                    "%USERPROFILE%",
                    f.Home,
                    StringComparison.Ordinal
                )
            ).Verdict
        );
    }

    [Fact]
    public void Nothing_is_started_while_checking()
    {
        var f = new DoctorFake();
        f.Write(
            f.At(".claude/settings.json"),
            DoctorFake.Hooks(("Stop", null, "nope"), ("Stop", null, "jq . | head"))
        );
        var started = 0;
        // The only probe a check may make is rtok's own MCP server, listed when no entry names it.
        var options = f.Options() with
        {
            Mcp = (server, _) =>
            {
                started += server.Name is "rtok" ? 0 : 1;
                return null;
            },
            Health = (_, _) =>
            {
                started++;
                return null;
            },
        };
        var report = DoctorPage.Page(options);
        Assert.Equal(0, started);
        Assert.Contains(report.Problems!, p => p.Kind is ProblemKinds.BrokenHook && p.Fixable);
        Assert.Contains(report.Problems!, p => p.Kind is ProblemKinds.UnverifiedHook && !p.Fixable);
    }
}
