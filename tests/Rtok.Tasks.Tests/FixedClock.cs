namespace Rtok.Tasks.Tests;

/// <summary>A clock that stands still unless moved; delays finish at once.</summary>
internal sealed class FixedClock(long unixSeconds) : TimeProvider
{
    public long Now { get; set; } = unixSeconds;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(Now);
}
