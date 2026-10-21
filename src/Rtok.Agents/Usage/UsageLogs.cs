namespace Rtok.Agents.Usage;

/// <summary>Everything <c>--source logs</c> read, and the hosts it had to skip.</summary>
/// <param name="Slices">Every request of every host, in the window.</param>
/// <param name="Skipped">The hosts whose files were left unread, in reader order.</param>
public sealed record UsageLogs(IReadOnlyList<UsageSlice> Slices, IReadOnlyList<UsageSkip> Skipped);
