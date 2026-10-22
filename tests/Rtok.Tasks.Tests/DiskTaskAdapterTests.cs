using Rtok.Tasks.Disk;
using Rtok.Testing;

namespace Rtok.Tasks.Tests;

public sealed class DiskTaskAdapterTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly FixedClock _clock = new(1000);
    private readonly DiskTaskAdapter _disk;

    public DiskTaskAdapterTests() => _disk = new DiskTaskAdapter(_temp.Combine("tasks"), _clock);

    public void Dispose() => _temp.Dispose();

    private static TaskId Id(string s) => TaskId.Parse(s);

    [Fact]
    public async Task Create_writes_a_slugged_file_and_reads_back()
    {
        var created = await _disk.CreateAsync(
            Id("R1"),
            new NewPlanTask("  Ship the: allocator!  ", " why \n"),
            Ct
        );
        Assert.Equal("Ship the: allocator!", created.Title);
        Assert.True(File.Exists(_temp.Combine("tasks", "R1 - ship-the-allocator.md")));
        var found = await _disk.FindAsync(Id("r1"), Ct);
        Assert.Equal("why", found!.Description);
        Assert.Equal(1000, found.CreatedAt);
        Assert.Single(await _disk.ListAsync(new TaskFilter(), Ct));
    }

    [Fact]
    public async Task Create_refuses_a_blank_title_and_a_taken_id()
    {
        await _disk.CreateAsync(Id("R1"), new NewPlanTask("One"), Ct);
        Assert.Equal(
            "disk tasks: a task needs a title",
            (
                await Assert.ThrowsAsync<TaskException>(() =>
                    _disk.CreateAsync(Id("R2"), new NewPlanTask(" "), Ct)
                )
            ).Message
        );
        await Assert.ThrowsAsync<TaskTakenException>(() =>
            _disk.CreateAsync(Id("R1"), new NewPlanTask("Again"), Ct)
        );
    }

    [Fact]
    public async Task Finishing_moves_the_file_to_done_and_reopening_moves_it_back()
    {
        await _disk.CreateAsync(Id("R1"), new NewPlanTask("One"), Ct);
        _clock.Now = 2000;
        var done = await _disk.WriteStatusAsync(Id("R1"), TaskState.Done, Ct);
        Assert.Equal(2000, done.UpdatedAt);
        Assert.False(File.Exists(_temp.Combine("tasks", "R1 - one.md")));
        Assert.True(File.Exists(_temp.Combine("tasks", "done", "R1 - one.md")));
        Assert.Empty(await _disk.ListAsync(new TaskFilter(), Ct));
        Assert.Single(await _disk.ListAsync(new TaskFilter { Statuses = [TaskState.Done] }, Ct));
        await _disk.WriteStatusAsync(Id("R1"), TaskState.Open, Ct);
        Assert.True(File.Exists(_temp.Combine("tasks", "R1 - one.md")));
        Assert.False(File.Exists(_temp.Combine("tasks", "done", "R1 - one.md")));
    }

    [Fact]
    public async Task Retitling_renames_the_file()
    {
        var task = await _disk.CreateAsync(Id("R1"), new NewPlanTask("Old name"), Ct);
        task.Title = "New name";
        using (await _disk.LockClaimsAsync(Ct))
        {
            await _disk.SaveAsync(task, Ct);
        }
        Assert.Equal(
            "R1 - new-name.md",
            string.Join(
                ' ',
                Directory.GetFiles(_temp.Combine("tasks"), "*.md").Select(Path.GetFileName)
            )
        );
    }

    [Fact]
    public async Task Foreign_files_are_ignored_and_a_broken_one_is_named()
    {
        _temp.Write("tasks/README.md", "# tasks\n");
        _temp.Write("tasks/R2 - notes.txt", "x");
        Assert.Empty(await _disk.ListAsync(new TaskFilter { All = true }, Ct));
        _temp.Write("tasks/R3 - broken.md", "no fence");
        var e = await Assert.ThrowsAsync<TaskException>(() =>
            _disk.ListAsync(new TaskFilter(), Ct)
        );
        Assert.Contains("R3 - broken.md: no front matter", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tasks/R1 - x.md")]
    [InlineData("tasks/done/r1 - x.md")]
    public async Task A_file_whose_name_and_front_matter_disagree_is_refused_untouched(
        string relative
    )
    {
        var text = "---\nid: R2\ntitle: X\nstatus: done\ncreated_at: 1\nupdated_at: 1\n---\n";
        var path = _temp.Write(relative, text);
        var e = await Assert.ThrowsAsync<TaskException>(() =>
            _disk.ListAsync(new TaskFilter { All = true }, Ct)
        );
        Assert.EndsWith(
            "file name says R1 but the front matter says R2",
            e.Message,
            StringComparison.Ordinal
        );
        await Assert.ThrowsAsync<TaskException>(() =>
            _disk.WriteStatusAsync(Id("R1"), TaskState.Open, Ct)
        );
        Assert.Equal(text, await File.ReadAllTextAsync(path, Ct));
    }

    [Fact]
    public async Task A_missing_task_is_named()
    {
        var e = await Assert.ThrowsAsync<TaskException>(() =>
            _disk.WriteStatusAsync(Id("R7"), TaskState.Done, Ct)
        );
        Assert.Equal($"disk tasks: no task R7 in {_temp.Combine("tasks")}", e.Message);
    }

    [Fact]
    public async Task The_claim_lock_excludes_a_second_holder_until_released()
    {
        _temp.Write("tasks/.claim.lock", "leftover");
        var first = await _disk.LockClaimsAsync(Ct);
        var e = await Assert.ThrowsAsync<TaskException>(() => _disk.LockClaimsAsync(Ct));
        Assert.EndsWith(".claim.lock is locked", e.Message, StringComparison.Ordinal);
        File.SetLastWriteTimeUtc(
            _temp.Combine("tasks", ".claim.lock"),
            DateTime.UtcNow.AddSeconds(-11)
        );
        await Assert.ThrowsAsync<TaskException>(() => _disk.LockClaimsAsync(Ct));
        first.Dispose();
        (await _disk.LockClaimsAsync(Ct)).Dispose();
        Assert.True(File.Exists(_temp.Combine("tasks", ".claim.lock")));
    }
}
