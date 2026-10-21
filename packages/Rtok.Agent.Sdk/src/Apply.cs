namespace Rtok.Agent.Sdk;

/// <summary>
/// How much an installer may do to the disk on this run. Every installer returns one report
/// line per change, and the report is also the write gate: a run whose report is
/// <see cref="NoChanges"/> never writes, so "nothing changed" and "nothing was written" cannot
/// drift apart.
/// </summary>
public sealed record Apply
{
    /// <summary>The report of an installer that found nothing to do.</summary>
    public const string NoChanges = "no changes";

    /// <summary>Describe the change and write nothing.</summary>
    public bool DryRun { get; init; }

    /// <summary>Copy each file to <c>_backup/&lt;name&gt;.bak-&lt;ts&gt;</c> before the first write to it.</summary>
    public bool Backup { get; init; }

    /// <summary>Newest <c>.bak-*</c> generations kept per file after a backup (<c>[setup] backup_files</c>); 0 keeps all.</summary>
    public int BackupFiles { get; init; } = Backups.DefaultKeep;

    /// <summary>Accept every offer without asking. The only way to say yes without a terminal.</summary>
    public bool Yes { get; init; }

    /// <summary>
    /// Asks a person on a terminal; <see langword="null"/> when there is none (CI, a pipe, a host
    /// running setup for the user), and every unanswered question is a no. The surface supplies it.
    /// </summary>
    public Prompt? Ask { get; init; }

    /// <summary>
    /// When set, backups go into this run instead of being pruned right away, so a run that ends
    /// up changing nothing can delete its own copies (<see cref="BackupRun.Finish"/>).
    /// </summary>
    public BackupRun? Run { get; init; }

    /// <summary>
    /// True when this run may write a file for <paramref name="report"/>. A report of only
    /// <c>leave …</c> or <c>? …</c> lines changed nothing, so it writes nothing either.
    /// </summary>
    public bool Writes(string report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return !DryRun
            && report.Length > 0
            && !string.Equals(report, NoChanges, StringComparison.Ordinal)
            && !report
                .Split('\n')
                .All(l =>
                    l.StartsWith("leave ", StringComparison.Ordinal)
                    || l.StartsWith("? ", StringComparison.Ordinal)
                );
    }

    /// <summary>A yes/no question whose default is no (<c>[y/N]</c>): <see cref="Yes"/> accepts, no terminal declines.</summary>
    public bool Confirmed(string question) => Asked(question, defaultYes: false);

    /// <summary>A yes/no question whose default is yes (<c>[Y/n]</c>): <see cref="Yes"/> accepts, no terminal declines.</summary>
    public bool Accepted(string question) => Asked(question, defaultYes: true);

    private bool Asked(string question, bool defaultYes) =>
        Yes || (Ask is { } ask && ask(question, defaultYes));
}
