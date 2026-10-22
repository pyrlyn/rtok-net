namespace Rtok.Store.Tests;

public sealed class LedgerTests
{
    [Fact]
    public void Recent_hook_inputs_are_this_session_newest_first_with_their_session_fields()
    {
        using var h = new StoreHarness();
        h.Store.UpsertSession("s", null, null, null, null);
        h.Store.UpsertSession("other", null, null, null, null);
        (string Session, string Tool)[] calls = [("s", "Bash"), ("other", "Read"), ("s", "Grep")];
        foreach (var (session, tool) in calls)
        {
            var call = h.Store.InsertCall(session, "hook", "hook", name: "PreToolUse");
            h.Store.InsertHookCallIo(
                call,
                System.Text.Encoding.UTF8.GetBytes(
                    $$"""{"session_id":"{{session}}","hook_event_name":"PreToolUse","tool_name":"{{tool}}"}"""
                ),
                null
            );
        }
        h.Store.InsertCall("s", "mcp", "tool");

        var inputs = h.Store.RecentHookInputs("s", 10);

        Assert.Equal(
            [
                """{"session_id":"s","hook_event_name":"PreToolUse","tool_name":"Grep"}""",
                """{"session_id":"s","hook_event_name":"PreToolUse","tool_name":"Bash"}""",
            ],
            inputs
        );
        Assert.Single(h.Store.RecentHookInputs("s", 1));
    }

    [Fact]
    public void Calls_since_counts_this_session_from_a_time_on()
    {
        using var h = new StoreHarness();
        h.Store.UpsertSession("s", null, null, null, null);
        h.Store.InsertCall("s", "hook", "hook");
        var mark = h.Clock.GetUtcNow().ToUnixTimeSeconds() + 60;
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        h.Store.InsertCall("s", "hook", "hook");
        h.Store.InsertCall("s", "mcp", "tool");

        Assert.Equal(2, h.Store.CallsSince("s", mark));
        Assert.Equal(0, h.Store.CallsSince("other", 0));
    }

    [Fact]
    public void Last_measurement_ref_is_the_newest_of_that_plugin_and_kind()
    {
        using var h = new StoreHarness();
        var m = Fakes.Measurement().Generate();
        MeasurementInput Row(string kind, string refId) =>
            m with
            {
                Plugin = "memory",
                Kind = kind,
                RefId = refId,
            };
        h.Store.InsertMeasurements(
            "s",
            [Row("prompt_recall", "old"), Row("prompt_recall", "new"), Row("recall", "other kind")]
        );

        Assert.Equal("new", h.Store.LastMeasurementRef("s", "memory", "prompt_recall"));
        Assert.Null(h.Store.LastMeasurementRef("s", "inject", "inject"));
    }

    [Fact]
    public void End_session_sets_ended_at_and_keeps_what_was_known()
    {
        using var h = new StoreHarness();
        var claude = h.Store.HostId("claude");
        var f = Fakes.Values();
        var project = f.Lorem.Word();
        h.Store.UpsertSession("s", claude, project, f.System.DirectoryPath(), "startup");
        h.Clock.Advance(TimeSpan.FromMinutes(5));

        h.Store.EndSession("s", null);
        h.Store.EndSession("unseen", claude);

        using var db = h.Read();
        var s = db.Sessions.Single(x => x.Id == "s");
        Assert.Equal(s.StartedAt + 300, s.EndedAt);
        Assert.Equal(claude, s.HostId);
        Assert.Equal(project, s.Project);
        Assert.NotNull(db.Sessions.Single(x => x.Id == "unseen").EndedAt);
    }
}
