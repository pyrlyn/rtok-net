using System.Text;
using Rtok.Tasks.Export;

namespace Rtok.Tasks;

/// <summary>The human text of <c>rtok task</c>: tables and reports, each ending in a newline.</summary>
public static class TaskText
{
    /// <summary><c>list</c>: one line per task, subtasks indented under their parent; empty for no task.</summary>
    /// <param name="tasks">The tasks, by id.</param>
    /// <returns>The table.</returns>
    public static string Table(IReadOnlyList<PlanTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var width = tasks.Select(t => Indented(t.Id).Length).DefaultIfEmpty(0).Max();
        var sb = new StringBuilder();
        foreach (var t in tasks)
        {
            sb.Append(Indented(t.Id).PadRight(width))
                .Append("  ")
                .Append(t.Status.Wire().PadRight(11))
                .Append("  ")
                .Append(t.Title);
            if (t.Parallel)
            {
                sb.Append("  [parallel]");
            }
            if (t.ConflictsWith.Count > 0)
            {
                sb.Append("  [conflicts: ").Append(Ids(t.ConflictsWith)).Append(']');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary><c>show</c>: the fields, then the description.</summary>
    /// <param name="view">The task and its subtasks.</param>
    /// <returns>The text.</returns>
    public static string Details(TaskView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var t = view.Task;
        var sb = new StringBuilder()
            .Append(t.Id.ToString())
            .Append("  ")
            .Append(t.Title)
            .Append("\nstatus: ")
            .Append(t.Status.Wire())
            .Append('\n');
        Line(sb, "parent", t.Parent?.ToString());
        Line(sb, "priority", t.Priority == PlanTask.DefaultPriority ? null : $"{t.Priority}");
        Line(sb, "assignee", t.Assignee);
        Line(sb, "blocked by", t.BlockedBy.Count > 0 ? Ids(t.BlockedBy) : null);
        Line(sb, "conflicts with", t.ConflictsWith.Count > 0 ? Ids(t.ConflictsWith) : null);
        Line(sb, "parallel", t.Parallel ? "yes" : null);
        Line(sb, "subtasks", view.Subtasks.Count > 0 ? Ids(view.Subtasks) : null);
        Line(sb, "link", t.External?.Url);
        if (t.Description.Length > 0)
        {
            sb.Append('\n').Append(t.Description).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary><c>ready</c>: one line per task, <c>(stale)</c> when its assignee can be replaced.</summary>
    /// <param name="items">The ready queue.</param>
    /// <returns>The text.</returns>
    public static string Ready(IReadOnlyList<ReadyItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return "no ready task\n";
        }
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append(item.Task.Id.ToString()).Append("  ").Append(item.Task.Title);
            if (item.Stale)
            {
                sb.Append("  (stale)");
            }
            if (item.Task.Parallel)
            {
                sb.Append("  [parallel]");
            }
            if (item.ConflictsInProgress.Count > 0)
            {
                sb.Append("  (conflicts with ")
                    .Append(Ids(item.ConflictsInProgress))
                    .Append(" in progress)");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary><c>sync</c>: the next id, then one line per kind of drift.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The text.</returns>
    public static string Sync(SyncReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder()
            .Append(report.Adapter)
            .Append(": next id ")
            .Append(report.Next.ToString());
        if (report.IsClean && report.Raised.Count == 0)
        {
            return sb.Append(", counters in step\n").ToString();
        }
        sb.Append('\n');
        foreach (var r in report.Raised)
        {
            sb.Append("raised ")
                .Append(r.Parent is { } p ? $"subtasks of {p}" : "top level")
                .Append(": ")
                .Append($"{r.From} -> {r.To}")
                .Append('\n');
        }
        if (report.AboveCounter.Count > 0)
        {
            sb.Append("above the counter: ").Append(Ids(report.AboveCounter)).Append('\n');
        }
        if (report.Duplicates.Count > 0)
        {
            sb.Append("duplicate ids: ").Append(Ids(report.Duplicates)).Append('\n');
        }
        foreach (var s in report.Unlabelled)
        {
            sb.Append("no id label: ").Append(s.Title);
            if (s.Id is { } id)
            {
                sb.Append(" (label rtok:").Append(id.ToString()).Append(" is gone)");
            }
            sb.Append(' ').Append(s.Url).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary><c>export</c>: a summary line, then one line per action.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The text.</returns>
    public static string Export(ExportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        int Count(ExportKind kind) => report.Actions.Count(a => a.Kind == kind);
        var sb = new StringBuilder(
            $"{report.Backend} ({report.Target}): {Count(ExportKind.Create)} create, {Count(ExportKind.Update)} update, {Count(ExportKind.Close)} close, {Count(ExportKind.Reopen)} reopen, {report.Unchanged} unchanged"
        );
        if (report.Skipped > 0)
        {
            sb.Append(", ").Append(report.Skipped).Append(" finished not exported");
        }
        if (report.DryRun)
        {
            sb.Append(" (dry run: nothing sent)");
        }
        sb.Append('\n');
        foreach (var a in report.Actions)
        {
            sb.Append(a.Kind.Word()).Append(' ').Append(a.Id.ToString());
            if (a.Fields.Count > 0)
            {
                sb.Append("  (").AppendJoin(", ", a.Fields).Append(')');
            }
            if (a.Url is not null)
            {
                sb.Append("  ").Append(a.Url);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string Indented(TaskId id) => new string(' ', 2 * (id.Depth - 1)) + id;

    private static string Ids(IEnumerable<TaskId> ids) => string.Join(", ", ids);

    private static void Line(StringBuilder sb, string name, string? value)
    {
        if (value is not null)
        {
            sb.Append(name).Append(": ").Append(value).Append('\n');
        }
    }
}
