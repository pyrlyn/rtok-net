namespace Rtok.Agents.Usage;

/// <summary>The one place a reader turns a request's counters into a <see cref="UsageSlice"/>.</summary>
internal static class LogSlice
{
    /// <summary>
    /// A request in the window, or null: one with no timestamp, before <paramref name="since"/>, or
    /// with no tokens at all (Claude Code's <c>&lt;synthetic&gt;</c> turns) yields nothing.
    /// </summary>
    public static UsageSlice? Of(
        string host,
        string api,
        string session,
        string? model,
        long ts,
        (long Input, long CacheCreate, long CacheRead, long Output) legs,
        long since
    )
    {
        if (
            ts <= 0
            || ts < since
            || legs.Input + legs.CacheCreate + legs.CacheRead + legs.Output == 0
        )
        {
            return null;
        }
        return new UsageSlice(
            host,
            api,
            session,
            model,
            ts,
            legs.Input,
            legs.CacheCreate,
            legs.CacheRead,
            legs.Output
        );
    }

    /// <summary>A file untouched since before <paramref name="since"/> cannot hold a later request.</summary>
    public static DateTime Cutoff(long since) =>
        since <= 0
            ? DateTime.UnixEpoch
            : DateTime.UnixEpoch.AddSeconds(Math.Min(since, 253_402_300_799));
}
