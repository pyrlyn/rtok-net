using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Tasks.Tests;

public sealed class StoreTaskStoreTests : IDisposable
{
    private readonly TempDir _dir = new("rtok-task-store-");

    public void Dispose() => _dir.Dispose();

    private RtokStore Open() => RtokStore.Open(new StoreOptions(_dir.Combine("rtok.db")));

    [Fact]
    public async Task Eight_connections_hand_out_every_number_once()
    {
        using (Open()) { }
        const int Writers = 8;
        const int Each = 25;
        var ids = new System.Collections.Concurrent.ConcurrentBag<TaskId>();
        await Task.WhenAll(
            Enumerable
                .Range(0, Writers)
                .Select(_ =>
                    Task.Run(
                        async () =>
                        {
                            using var store = Open();
                            var allocator = new TaskAllocator(new StoreTaskStore(store));
                            for (var i = 0; i < Each; i++)
                            {
                                ids.Add(
                                    await allocator.AllocateAsync("github.com/x/y", "R", null, Ct)
                                );
                            }
                        },
                        Ct
                    )
                )
        );

        Assert.Equal(
            Enumerable.Range(1, Writers * Each).Select(n => (uint)n),
            ids.Select(id => id.Path[0]).Order()
        );
    }

    [Fact]
    public async Task Claims_and_counters_round_trip_through_the_store()
    {
        using var store = Open();
        var tasks = new StoreTaskStore(store);
        var allocator = new TaskAllocator(tasks);
        Assert.Equal("R1", (await allocator.AllocateAsync("p", "r", null, Ct)).ToString());
        Assert.Equal(
            "R1.1",
            (await allocator.AllocateAsync("p", "R", TaskId.Parse("R1"), Ct)).ToString()
        );
        Assert.Equal(40u, await allocator.SeedAsync("p", null, 40, Ct));
        Assert.Equal(40u, await allocator.SeedAsync("p", null, 7, Ct));

        await tasks.UpsertClaimAsync("p", TaskId.Parse("R1"), "a1", "One", Ct);
        Assert.Equal(TaskId.Parse("R1"), (await tasks.LatestClaimAsync("p", "a1", Ct))?.Id);
        await tasks.ClearClaimAsync("p", TaskId.Parse("R1"), Ct);
        Assert.Null(await tasks.LatestClaimAsync("p", "a1", Ct));
        Assert.Null(await tasks.AgentLastSeenAsync("a1", Ct));
        Assert.Null(await tasks.ResolveAgentAsync("a1", Ct));
    }
}
