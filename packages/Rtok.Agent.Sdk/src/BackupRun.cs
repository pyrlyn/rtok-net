namespace Rtok.Agent.Sdk;

/// <summary>
/// The backups of one install, update or uninstall run. Copies are taken uncapped while the run
/// goes; <see cref="Finish"/> then prunes each file to the cap when the run changed something,
/// or deletes the run's own copies when it changed nothing, so a no-op run leaves no copy and
/// never costs an older generation.
/// </summary>
public sealed class BackupRun(int keep, TimeProvider? time = null)
{
    private readonly List<string> copies = [];

    /// <summary>The copies this run took, in order.</summary>
    public IReadOnlyList<string> Copies => copies;

    /// <summary>Copy <paramref name="path"/> into its <c>_backup</c> folder unless an identical generation exists; see <see cref="Backups.Copy"/>.</summary>
    public string? Take(string path)
    {
        var bak = Backups.Copy(path, time);
        if (bak is not null)
        {
            copies.Add(bak);
        }
        return bak;
    }

    /// <summary>Close the run: <paramref name="changed"/> prunes to the cap, otherwise the run's own copies go.</summary>
    public void Finish(bool changed)
    {
        foreach (var bak in copies)
        {
            if (changed)
            {
                Backups.Prune(bak, keep);
            }
            else
            {
                Backups.TryDelete(bak);
            }
        }
        copies.Clear();
    }
}
