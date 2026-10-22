using Rtok.Store.Model;

namespace Rtok.Store.Tests;

public sealed class TaskTests
{
    [Fact]
    public void Counters_are_kept_per_project_and_parent()
    {
        using var h = new StoreHarness();
        long Next(string project, string parent) =>
            h.Store.WithTaskCounters(
                project,
                c =>
                {
                    var next = c.Last(parent) + 1;
                    c.SetLast(parent, next);
                    return next;
                }
            );

        Assert.Equal(1, Next("p", ""));
        Assert.Equal(2, Next("p", ""));
        Assert.Equal(1, Next("q", ""));
        Assert.Equal(1, Next("p", "2"));
        Assert.Equal(3, Next("p", ""));
        Assert.Equal((1L, 0L), h.Store.WithTaskCounters("p", c => (c.Last("2"), c.Last("9"))));
    }

    [Fact]
    public void A_failed_counter_transaction_writes_nothing()
    {
        using var h = new StoreHarness();
        Assert.Throws<InvalidOperationException>(() =>
            h.Store.WithTaskCounters<long>(
                "p",
                c =>
                {
                    c.SetLast("", 7);
                    throw new InvalidOperationException("refused");
                }
            )
        );

        Assert.Equal(0, h.Store.WithTaskCounters("p", c => c.Last("")));
    }

    [Fact]
    public void The_latest_claim_is_the_last_one_written()
    {
        using var h = new StoreHarness();
        h.Store.UpsertTaskClaim("p", "R1", "a1", "One");
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        h.Store.UpsertTaskClaim("p", "R2", "a1", "Two");
        h.Store.UpsertTaskClaim("q", "R9", "a1", "Elsewhere");
        h.Store.UpsertTaskClaim("p", "R3", "a2", "Theirs");
        Assert.Equal("R2", h.Store.LatestTaskClaim("p", "a1")?.TaskId);

        h.Store.UpsertTaskClaim("p", "R1", "a1", "One again");
        var latest = h.Store.LatestTaskClaim("p", "a1");
        Assert.Equal(("R1", "One again"), (latest?.TaskId, latest?.Title));

        h.Store.ClearTaskClaim("p", "R1");
        h.Store.ClearTaskClaim("p", "R7");
        Assert.Equal("R2", h.Store.LatestTaskClaim("p", "a1")?.TaskId);
        Assert.Equal(1_800_000_001, h.Store.LatestTaskClaim("p", "a1")?.Since);
        Assert.Null(h.Store.LatestTaskClaim("p", "a3"));
    }

    [Fact]
    public void Agents_resolve_by_full_or_unique_short_id()
    {
        using var h = new StoreHarness();
        _ = h.Store.HostId("claude");
        using (var db = h.Read())
        {
            db.Agents.AddRange(Agent("5691aa16-1", "s1", 100), Agent("5691bb00-2", "s2", 200));
            db.SaveChanges();
        }

        Assert.Equal("5691aa16-1", h.Store.ResolveAgent("5691aa16-1"));
        Assert.Equal("5691aa16-1", h.Store.ResolveAgent("5691aa"));
        Assert.Null(h.Store.ResolveAgent("5691"));
        Assert.Null(h.Store.ResolveAgent("ffff"));
        Assert.Null(h.Store.ResolveAgent(""));
        Assert.Equal(200, h.Store.AgentLastSeen("5691bb00-2"));
        Assert.Null(h.Store.AgentLastSeen("5691bb00"));
    }

    private static Agent Agent(string id, string session, long seen) =>
        new()
        {
            Id = id,
            HostId = 1,
            HostSessionId = session,
            StartedAt = seen,
            LastSeen = seen,
        };
}
