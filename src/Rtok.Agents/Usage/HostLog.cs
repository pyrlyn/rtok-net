namespace Rtok.Agents.Usage;

/// <summary>What one host's reader found: its requests, and the first file it could not read.</summary>
/// <param name="Slices">The requests in the window.</param>
/// <param name="Unreadable">The first file that was unreadable or held no JSON, else null.</param>
internal sealed record HostLog(IReadOnlyList<UsageSlice> Slices, string? Unreadable);
