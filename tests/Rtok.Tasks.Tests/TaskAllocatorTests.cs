namespace Rtok.Tasks.Tests;

public sealed class TaskAllocatorTests
{
    private readonly TaskAllocator _ids = new(new MemoryTaskStore());

    private static TaskId Id(string s) => TaskId.Parse(s);

    [Fact]
    public async Task Counts_per_project_and_parent()
    {
        Assert.Equal("R1", (await _ids.AllocateAsync("p", "r", null, Ct)).ToString());
        Assert.Equal("R2", (await _ids.AllocateAsync("p", "R", null, Ct)).ToString());
        Assert.Equal("R1", (await _ids.AllocateAsync("q", "R", null, Ct)).ToString());
        Assert.Equal("R2.1", (await _ids.AllocateAsync("p", "R", Id("R2"), Ct)).ToString());
        Assert.Equal("R2.2", (await _ids.AllocateAsync("p", "R", Id("R2"), Ct)).ToString());
        Assert.Equal("R1.1", (await _ids.AllocateAsync("p", "R", Id("R1"), Ct)).ToString());
        Assert.Equal("AT3", (await _ids.AllocateAsync("p", "at", null, Ct)).ToString());
    }

    [Fact]
    public async Task Refusals_burn_no_number()
    {
        var e = await Assert.ThrowsAsync<TaskException>(() =>
            _ids.AllocateAsync("p", "R", Id("R1"), Ct)
        );
        Assert.Equal("parent R1 was never allocated in this project (last is 0)", e.Message);
        await Assert.ThrowsAsync<TaskException>(() => _ids.AllocateAsync("p", "R", Id("R1.1"), Ct));
        await Assert.ThrowsAsync<TaskException>(() => _ids.AllocateAsync("p", "R9", null, Ct));
        Assert.Equal("R1", (await _ids.AllocateAsync("p", "R", null, Ct)).ToString());
    }

    [Fact]
    public async Task Seeding_only_raises()
    {
        Assert.Equal(40u, await _ids.SeedAsync("p", null, 40, Ct));
        Assert.Equal(40u, await _ids.SeedAsync("p", null, 7, Ct));
        Assert.Equal("R41", (await _ids.AllocateAsync("p", "R", null, Ct)).ToString());
        await _ids.SeedAsync("p", Id("R41"), 3, Ct);
        Assert.Equal("R41.4", (await _ids.AllocateAsync("p", "R", Id("R41"), Ct)).ToString());
    }
}
