using System.Text.Json;
using CsCheck;
using Rtok.Config;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>The PreToolUse(Bash) rewrite table: what is wrapped in <c>rtok run</c> and every never-wrap rule.</summary>
public sealed class BashRewriteTests
{
    private static readonly CmdSection Defaults = new();

    private static PreToolDecision? Decide(
        string command,
        string? agentId = null,
        bool windows = false
    ) => BashRewrite.PreTool(Event(command), agentId, Defaults, windows);

    private static PreToolUse Event(string command, string tool = "Bash") =>
        new(
            tool,
            JsonSerializer.SerializeToElement(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["command"] = command,
                    ["description"] = "t",
                },
                TestJson.Default.DictionaryStringString
            )
        );

    private static string Wrapped(PreToolDecision? decision)
    {
        var rewrite = Assert.IsType<PreToolDecision.Rewrite>(decision);
        Assert.Equal("wrapped by rtok", rewrite.Reason);
        Assert.Equal("t", rewrite.Input["description"]!.GetValue<string>());
        return rewrite.Input["command"]!.GetValue<string>();
    }

    private static bool Skips(bool windows, string cmd) =>
        BashRewrite.SkipWrap(windows, cmd, [], Defaults.InteractiveStems);

    [Fact]
    public void Git_status_is_wrapped() =>
        Assert.Equal("rtok run -- 'git status'", Wrapped(Decide("git status")));

    [Theory]
    [InlineData("cat <<EOF")]
    [InlineData("sudo ls")]
    [InlineData("Sudo ls")]
    [InlineData("Sudo.exe ls")]
    [InlineData("SUDO.EXE ls")]
    [InlineData("sudo.exe ls")]
    [InlineData("RTOK.EXE run -- true")]
    [InlineData("rtok run -- 'git status'")]
    [InlineData("rtok run --agent a1 -- 'git status'")]
    [InlineData(@"C:\Windows\System32\sudo.exe ls")]
    [InlineData(@"C:\tools\rtok.exe run -- true")]
    [InlineData("sleep 10 &")]
    [InlineData("sleep 10&")]
    [InlineData("a & b")]
    [InlineData("python -i")]
    [InlineData("docker run -i alpine sh")]
    [InlineData("ffmpeg --interactive x")]
    public void Never_wrap_rules_leave_the_command_alone(string cmd) => Assert.Null(Decide(cmd));

    [Theory]
    [InlineData("true && false")]
    [InlineData("git status | head")]
    [InlineData("foo; bar")]
    [InlineData("echo x >/tmp/x")]
    [InlineData("sleep 90; cat /tmp/tasks/x.output | tail -30")]
    public void Metacharacters_stay_inside_one_quoted_argument(string cmd) =>
        Assert.Equal("rtok run -- " + Shells.WrapQuote(cmd, windows: false), Wrapped(Decide(cmd)));

    [Fact]
    public void A_normal_command_with_a_windows_path_is_wrapped() =>
        Assert.NotNull(Decide(@"C:\Program Files\Git\cmd\git.exe status"));

    // The host keeps the shell's cwd between calls: a leading `cd` runs in the host shell.
    [Theory]
    [InlineData("cd crates/rtok && cargo test", null, "cd crates/rtok && rtok run -- 'cargo test'")]
    [InlineData(
        "cd \"a && b\" && cd c && git status | head",
        null,
        "cd \"a && b\" && cd c && rtok run -- 'git status | head'"
    )]
    [InlineData("cd x && ls", "sub_1", "cd x && rtok run --agent sub_1 -- 'ls'")]
    [InlineData("cd x && cargo test", null, "cd x && rtok run -- 'cargo test'")]
    [InlineData("A=1 cargo test", null, "rtok run -- 'A=1 cargo test'")]
    [InlineData("cargo test", "agent-a1", "rtok run --agent agent-a1 -- 'cargo test'")]
    [InlineData("git status", null, "rtok run -- 'git status'")]
    [InlineData("cd /repo && git status", null, "cd /repo && rtok run -- 'git status'")]
    public void Rewrites(string cmd, string? agentId, string want) =>
        Assert.Equal(want, Wrapped(Decide(cmd, agentId)));

    // Builtins whose effect outlives the command would be lost in `rtok run`'s child shell.
    [Theory]
    [InlineData("cd /repo")]
    [InlineData("cd /repo; ls")]
    [InlineData("ls && cd /repo")]
    [InlineData("cd a && cargo test && cd b")]
    [InlineData("export A=1")]
    [InlineData("export A=1 && cargo test")]
    [InlineData("source .venv/bin/activate && pytest")]
    [InlineData(". ./env.sh")]
    [InlineData("unset A")]
    [InlineData("pushd x && ls")]
    [InlineData("popd")]
    [InlineData("alias g=git")]
    [InlineData("set -e")]
    [InlineData("shopt -s globstar")]
    [InlineData("umask 022")]
    [InlineData("trap 'rm x' EXIT")]
    [InlineData("declare -x A=1")]
    [InlineData("typeset A=1")]
    [InlineData("readonly A=1")]
    [InlineData("A=1")]
    [InlineData("A=1 B=2 && cargo test")]
    [InlineData("A='x y'")]
    public void Shell_state_builtins_are_not_wrapped(string cmd) => Assert.Null(Decide(cmd));

    [Theory]
    [InlineData("git checkout -b export")]
    [InlineData("echo a=b")]
    public void A_builtin_name_as_an_argument_still_wraps(string cmd) =>
        Assert.NotNull(Decide(cmd));

    // The skip rules also apply to the command behind `cd` hops.
    [Theory]
    [InlineData("cd x && rtok expand abc")]
    [InlineData("cd x && cd y && rtok expand abc")]
    [InlineData("cd x && sudo ls")]
    [InlineData("cd x && python -i")]
    [InlineData("cd x && sleep 10 &")]
    public void Skip_rules_apply_behind_cd_hops(string cmd) => Assert.Null(Decide(cmd));

    // An escaped quote must not hide a later `cd` from the state check.
    [Fact]
    public void Escaped_quote_does_not_hide_a_cd()
    {
        Assert.Null(Decide(@"echo it\'s && cd sub"));
        Assert.Null(Decide("echo \"a\\\" b\" && cd sub"));
        Assert.NotNull(Decide(@"echo it\'s fine"));
        Assert.Null(Decide(@"echo it\'s fine", windows: true));
    }

    // On Windows the PowerShell `''` form would lose the apostrophe if the rewrite reached a
    // POSIX shell, so every apostrophe command stays whole there.
    [Theory]
    [InlineData("echo it's fine")]
    [InlineData("git commit -m 'fix it'")]
    [InlineData("jq '.' data.json")]
    public void Windows_apostrophe_commands_stay_unwrapped(string cmd)
    {
        Assert.True(Skips(windows: true, cmd));
        Assert.Null(Decide(cmd, windows: true));
    }

    [Theory]
    [InlineData("echo it's fine")]
    [InlineData("git commit -m 'fix it'")]
    public void Posix_apostrophe_commands_wrap_and_round_trip(string cmd)
    {
        Assert.False(Skips(windows: false, cmd));
        Assert.Equal([cmd], ShWords(Wrapped(Decide(cmd))["rtok run -- ".Length..]));
    }

    [Fact]
    public void Windows_wrap_uses_powershell_quotes() =>
        Assert.Equal("rtok run -- 'git status'", Wrapped(Decide("git status", windows: true)));

    [Fact]
    public void Ps_quoting_does_not_round_trip_under_sh()
    {
        const string cmd = "echo it's fine";
        var psForm = Shells.WrapQuote(cmd, windows: true);
        Assert.Equal(["echo its fine"], ShWords(psForm));
        Assert.Equal([cmd], ShWords(Shells.ShQuote(cmd)));
    }

    [Theory]
    [InlineData("ffmpeg -i x", false)]
    [InlineData("ssh -i key host", false)]
    [InlineData("python -i", true)]
    [InlineData("node -i", true)]
    [InlineData("psql -i", true)]
    [InlineData("sqlite3 -i", true)]
    [InlineData("irb -i", true)]
    [InlineData("bash -i", true)]
    [InlineData("sh -i", true)]
    [InlineData("zsh -i", true)]
    [InlineData("docker exec -i box sh", true)]
    [InlineData("docker run -i alpine sh", true)]
    [InlineData("docker logs -i x", false)]
    [InlineData("kubectl exec -i pod -- sh", true)]
    [InlineData("kubectl get -i x", false)]
    [InlineData("ffmpeg --interactive x", true)]
    public void Per_stem_interactive_i_table(string cmd, bool skip)
    {
        Assert.Equal(skip, Skips(windows: false, cmd));
        Assert.Equal(skip, Decide(cmd) is null);
    }

    // A shape outside `[A-Za-z0-9_-]{1,64}` is dropped, never embedded.
    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("")]
    [InlineData("quote'here")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Malformed_agent_id_is_dropped(string bad) =>
        Assert.Equal("rtok run -- 'cargo test'", Wrapped(Decide("cargo test", bad)));

    // An isolated sub-agent's host guard refuses a wrapped git command.
    [Theory]
    [InlineData("git status")]
    [InlineData("cd /repo/.claude/worktrees/agent-a1 && git status")]
    [InlineData("/usr/bin/git add -A")]
    [InlineData("git -C /repo/.claude/worktrees/agent-a1 commit -m x")]
    [InlineData("cargo test && git status")]
    [InlineData(@"C:\Program Files\Git\cmd\git.exe status")]
    [InlineData("bash -c 'git status'")]
    [InlineData("echo $(git rev-parse HEAD)")]
    [InlineData("env GIT_DIR=x GIT_PAGER=cat git log")]
    public void Sub_agent_git_commands_are_never_wrapped(string cmd) =>
        Assert.Null(Decide(cmd, "agent-a1"));

    [Theory]
    [InlineData("cargo test")]
    [InlineData("cargo test github")]
    [InlineData("ls my_git digit")]
    public void Sub_agent_non_git_commands_stay_wrapped(string cmd) =>
        Assert.Equal($"rtok run --agent agent-a1 -- '{cmd}'", Wrapped(Decide(cmd, "agent-a1")));

    [Fact]
    public void Other_tools_and_shapes_are_left_alone()
    {
        Assert.Null(
            BashRewrite.PreTool(Event("git status", tool: "Read"), null, Defaults, windows: false)
        );
        var noCommand = new PreToolUse(
            "Bash",
            JsonSerializer.SerializeToElement(1, TestJson.Default.Int32)
        );
        Assert.Null(BashRewrite.PreTool(noCommand, null, Defaults, windows: false));
    }

    [Fact]
    public void The_plugin_reads_rewrite_and_never_wrap_from_its_section()
    {
        var plugin = new CmdPlugin();
        Assert.NotNull(plugin.OnPreTool(Event("git status"), new Ctx(new MemoryHost())));
        var off = new MemoryHost("""{"plugins": {"cmd": {"rewrite": false}}}""");
        Assert.Null(plugin.OnPreTool(Event("git status"), new Ctx(off)));
        var never = new MemoryHost("""{"plugins": {"cmd": {"never_wrap": ["git"]}}}""");
        Assert.Null(plugin.OnPreTool(Event("git status"), new Ctx(never)));
        Assert.NotNull(plugin.OnPreTool(Event("sudo ls"), new Ctx(never)));
        Assert.Null(plugin.OnPreTool(Event("git status"), new Ctx(new MemoryHost(), "agent-a1")));
    }

    // The output is either the command as is or `rtok run -- ` and the command quoted once.
    [Fact]
    public void The_rewrite_wraps_once_or_not_at_all_and_never_throws() =>
        Gen.String.Sample(cmd =>
        {
            var decision = Decide(cmd);
            if (decision is not PreToolDecision.Rewrite rewrite)
            {
                Assert.Null(decision);
                return;
            }

            var wrapped = rewrite.Input["command"]!.GetValue<string>();
            var at = wrapped.IndexOf("rtok run -- ", StringComparison.Ordinal);
            Assert.True(at >= 0, wrapped);
            Assert.Equal(-1, wrapped.IndexOf("rtok run -- ", at + 1, StringComparison.Ordinal));
            Assert.StartsWith(wrapped[..at], cmd, StringComparison.Ordinal);
            Assert.Equal([cmd[at..]], ShWords(wrapped[(at + "rtok run -- ".Length)..]));
        });

    // POSIX sh word splitting, quotes only: enough to show which quoting round-trips.
    private static List<string> ShWords(string input)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        var inWord = false;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '\'')
            {
                inWord = true;
                while (++i < input.Length && input[i] != '\'')
                {
                    word.Append(input[i]);
                }
            }
            else if (c == '"')
            {
                inWord = true;
                while (++i < input.Length && input[i] != '"')
                {
                    word.Append(input[i] == '\\' && i + 1 < input.Length ? input[++i] : input[i]);
                }
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    inWord = false;
                }
            }
            else
            {
                inWord = true;
                word.Append(c);
            }
        }

        if (inWord)
        {
            words.Add(word.ToString());
        }

        return words;
    }
}
