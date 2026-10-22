using Rtok.Tasks.Disk;
using Rtok.Testing;

namespace Rtok.Tasks.Tests;

public sealed class TaskProjectTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly FixedClock _clock = new(10_000);
    private readonly MemoryTaskStore _store = new();
    private readonly TaskProject _project;

    public TaskProjectTests()
    {
        _project = new TaskProject(
            new DiskTaskAdapter(_temp.Combine("tasks"), _clock),
            _store,
            "github.com/me/app",
            "a",
            _clock
        );
        _store.Agents["agent-1"] = 10_000;
        _store.Agents["agent-2"] = 10_000;
    }

    public void Dispose() => _temp.Dispose();

    private static TaskId Id(string s) => TaskId.Parse(s);

    private Task<PlanTask> CreateAsync(string title, string? parent = null) =>
        _project.CreateAsync(new NewPlanTask(title, "", parent is null ? null : Id(parent)), Ct);

    [Fact]
    public async Task Create_numbers_past_tasks_written_elsewhere()
    {
        _temp.Write(
            "tasks/A7 - pulled.md",
            "---\nid: A7\ntitle: Pulled\nstatus: open\ncreated_at: 1\nupdated_at: 1\n---\n"
        );
        Assert.Equal("A8", (await CreateAsync("First")).Id.ToString());
        Assert.Equal("A7.1", (await CreateAsync("Leaf", "A7")).Id.ToString());
        Assert.Equal("A7.2", (await CreateAsync("Leaf 2", "a7")).Id.ToString());
        var e = await Assert.ThrowsAsync<TaskException>(() => CreateAsync("Orphan", "A9"));
        Assert.Equal("rtok task: no parent task A9", e.Message);
    }

    [Fact]
    public async Task Ids_are_never_reused_after_a_file_is_deleted()
    {
        await CreateAsync("One");
        var two = await CreateAsync("Two");
        File.Delete(_temp.Combine("tasks", "A2 - two.md"));
        Assert.Equal("A3", (await CreateAsync("Three")).Id.ToString());
        Assert.Equal("A2", two.Id.ToString());
    }

    [Fact]
    public async Task A_parent_cannot_finish_before_its_subtasks_unless_forced()
    {
        await CreateAsync("Parent");
        await CreateAsync("Child", "A1");
        await CreateAsync("Child 2", "A1");
        var e = await Assert.ThrowsAsync<TaskException>(() =>
            _project.StatusAsync(Id("A1"), TaskState.Done, false, Ct)
        );
        Assert.Equal(
            "A1 has open subtasks: A1.1, A1.2; finish them first or pass --force",
            e.Message
        );
        await _project.StatusAsync(Id("A1"), TaskState.Done, true, Ct);
        await _project.StatusAsync(Id("A1"), TaskState.Open, false, Ct);
        Assert.Equal(
            "A1.1 A1.2",
            string.Join(' ', (await _project.GetAsync(Id("A1"), Ct)).Subtasks)
        );
    }

    [Fact]
    public async Task Next_takes_leaves_first_and_by_priority()
    {
        await CreateAsync("Parent");
        await CreateAsync("Child", "A1");
        await CreateAsync("Other");
        Assert.Equal("A1.1", (await _project.NextAsync(Ct))!.Id.ToString());
        await _project.SetPriorityAsync(Id("A2"), 0, Ct);
        Assert.Equal("A2", (await _project.NextAsync(Ct))!.Id.ToString());
        Assert.Equal(
            "priority 5 is outside 0–4",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.SetPriorityAsync(Id("A2"), 5, Ct)
                )
            ).Message
        );
    }

    [Fact]
    public async Task Claims_bind_once_and_refuse_others()
    {
        await CreateAsync("First");
        await CreateAsync("Second");
        await _project.BlockAsync(Id("A2"), Id("A1"), Ct);
        var claim = await _project.ClaimAsync(null, "agent-1", Ct);
        Assert.True(claim.Changed);
        Assert.Equal(TaskState.InProgress, claim.Task.Status);
        Assert.Equal("agent-1", claim.Task.Assignee);
        var path = _temp.Combine("tasks", "A1 - first.md");
        var bytes = await File.ReadAllTextAsync(path, Ct);
        Assert.False((await _project.ClaimAsync(Id("A1"), "agent-1", Ct)).Changed);
        Assert.Equal(bytes, await File.ReadAllTextAsync(path, Ct));
        Assert.Equal(
            "A1 is claimed by agent-1",
            (
                await Assert.ThrowsAsync<ClaimConflictException>(() =>
                    _project.ClaimAsync(Id("A1"), "agent-2", Ct)
                )
            ).Message
        );
        Assert.Equal(
            "A2 is blocked by A1",
            (
                await Assert.ThrowsAsync<ClaimConflictException>(() =>
                    _project.ClaimAsync(Id("A2"), "agent-2", Ct)
                )
            ).Message
        );
        Assert.Equal(
            "no ready task",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.ClaimAsync(null, "agent-2", Ct)
                )
            ).Message
        );
        Assert.Equal(
            Id("A1"),
            (await _store.LatestClaimAsync("github.com/me/app", "agent-1", Ct))!.Id
        );

        Assert.Equal(
            "A1 is claimed by agent-1; pass --force",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.ReleaseAsync(Id("A1"), "agent-2", false, Ct)
                )
            ).Message
        );
        var released = await _project.ReleaseAsync(Id("A1"), "agent-1", false, Ct);
        Assert.Equal(TaskState.Open, released.Status);
        Assert.Null(released.Assignee);
        Assert.Null(await _store.LatestClaimAsync("github.com/me/app", "agent-1", Ct));
    }

    [Fact]
    public async Task A_stale_claim_can_be_taken_over()
    {
        await CreateAsync("First");
        await _project.ClaimAsync(Id("A1"), "agent-1", Ct);
        _clock.Now = 10_000 + Readiness.StaleSeconds;
        Assert.Empty(await _project.ReadyAsync(Ct));
        _clock.Now++;
        var ready = Assert.Single(await _project.ReadyAsync(Ct));
        Assert.True(ready.Stale);
        _store.Agents["agent-2"] = _clock.Now;
        Assert.Equal("agent-2", (await _project.ClaimAsync(null, "agent-2", Ct)).Task.Assignee);
    }

    [Fact]
    public async Task A_holder_with_no_agent_row_is_stale_but_a_store_error_is_not()
    {
        await CreateAsync("First");
        await _project.ClaimAsync(Id("A1"), "ghost", Ct);
        Assert.Single(await _project.ReadyAsync(Ct));
        _store.FailAgents = true;
        Assert.Empty(await _project.ReadyAsync(Ct));
    }

    [Fact]
    public async Task Finishing_a_claimed_task_drops_the_claim()
    {
        await CreateAsync("First");
        await _project.ClaimAsync(Id("A1"), "agent-1", Ct);
        await _project.StatusAsync(Id("A1"), TaskState.Done, false, Ct);
        Assert.Null(await _store.LatestClaimAsync("github.com/me/app", "agent-1", Ct));
        Assert.Equal(
            "A1 is done and cannot be claimed",
            (
                await Assert.ThrowsAsync<ClaimConflictException>(() =>
                    _project.ClaimAsync(Id("A1"), "agent-1", Ct)
                )
            ).Message
        );
    }

    [Fact]
    public async Task Dependencies_refuse_self_missing_and_cycles_and_write_nothing_twice()
    {
        await CreateAsync("Parent");
        await CreateAsync("Child", "A1");
        await CreateAsync("Other");
        Assert.Equal(
            "A1 cannot depend on itself",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.BlockAsync(Id("A1"), Id("A1"), Ct)
                )
            ).Message
        );
        Assert.Equal(
            "no task A9",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.BlockAsync(Id("A1"), Id("A9"), Ct)
                )
            ).Message
        );
        Assert.Equal(
            "A1.1 would cycle: A1 -> A1.1",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.BlockAsync(Id("A1.1"), Id("A1"), Ct)
                )
            ).Message
        );
        await _project.BlockAsync(Id("A2"), Id("A1.1"), Ct);
        Assert.Equal(
            "A1.1 would cycle: A1.1 -> A2",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.BlockAsync(Id("A1.1"), Id("A2"), Ct)
                )
            ).Message
        );
        _clock.Now = 20_000;
        var again = await _project.BlockAsync(Id("A2"), Id("A1.1"), Ct);
        Assert.Equal(10_000, again.UpdatedAt);
    }

    [Fact]
    public async Task Conflicts_are_symmetric_and_flag_the_ready_queue()
    {
        await CreateAsync("First");
        await CreateAsync("Second");
        await CreateAsync("Third");
        await _project.ConflictAsync(Id("A1"), Id("A2"), false, Ct);
        await _project.ConflictAsync(Id("A1"), Id("A3"), false, Ct);
        await _project.ConflictAsync(Id("A1"), Id("A2"), false, Ct);
        Assert.Equal(
            "A2 A3",
            string.Join(' ', (await _project.GetAsync(Id("A1"), Ct)).Task.ConflictsWith)
        );
        Assert.Equal(
            "A1",
            string.Join(' ', (await _project.GetAsync(Id("A2"), Ct)).Task.ConflictsWith)
        );
        await _project.SetParallelAsync(Id("A3"), true, Ct);
        await _project.ClaimAsync(Id("A1"), "agent-1", Ct);
        var ready = await _project.ReadyAsync(Ct);
        Assert.Equal("A1", string.Join(' ', ready[0].ConflictsInProgress));
        await _project.ConflictAsync(Id("A2"), Id("A1"), true, Ct);
        Assert.Empty((await _project.GetAsync(Id("A2"), Ct)).Task.ConflictsWith);
        Assert.Equal(
            "A3",
            string.Join(' ', (await _project.GetAsync(Id("A1"), Ct)).Task.ConflictsWith)
        );
        Assert.Equal(
            "no task A9",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.ConflictAsync(Id("A1"), Id("A9"), false, Ct)
                )
            ).Message
        );
        await _project.ConflictAsync(Id("A1"), Id("A9"), true, Ct);
    }

    [Fact]
    public async Task Agents_come_from_the_flag_then_the_environment()
    {
        var warnings = new List<string>();
        Assert.Equal("agent-1", await _project.CallerAsync("agent-1", "agent-2", warnings.Add, Ct));
        Assert.Equal("agent-2", await _project.CallerAsync(null, "agent-2", warnings.Add, Ct));
        Assert.Equal(
            "--agent nobody: unknown",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.CallerAsync("nobody", null, warnings.Add, Ct)
                )
            ).Message
        );
        Assert.Null(await _project.CallerAsync(null, "nobody", warnings.Add, Ct));
        Assert.Single(warnings);
        Assert.Equal(
            "no agent to bind: pass --agent or set RTOK_AGENT_ID",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _project.RequireAgentAsync(null, null, warnings.Add, Ct)
                )
            ).Message
        );
    }
}
