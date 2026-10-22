namespace Rtok.Tasks.Export;

/// <summary>
/// A tracker the disk tasks are mirrored into, one way: it lists what it holds and applies one
/// action at a time. The diff, the dry run and the report are <see cref="TaskExport"/>'s.
/// </summary>
public interface IExportBackend
{
    /// <summary><c>github</c> or <c>linear</c>.</summary>
    string Name { get; }

    /// <summary>Where the issues go, for the report.</summary>
    string Target { get; }

    /// <summary>
    /// Whether the tracker keeps the assignee, blockers and markers. One that does not must not
    /// have them compared, or every run would see them drift.
    /// </summary>
    bool KeepsClaims { get; }

    /// <summary>Every issue the tracker holds that an rtok id names, read as tasks with <c>External</c> set.</summary>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tasks.</returns>
    Task<IReadOnlyList<PlanTask>> RemoteAsync(CancellationToken ct = default);

    /// <summary>Makes the tracker match <paramref name="task"/> for <paramref name="action"/>.</summary>
    /// <param name="action">The planned write.</param>
    /// <param name="task">The local task.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The issue's page, if the tracker says.</returns>
    Task<string?> ApplyAsync(ExportAction action, PlanTask task, CancellationToken ct = default);
}
