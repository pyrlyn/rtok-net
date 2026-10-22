using System.Globalization;

namespace Rtok.Tasks.Remote;

/// <summary>
/// The label vocabulary of the issue adapters. <c>rtok</c> marks every issue, <c>rtok:&lt;ID&gt;</c>
/// names its task, <c>rtok:in-progress</c> is the GitHub state. Save rewrites the managed labels
/// (owner, needs, priority, conflicts, parallel) and keeps every other label in its place.
/// </summary>
internal static class RemoteLabels
{
    public const string Rtok = "rtok";
    public const string InProgress = "rtok:in-progress";
    public const int MaxLength = 50;

    private const string Prefix = "rtok:";
    private const string Owner = "rtok:owner:";
    private const string Needs = "rtok:needs:";
    private const string Level = "rtok:p:";
    private const string Conflicts = "rtok:conflicts:";
    private const string Parallel = "rtok:parallel";

    /// <summary>The label that names a task: <c>rtok:R12</c>.</summary>
    public static string IdLabel(TaskId id) => Prefix + id;

    /// <summary>The task an issue's labels name, or null.</summary>
    public static TaskId? IdOf(IEnumerable<string> labels)
    {
        foreach (var label in labels)
        {
            if (
                label.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && TaskId.TryParse(label[Prefix.Length..], out var id)
            )
            {
                return id;
            }
        }
        return null;
    }

    public static bool Has(IEnumerable<string> labels, string label) =>
        labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));

    public static bool IsManaged(string label) =>
        label.StartsWith(Owner, StringComparison.OrdinalIgnoreCase)
        || label.StartsWith(Needs, StringComparison.OrdinalIgnoreCase)
        || label.StartsWith(Level, StringComparison.OrdinalIgnoreCase)
        || label.StartsWith(Conflicts, StringComparison.OrdinalIgnoreCase)
        || string.Equals(label, Parallel, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the assignee, blockers, priority and markers into <paramref name="task"/>:
    /// the first owner wins, bad ids and levels are skipped.</summary>
    public static void Read(IEnumerable<string> labels, PlanTask task)
    {
        var needs = new List<TaskId>();
        var conflicts = new List<TaskId>();
        int? level = null;
        foreach (var label in labels)
        {
            if (label.StartsWith(Owner, StringComparison.OrdinalIgnoreCase))
            {
                task.Assignee ??= label[Owner.Length..];
            }
            else if (
                label.StartsWith(Needs, StringComparison.OrdinalIgnoreCase)
                && TaskId.TryParse(label[Needs.Length..], out var need)
            )
            {
                needs.Add(need);
            }
            else if (
                label.StartsWith(Level, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(
                    label[Level.Length..],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var p
                )
                && p <= PlanTask.MaxPriority
            )
            {
                level ??= p;
            }
            else if (
                label.StartsWith(Conflicts, StringComparison.OrdinalIgnoreCase)
                && TaskId.TryParse(label[Conflicts.Length..], out var other)
            )
            {
                conflicts.Add(other);
            }
            else if (string.Equals(label, Parallel, StringComparison.OrdinalIgnoreCase))
            {
                task.Parallel = true;
            }
        }
        task.BlockedBy = needs;
        task.ConflictsWith = conflicts;
        task.Priority = level ?? PlanTask.DefaultPriority;
        task.Normalize();
    }

    /// <summary>The managed labels of a task: owner, needs, priority, conflicts, parallel.</summary>
    /// <exception cref="TaskException">A label is longer than the services allow.</exception>
    public static List<string> Managed(PlanTask task, string adapter)
    {
        var labels = new List<string>();
        if (task.Assignee is { } owner)
        {
            labels.Add(Owner + owner);
        }
        labels.AddRange(task.BlockedBy.Select(id => Needs + id));
        if (task.Priority != PlanTask.DefaultPriority)
        {
            labels.Add(Level + task.Priority.ToString(CultureInfo.InvariantCulture));
        }
        labels.AddRange(task.ConflictsWith.Select(id => Conflicts + id));
        if (task.Parallel)
        {
            labels.Add(Parallel);
        }
        if (labels.FirstOrDefault(l => l.Length > MaxLength) is { } tooLong)
        {
            throw new TaskException(
                $"{adapter} tasks: label \"{tooLong}\" is longer than {MaxLength} characters"
            );
        }
        return labels;
    }

    /// <summary>The labels an issue keeps, in their order, then the task's managed ones.</summary>
    public static List<string> Merge(IEnumerable<string> existing, PlanTask task, string adapter) =>
        [.. existing.Where(l => !IsManaged(l)), .. Managed(task, adapter)];

    /// <summary>An issue title: <c>R2.1. Ship it</c>.</summary>
    public static string Title(TaskId id, string title) => $"{id}. {title.Trim()}";

    /// <summary>The task title in an issue title: without its <c>R2.1. </c> lead, or as edited by hand.</summary>
    public static string StripTitle(TaskId id, string title)
    {
        var lead = $"{id}. ";
        return title.StartsWith(lead, StringComparison.OrdinalIgnoreCase)
            ? title[lead.Length..].Trim()
            : title.Trim();
    }

    /// <summary>The id an issue title starts with, for issues that lost their label.</summary>
    public static TaskId? TitleId(string title)
    {
        var dot = title.IndexOf(". ", StringComparison.Ordinal);
        return dot > 0 && TaskId.TryParse(title[..dot], out var id) ? id : null;
    }
}
