namespace Rtok.Model.Tests;

public sealed class BuilderTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public void Builds_off_the_caller_thread_and_collapses_requests_during_a_build()
    {
        using var started = new SemaphoreSlim(0);
        using var gate = new ManualResetEventSlim();
        var threads = new List<int>();
        var builder = new SnapshotBuilder<string>(() =>
        {
            lock (threads)
            {
                threads.Add(Environment.CurrentManagedThreadId);
            }
            started.Release();
            gate.Wait(Wait);
            return "built";
        });

        builder.Refresh();
        Assert.True(started.Wait(Wait, TestContext.Current.CancellationToken));
        for (var i = 0; i < 10; i++)
        {
            builder.Refresh();
        }
        Assert.Equal(1, builder.Builds);
        gate.Set();

        Assert.True(SpinWait.SpinUntil(() => !builder.IsBuilding, Wait));
        Assert.Equal(2, builder.Builds);
        Assert.DoesNotContain(Environment.CurrentManagedThreadId, threads);
    }

    [Fact]
    public void A_reader_gets_the_last_complete_snapshot_while_a_build_runs()
    {
        using var started = new SemaphoreSlim(0);
        using var gate = new ManualResetEventSlim(initialState: true);
        var n = 0;
        var builder = new SnapshotBuilder<string>(() =>
        {
            started.Release();
            gate.Wait(Wait);
            return $"snapshot {++n}";
        });
        builder.Refresh();
        Assert.True(started.Wait(Wait, TestContext.Current.CancellationToken));
        Assert.True(SpinWait.SpinUntil(() => !builder.IsBuilding, Wait));
        gate.Reset();

        builder.Refresh();
        Assert.True(started.Wait(Wait, TestContext.Current.CancellationToken));
        Assert.True(builder.IsBuilding);
        Assert.Equal("snapshot 1", builder.Current); // returns while the build is held
        gate.Set();

        Assert.True(SpinWait.SpinUntil(() => !builder.IsBuilding, Wait));
        Assert.Equal("snapshot 2", builder.Current);
    }

    [Fact]
    public void A_failing_build_keeps_the_last_snapshot_and_records_the_error()
    {
        var fail = false;
        var builder = new SnapshotBuilder<string>(() =>
            fail ? throw new InvalidOperationException("store is gone") : "ok"
        );
        builder.Refresh();
        Assert.True(SpinWait.SpinUntil(() => !builder.IsBuilding, Wait));
        fail = true;
        builder.Refresh();
        Assert.True(SpinWait.SpinUntil(() => !builder.IsBuilding, Wait));

        Assert.Equal("ok", builder.Current);
        Assert.Equal("store is gone", builder.Error);
    }

    [Fact]
    public void A_config_reload_during_a_build_gives_the_old_config_or_the_new_never_a_mix()
    {
        using var fx = new ModelFixture();
        var held = new CountingSource("held");
        var snapshots = new List<Snapshot>();
        var model = fx.Model(
            fx.Sources() with
            {
                Overview = new HeldOverview(fx.Sources().Overview, held),
            },
            built: s =>
            {
                lock (snapshots)
                {
                    snapshots.Add(s);
                }
            }
        );
        held.Gate.Reset();
        model.Refresh();
        Assert.True(SpinWait.SpinUntil(() => held.Reads == 1, Wait));

        File.WriteAllText(fx.UserFile, "[proxy]\nport = 1002\n[otel]\nservice_name = \"new\"\n");
        model.Reload();
        held.Gate.Set();

        Assert.True(SpinWait.SpinUntil(() => snapshots.Count == 2, Wait));
        Assert.Equal(("1001", null), Pair(snapshots[0]));
        Assert.Equal(("1002", "new"), Pair(snapshots[1]));
    }

    private static (string?, string?) Pair(Snapshot s) =>
        (
            s.Config.Single(r => r is { Key: "proxy.port" }).Value,
            s.Config.SingleOrDefault(r => r is { Key: "otel.service_name", Value: "new" })?.Value
        );

    private sealed class HeldOverview(IPageSource<OverviewPage> inner, CountingSource held)
        : IPageSource<OverviewPage>
    {
        public OverviewPage Read(ModelContext context)
        {
            held.Read(context);
            return inner.Read(context);
        }
    }
}
