namespace Rtok.Store.Tests;

public sealed class UsageReadTests
{
    [Fact]
    public void Rows_of_one_request_are_summed_and_carry_their_session_host()
    {
        using var h = new StoreHarness();
        var store = h.Store;
        store.UpsertSession("s", store.HostId("claude"), null, null, "hook");
        store.UpsertSession("p", null, null, null, "proxy");
        var call = store.InsertCall("s", "proxy", "api");
        store.InsertUsage("s", "m", "anthropic", 1, 2, 3, 4, call);
        store.InsertUsage("s", "m", "anthropic", 10, 0, 0, 1, call);
        store.InsertUsage("p", null, "openai", 5, 0, 0, 5, call);
        h.Clock.Advance(TimeSpan.FromSeconds(10));
        store.InsertUsage("s", "m", "anthropic", 7, 0, 0, 0, call);

        var t0 = 1_800_000_000;
        Assert.Equal(
            [
                new UsageRead("claude", "anthropic", "s", "m", t0, 11, 2, 3, 5),
                new UsageRead("claude", "anthropic", "s", "m", t0 + 10, 7, 0, 0, 0),
                new UsageRead(null, "openai", "p", null, t0, 5, 0, 0, 5),
            ],
            store.UsageSlices(t0, t0 + 11)
        );
        Assert.Equal(2, store.UsageSlices(t0, t0 + 10).Count);
        Assert.Empty(store.UsageSlices(t0 + 11, long.MaxValue));
    }

    [Fact]
    public void Savings_are_net_per_host_in_the_window()
    {
        using var h = new StoreHarness();
        var store = h.Store;
        store.UpsertSession("s", store.HostId("codex"), null, null, "hook");
        store.InsertMeasurement("s", new MeasurementInput("dedup", "dedup", 0, 0, 100, 40));
        store.InsertMeasurement("s", new MeasurementInput("expand", "expand", 0, 0, 0, 15));
        store.InsertMeasurement("orphan", new MeasurementInput("dedup", "dedup", 0, 0, 9, 2));
        h.Clock.Advance(TimeSpan.FromSeconds(5));
        store.InsertMeasurement("s", new MeasurementInput("dedup", "dedup", 0, 0, 1000, 0));

        var t0 = 1_800_000_000;
        Assert.Equal([(null, 7L), ("codex", 45L)], store.MeasurementSavedByHost(t0, t0 + 5));
        Assert.Equal([("codex", 1000L)], store.MeasurementSavedByHost(t0 + 5, t0 + 6));
    }
}
