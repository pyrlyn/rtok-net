namespace Rtok.Agents.Junk;

/// <summary>The store calls the junk commands make: rtok's archives past retention, and retention itself.</summary>
public interface IJunkStore
{
    /// <summary>The archive files retention would remove at <paramref name="days"/>.</summary>
    IReadOnlyList<string> ArchivesPendingRetention(int days);

    /// <summary>Run retention: the rows and their archive files.</summary>
    void RunRetention(int calls, int hookBodies);
}
