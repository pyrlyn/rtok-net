using System.Diagnostics;
using System.Text;
using Rtok.Testing;

namespace Rtok.Hooks.Tests.Pipeline;

public sealed class HookPipelineTests
{
    private const int Cap = 8 * 1024 * 1024;

    [Fact]
    public void A_payload_reaches_the_plugins_and_the_reply_is_in_the_host_shape()
    {
        using var box = new PipelineBox();
        using var pipeline = box.Pipeline();

        var reply = box.Run(pipeline, "PreToolUse", Fixtures.Hook("pre_tool_use.json"));

        Assert.Equal(
            """{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"no shell here"}}""",
            reply
        );
        Assert.Equal(1, box.Count("SELECT count(*) FROM calls WHERE name = 'PreToolUse'"));
        Assert.Equal(1, box.Count("SELECT count(*) FROM call_io"));
        Assert.Equal(
            1,
            box.Count(
                "SELECT count(*) FROM sessions WHERE id = '8f1c0a52-2d4e-4b7a-9f61-3c2e5d7a9b10'"
            )
        );
        Assert.Empty(box.ErrorLines());
    }

    [Fact]
    public void Stdin_one_byte_over_the_cap_answers_the_no_op_reply_with_one_line()
    {
        using var box = new PipelineBox();
        using var pipeline = box.Pipeline();
        var over = new byte[Cap + 1];
        over.AsSpan().Fill((byte)'{');

        var reply = box.Run(pipeline, "PreToolUse", over);

        Assert.Equal("{}", reply);
        var line = Assert.Single(box.ErrorLines());
        Assert.Equal(
            $"rtok: hook PreToolUse stdin over core.hook_max_input_bytes ({Cap} bytes, got {Cap + 1}); failing open",
            line
        );
        Assert.False(File.Exists(box.Db));
    }

    [Fact]
    public void Hook_max_input_bytes_moves_the_cap()
    {
        using var box = new PipelineBox("hook_max_input_bytes = 64\n");
        using var pipeline = box.Pipeline();

        var reply = box.Run(pipeline, "PreToolUse", Fixtures.Hook("pre_tool_use.json"));

        Assert.Equal("{}", reply);
        Assert.Contains(
            "(64 bytes, got 65)",
            Assert.Single(box.ErrorLines()),
            StringComparison.Ordinal
        );
        Assert.Equal(0, box.Plugin.Calls);
    }

    [Fact]
    public void The_same_tool_call_twice_runs_the_plugins_once()
    {
        using var box = new PipelineBox();
        using var pipeline = box.Pipeline();
        var payload = Fixtures.Hook("pre_tool_use.json");

        var first = box.Run(pipeline, "PreToolUse", payload);
        var second = box.Run(pipeline, "PreToolUse", payload);

        Assert.Equal(first, second);
        Assert.Equal(1, box.Plugin.Calls);
        Assert.Equal(1, box.Count("SELECT count(*) FROM measurements"));
    }

    // Two processes do not share the reply cache; the once key still keeps one row.
    [Fact]
    public void The_same_tool_call_in_two_processes_writes_one_measurement()
    {
        using var box = new PipelineBox();
        var payload = Fixtures.Hook("pre_tool_use.json");
        using (var one = box.Pipeline())
        {
            box.Run(one, "PreToolUse", payload);
        }
        using (var two = box.Pipeline())
        {
            box.Run(two, "PreToolUse", payload);
        }

        Assert.Equal(2, box.Plugin.Calls);
        Assert.Equal(1, box.Count("SELECT count(*) FROM measurements"));
    }

    [Fact]
    public void An_event_over_max_ms_logs_one_warn_line()
    {
        using var box = new PipelineBox("[hook]\nmax_ms = 10\n");
        using var pipeline = box.Pipeline(new SteppingTime(TimeSpan.FromMilliseconds(25)));

        box.Run(pipeline, "SessionStart", Fixtures.Hook("session_start.json"));

        Assert.Equal(
            "rtok: hook SessionStart slow: 25.0 ms over max_ms 10 ms",
            Assert.Single(box.ErrorLines())
        );
        Assert.Equal(1, box.Count("SELECT count(*) FROM logs WHERE level = 'warn'"));
    }

    [Fact]
    public void Max_ms_zero_logs_no_slow_line()
    {
        using var box = new PipelineBox("[hook]\nmax_ms = 0\n");
        using var pipeline = box.Pipeline(new SteppingTime(TimeSpan.FromSeconds(1)));

        box.Run(pipeline, "SessionStart", Fixtures.Hook("session_start.json"));

        Assert.Empty(box.ErrorLines());
        Assert.Equal(0, box.Count("SELECT count(*) FROM logs"));
    }

    [Fact]
    public void A_locked_store_answers_the_no_op_reply_at_once_and_writes_nothing()
    {
        using var box = new PipelineBox();
        using (var warm = box.Pipeline())
        {
            box.Run(warm, "SessionStart", Fixtures.Hook("session_start.json"));
        }
        var calls = box.Count("SELECT count(*) FROM calls");
        var runs = box.Plugin.Calls;
        using var pipeline = box.Pipeline();
        string reply;
        var clock = Stopwatch.StartNew();
        using (var holder = box.Connect())
        using (var tx = holder.BeginTransaction(deferred: false))
        {
            reply = box.Run(pipeline, "PreToolUse", Fixtures.Hook("pre_tool_use.json"));
            clock.Stop();
            tx.Rollback();
        }

        Assert.Equal("{}", reply);
        Assert.StartsWith(
            "rtok: hook PreToolUse skipped: store locked:",
            Assert.Single(box.ErrorLines()),
            StringComparison.Ordinal
        );
        Assert.Equal(calls, box.Count("SELECT count(*) FROM calls"));
        Assert.Equal(runs, box.Plugin.Calls);
        // One 5 ms attempt; the bound leaves room for a loaded runner.
        Assert.InRange(clock.ElapsedMilliseconds, 0, 500);
    }

    // SessionStart, SubagentStart and PreCompact each land in one write transaction.
    [Theory]
    [InlineData("SessionStart", "session_start.json")]
    [InlineData("SubagentStart", "subagent_start.json")]
    [InlineData("PreCompact", "pre_compact.json")]
    public void An_inject_event_commits_once(string hookEvent, string fixture)
    {
        using var box = new PipelineBox();
        using var pipeline = box.Pipeline();
        box.Run(pipeline, "SessionStart", Fixtures.Hook("session_start.json"));
        var before = pipeline.OpenStore!.Commits;

        box.Run(pipeline, hookEvent, Fixtures.Hook(fixture));

        Assert.Equal(before + 1, pipeline.OpenStore!.Commits);
    }

    [Theory]
    [InlineData("UserPromptSubmit", "user_prompt_submit.json", "prompt context")]
    [InlineData("SubagentStart", "subagent_start.json", "subagent context")]
    [InlineData("PostCompact", "pre_compact.json", "start compact")]
    public void An_inject_event_answers_the_plugins_context(
        string hookEvent,
        string fixture,
        string context
    )
    {
        using var box = new PipelineBox();
        using var pipeline = box.Pipeline();
        // A PostCompact without Claude's summary takes the post-compaction injection.
        var payload = Fixtures
            .Hook(fixture)
            .Replace("\"PreCompact\"", "\"PostCompact\"", StringComparison.Ordinal);

        var reply = box.Run(pipeline, hookEvent, payload);

        Assert.Equal(
            $$$"""{"hookSpecificOutput":{"hookEventName":"{{{hookEvent}}}","additionalContext":"{{{context}}}"}}""",
            reply
        );
    }

    [Fact]
    public void Claudes_post_compact_with_a_summary_answers_the_no_op_reply()
    {
        using var box = new PipelineBox();
        using var pipeline = box.Pipeline();

        var reply = box.Run(pipeline, "PostCompact", Fixtures.Hook("post_compact.json"));

        Assert.Equal("{}", reply);
        Assert.Equal(0, box.Plugin.Calls);
    }

    [Fact]
    public void Session_end_marks_the_session_ended()
    {
        using var box = new PipelineBox();
        using var pipeline = box.Pipeline();

        var reply = box.Run(pipeline, "SessionEnd", Fixtures.Hook("session_end.json"));

        Assert.Equal("{}", reply);
        Assert.Equal(1, box.Count("SELECT count(*) FROM sessions WHERE ended_at IS NOT NULL"));
    }

    [Fact]
    public void A_disabled_rtok_answers_the_no_op_reply_and_writes_nothing()
    {
        using var box = new PipelineBox("enabled = false\n");
        using var pipeline = box.Pipeline();

        var reply = box.Run(pipeline, "PreToolUse", Fixtures.Hook("pre_tool_use.json"));

        Assert.Equal("{}", reply);
        Assert.False(File.Exists(box.Db));
    }

    // The locked SessionEnd hands its payload to a detached rtok, which waits for the lock.
    [Fact]
    public async Task A_session_end_blocked_by_the_lock_lands_through_the_detached_retry()
    {
        var ct = TestContext.Current.CancellationToken;
        var rtok = await PublishedApps.PathAsync("rtok", ct);
        using var box = new PipelineBox();
        using (var warm = box.Pipeline())
        {
            box.Run(warm, "SessionStart", Fixtures.Hook("session_start.json"));
        }
        using var pipeline = box.Pipeline(executable: rtok);
        using (var holder = box.Connect())
        using (var tx = holder.BeginTransaction(deferred: false))
        {
            Assert.Equal("{}", box.Run(pipeline, "SessionEnd", Fixtures.Hook("session_end.json")));
            Assert.Equal(0, box.Count("SELECT count(*) FROM sessions WHERE ended_at IS NOT NULL"));
            await Task.Delay(200, ct);
            tx.Rollback();
        }

        Assert.True(
            await HookSandbox.UntilAsync(
                () => box.Count("SELECT count(*) FROM sessions WHERE ended_at IS NOT NULL") == 1,
                TimeSpan.FromSeconds(20),
                ct
            )
        );
        Assert.Empty(Directory.EnumerateFiles(Path.Join(box.Home, "run"), "session-end-*"));
    }

    [Fact]
    public void A_deferral_spool_outside_run_is_not_read()
    {
        using var box = new PipelineBox();
        var stray = Path.Join(box.Home, "session-end-x.json");
        File.WriteAllText(stray, Fixtures.Hook("session_end.json"));

        Assert.Null(HookDeferral.Take(box.Home, stray));
        Assert.True(File.Exists(stray));
        Assert.Null(HookDeferral.Take(box.Home, Path.Join(box.Home, "run", "other.json")));
    }

    [Fact]
    public void The_resident_reads_no_session_from_its_own_environment()
    {
        using var box = new PipelineBox();
        box.Env["CLAUDE_SESSION_ID"] = "from-env";
        using var pipeline = box.Pipeline();
        var request = new Rtok.Hook.Contracts.HookRequest(
            "v1",
            0,
            "SessionStart",
            "",
            box.Project,
            [],
            0,
            Encoding.UTF8.GetBytes("""{"hook_event_name":"SessionStart","source":"startup"}""")
        );

        pipeline.Handle(request, replay: false);

        Assert.Equal(1, box.Count("SELECT count(*) FROM sessions WHERE id = 'unknown'"));
    }

    private sealed class SteppingTime(TimeSpan step) : TimeProvider
    {
        private long _now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _now += step.Ticks;
    }
}
