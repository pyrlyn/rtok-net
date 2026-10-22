using Rtok.Tasks.Disk;
using Rtok.Tasks.GitHub;
using Rtok.Testing;

namespace Rtok.Tasks.Tests;

public sealed class TaskSyncTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly FixedClock _clock = new(10_000);

    public void Dispose() => _temp.Dispose();

    private TaskProject Disk(MemoryTaskStore store) =>
        new(
            new DiskTaskAdapter(_temp.Combine("tasks"), _clock),
            store,
            "github.com/me/app",
            "s",
            _clock
        );

    [Fact]
    public async Task Sync_raises_a_lost_counter_and_reports_the_drift()
    {
        var first = Disk(new MemoryTaskStore());
        await first.CreateAsync(new NewPlanTask("One"), Ct);
        await first.CreateAsync(new NewPlanTask("Two"), Ct);
        await first.CreateAsync(new NewPlanTask("Leaf", "", TaskId.Parse("S1")), Ct);
        var project = Disk(new MemoryTaskStore());

        var report = await project.SyncAsync(Ct);

        Assert.Equal(
            "disk: next id S3\nraised top level: 0 -> 2\nraised subtasks of S1: 0 -> 1\nabove the counter: S1, S2, S1.1\n",
            TaskText.Sync(report)
        );
        Assert.Equal(
            """{"adapter":"disk","counter":2,"next":"S3","raised":[{"parent":null,"from":0,"to":2},{"parent":"S1","from":0,"to":1}],"above_counter":["S1","S2","S1.1"],"duplicates":[],"unlabelled":[]}""",
            TaskJson.Render(w => TaskJson.Write(w, report), indented: false)
        );
        Assert.Equal(
            "disk: next id S3, counters in step\n",
            TaskText.Sync(await project.SyncAsync(Ct))
        );
    }

    [Fact]
    public async Task A_counter_ahead_of_the_adapter_is_never_lowered()
    {
        var store = new MemoryTaskStore();
        var project = Disk(store);
        await project.CreateAsync(new NewPlanTask("One"), Ct);
        await new TaskAllocator(store).SeedAsync("github.com/me/app", null, 9, Ct);

        var report = await project.SyncAsync(Ct);

        Assert.Equal((9u, "S10"), (report.Counter, report.Next.ToString()));
        Assert.Equal("disk: next id S10, counters in step\n", TaskText.Sync(report));
    }

    [Fact]
    public void Duplicates_and_strays_are_named()
    {
        var report = new SyncReport(
            "github",
            4,
            TaskId.Parse("A5"),
            [],
            [],
            [TaskId.Parse("A2")],
            [
                new StrayIssue("A5. Lost", "https://x/5", TaskId.Parse("A5")),
                new StrayIssue("Untitled", "https://x/6", null),
            ]
        );
        Assert.Equal(
            "github: next id A5\nduplicate ids: A2\nno id label: A5. Lost (label rtok:A5 is gone) https://x/5\nno id label: Untitled https://x/6\n",
            TaskText.Sync(report)
        );
    }

    [Fact]
    public async Task GitHub_sync_only_reads()
    {
        using var fake = new FakeService();
        fake.On(
            "GET",
            "/repos/me/app/issues",
            GitHubJson.Issues(
                GitHubJson.Issue(3, "R3. Shipped", ["rtok", "rtok:R3"], state: "closed"),
                GitHubJson.Issue(4, "R4. Lost", ["rtok"]),
                GitHubJson.Issue(5, "R5. A pull", ["rtok", "rtok:R5"], pull: true)
            ),
            query: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["labels"] = "rtok",
                ["state"] = "all",
            }
        );
        var github = await GitHubTaskAdapter.OpenAsync("me/app", 0, fake.Context, fake.Url, Ct);
        var project = new TaskProject(
            github,
            new MemoryTaskStore(),
            "github.com/me/app",
            "r",
            _clock
        );

        var report = await project.SyncAsync(Ct);

        Assert.Equal(
            "github: next id R4\nraised top level: 0 -> 3\nabove the counter: R3\nno id label: R4. Lost (label rtok:R4 is gone) https://github.com/me/app/issues/4\n",
            TaskText.Sync(report)
        );
        Assert.Equal(["GET", "GET"], fake.Calls.Select(c => c.Method), StringComparer.Ordinal);
    }
}
