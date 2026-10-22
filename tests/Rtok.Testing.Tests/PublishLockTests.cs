namespace Rtok.Testing.Tests;

/// <summary>The publish lock on a file of its own: a second holder waits for the first, and
/// gives up with the sharing error after its timeout.</summary>
public sealed class PublishLockTests
{
    [Fact]
    public async Task A_second_holder_waits_until_the_first_lets_go()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDir();
        var path = dir.Combine("smoke.lock");
        var first = await PublishedApps.LockAsync(path, TimeSpan.FromMinutes(1), ct);
        var second = PublishedApps.LockAsync(path, TimeSpan.FromMinutes(1), ct);
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        Assert.False(second.IsCompleted);
        first.Dispose();
        (await second).Dispose();
    }

    [Fact]
    public async Task A_holder_that_never_lets_go_times_the_waiter_out()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDir();
        var path = dir.Combine("smoke.lock");
        using var held = await PublishedApps.LockAsync(path, TimeSpan.Zero, ct);
        await Assert.ThrowsAnyAsync<IOException>(() =>
            PublishedApps.LockAsync(path, TimeSpan.FromMilliseconds(300), ct)
        );
    }
}
