using Rtok.Store;

namespace Rtok.Agents.Junk;

/// <summary>The junk commands' store calls on rtok's store.</summary>
/// <param name="store">The open store.</param>
public sealed class StoreJunk(RtokStore store) : IJunkStore
{
    /// <inheritdoc/>
    public IReadOnlyList<string> ArchivesPendingRetention(int days) =>
        store.ArchivesPendingRetention(days);

    /// <inheritdoc/>
    public void RunRetention(int calls, int hookBodies) => store.RunRetention(calls, hookBodies);
}
