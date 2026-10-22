using System.Globalization;
using System.Text;
using SharpYaml;

namespace Rtok.Tasks.Disk;

/// <summary>
/// One task as a Markdown file: <c>&lt;id&gt; - &lt;slug&gt;.md</c>, YAML front matter, then
/// the description as the body.
/// </summary>
internal static class TaskFile
{
    /// <summary>The longest slug in a file name.</summary>
    public const int SlugMax = 48;

    private const string Fence = "---\n";

    /// <summary>The file name of a task: <c>R1 - ship-the-allocator.md</c>.</summary>
    public static string FileName(TaskId id, string title) => $"{id} - {Slug(title)}.md";

    /// <summary>The id a file name claims, or null for files that are not tasks.</summary>
    public static TaskId? FileId(string fileName)
    {
        if (!fileName.EndsWith(".md", StringComparison.Ordinal))
        {
            return null;
        }
        var stem = fileName[..^3];
        var dash = stem.IndexOf(" - ", StringComparison.Ordinal);
        return dash > 0 && TaskId.TryParse(stem[..dash], out var id) ? id : null;
    }

    /// <summary>ASCII letters and digits in lower case, every other run one dash, at most <see cref="SlugMax"/> characters.</summary>
    public static string Slug(string title)
    {
        var sb = new StringBuilder();
        foreach (var c in title)
        {
            if (sb.Length >= SlugMax)
            {
                break;
            }
            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }
        var slug = sb.ToString().TrimEnd('-');
        return slug.Length == 0 ? "task" : slug;
    }

    /// <summary>The file's text. Default priority, no assignee, empty lists and no parallel flag are left out.</summary>
    public static string Render(PlanTask task)
    {
        var sb = new StringBuilder(Fence);
        Line(sb, "id", task.Id.ToString());
        Line(sb, "title", Scalar(task.Title));
        Line(sb, "status", task.Status.Wire());
        if (task.Parent is { } parent)
        {
            Line(sb, "parent", parent.ToString());
        }
        Line(sb, "created_at", task.CreatedAt.ToString(CultureInfo.InvariantCulture));
        Line(sb, "updated_at", task.UpdatedAt.ToString(CultureInfo.InvariantCulture));
        if (task.Priority != PlanTask.DefaultPriority)
        {
            Line(sb, "priority", task.Priority.ToString(CultureInfo.InvariantCulture));
        }
        if (task.Assignee is { } assignee)
        {
            Line(sb, "assignee", Scalar(assignee));
        }
        List(sb, "blocked_by", task.BlockedBy);
        List(sb, "conflicts_with", task.ConflictsWith);
        if (task.Parallel)
        {
            Line(sb, "parallel", "true");
        }
        sb.Append(Fence);
        var body = task.Description.TrimEnd();
        if (body.Length > 0)
        {
            sb.Append('\n').Append(body).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Reads a file's text. Only the first <c>---</c> line after the opening one closes the front matter.</summary>
    /// <exception cref="TaskException">The text is not a task file; the message does not name the file.</exception>
    public static PlanTask Parse(string text)
    {
        var s = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!s.StartsWith(Fence, StringComparison.Ordinal))
        {
            throw new TaskException("no front matter (the file must start with ---)");
        }
        var close = s.IndexOf("\n---\n", Fence.Length - 1, StringComparison.Ordinal);
        string yaml;
        string body;
        if (close >= 0)
        {
            yaml = s[Fence.Length..(close + 1)];
            body = s[(close + 5)..];
        }
        else if (s.EndsWith("\n---", StringComparison.Ordinal) && s.Length >= Fence.Length + 3)
        {
            yaml = s[Fence.Length..^3];
            body = "";
        }
        else
        {
            throw new TaskException("front matter is not closed with ---");
        }
        TaskHead head;
        try
        {
            head =
                YamlSerializer.Deserialize(yaml, TaskYamlContext.Default.TaskHead)
                ?? throw new TaskException("the front matter is empty");
        }
        catch (YamlException e)
        {
            throw new TaskException($"bad front matter: {e.Message}", e);
        }
        var task = new PlanTask
        {
            Id = TaskId.Parse(Required(head.Id, "id")),
            Title = Required(head.Title, "title"),
            Description = body.Trim(),
            Status = TaskStates.Parse(Required(head.Status, "status")),
            Parent = string.IsNullOrWhiteSpace(head.Parent) ? null : TaskId.Parse(head.Parent),
            CreatedAt = head.CreatedAt,
            UpdatedAt = head.UpdatedAt,
            Priority = head.Priority ?? PlanTask.DefaultPriority,
            Assignee = head.Assignee,
            BlockedBy = Ids(head.BlockedBy, "blocked_by"),
            ConflictsWith = Ids(head.ConflictsWith, "conflicts_with"),
            Parallel = head.Parallel ?? false,
        };
        task.Normalize();
        return task;
    }

    private static string Required(string value, string key) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new TaskException($"missing `{key}` in the front matter")
            : value;

    /// <summary>A list of ids; an empty item, such as <c>- # note</c>, is refused.</summary>
    private static List<TaskId> Ids(List<string>? items, string key) =>
        [
            .. (items ?? []).Select(item =>
                string.IsNullOrWhiteSpace(item)
                    ? throw new TaskException($"an empty item in `{key}`")
                    : TaskId.Parse(item)
            ),
        ];

    private static void Line(StringBuilder sb, string key, string value) =>
        sb.Append(key).Append(": ").Append(value).Append('\n');

    private static void List(StringBuilder sb, string key, IReadOnlyList<TaskId> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }
        sb.Append(key).Append(":\n");
        foreach (var id in ids)
        {
            sb.Append("  - ").Append(id).Append('\n');
        }
    }

    /// <summary>A string as a YAML scalar, quoted when plain style would change its meaning.</summary>
    private static string Scalar(string value) =>
        YamlSerializer.Serialize(value).TrimEnd('\n', '\r');
}
