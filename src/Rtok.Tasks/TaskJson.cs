using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rtok.Tasks.Export;

namespace Rtok.Tasks;

/// <summary>
/// The JSON of tasks, shared by <c>rtok task --json</c> (indented) and the MCP <c>task_*</c>
/// tools (compact). Keys keep the model's order; empty optional fields are left out.
/// </summary>
public static class TaskJson
{
    private static readonly JsonWriterOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonWriterOptions Indented = Compact with { Indented = true };

    /// <summary>Renders one value written by <paramref name="write"/>.</summary>
    /// <param name="write">Writes exactly one JSON value.</param>
    /// <param name="indented">Two-space indentation, for the CLI.</param>
    /// <returns>The JSON, without a trailing newline.</returns>
    public static string Render(Action<Utf8JsonWriter> write, bool indented)
    {
        ArgumentNullException.ThrowIfNull(write);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, indented ? Indented : Compact))
        {
            write(writer);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <summary>A task as an object.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="task">The task.</param>
    public static void Write(Utf8JsonWriter writer, PlanTask task)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        WriteFields(writer, task);
        writer.WriteEndObject();
    }

    /// <summary>Tasks as an array.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="tasks">The tasks.</param>
    public static void Write(Utf8JsonWriter writer, IEnumerable<PlanTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(tasks);
        writer.WriteStartArray();
        foreach (var task in tasks)
        {
            Write(writer, task);
        }
        writer.WriteEndArray();
    }

    /// <summary>A task with its subtasks (<c>show</c>, <c>task_get</c>).</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="view">The task and its subtasks.</param>
    public static void Write(Utf8JsonWriter writer, TaskView view)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(view);
        writer.WriteStartObject();
        WriteFields(writer, view.Task);
        Ids(writer, "subtasks", view.Subtasks);
        writer.WriteEndObject();
    }

    /// <summary>The ready queue (<c>ready</c>, <c>task_ready</c>).</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="items">The ready tasks.</param>
    public static void Write(Utf8JsonWriter writer, IEnumerable<ReadyItem> items)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(items);
        writer.WriteStartArray();
        foreach (var item in items)
        {
            writer.WriteStartObject();
            WriteFields(writer, item.Task);
            writer.WriteBoolean("stale", item.Stale);
            if (item.ConflictsInProgress.Count > 0)
            {
                Ids(writer, "conflicts_in_progress", item.ConflictsInProgress);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    /// <summary>A claim's result (<c>claim</c>, <c>task_claim</c>).</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="claim">The claimed task and whether it was written.</param>
    public static void Write(Utf8JsonWriter writer, ClaimResult claim)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(claim);
        writer.WriteStartObject();
        WriteFields(writer, claim.Task);
        writer.WriteBoolean("changed", claim.Changed);
        writer.WriteEndObject();
    }

    /// <summary>A sync report (<c>sync --json</c>).</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="report">The report.</param>
    public static void Write(Utf8JsonWriter writer, SyncReport report)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(report);
        writer.WriteStartObject();
        writer.WriteString("adapter", report.Adapter);
        writer.WriteNumber("counter", report.Counter);
        writer.WriteString("next", report.Next.ToString());
        writer.WriteStartArray("raised");
        foreach (var r in report.Raised)
        {
            writer.WriteStartObject();
            OptionalId(writer, "parent", r.Parent);
            writer.WriteNumber("from", r.From);
            writer.WriteNumber("to", r.To);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        Ids(writer, "above_counter", report.AboveCounter);
        Ids(writer, "duplicates", report.Duplicates);
        writer.WriteStartArray("unlabelled");
        foreach (var s in report.Unlabelled)
        {
            writer.WriteStartObject();
            writer.WriteString("title", s.Title);
            writer.WriteString("url", s.Url);
            OptionalId(writer, "id", s.Id);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>An export report (<c>export --json</c>).</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="report">The report.</param>
    public static void Write(Utf8JsonWriter writer, ExportReport report)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(report);
        writer.WriteStartObject();
        writer.WriteString("backend", report.Backend);
        writer.WriteString("target", report.Target);
        writer.WriteBoolean("dry_run", report.DryRun);
        writer.WriteStartArray("actions");
        foreach (var a in report.Actions)
        {
            writer.WriteStartObject();
            writer.WriteString("id", a.Id.ToString());
            writer.WriteString("kind", a.Kind.Word());
            writer.WriteStartArray("fields");
            foreach (var f in a.Fields)
            {
                writer.WriteStringValue(f);
            }
            writer.WriteEndArray();
            if (a.Url is not null)
            {
                writer.WriteString("url", a.Url);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteNumber("unchanged", report.Unchanged);
        writer.WriteNumber("skipped", report.Skipped);
        writer.WriteEndObject();
    }

    /// <summary>A task's properties, for objects that add their own.</summary>
    /// <param name="writer">The writer, inside an object.</param>
    /// <param name="task">The task.</param>
    public static void WriteFields(Utf8JsonWriter writer, PlanTask task)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(task);
        writer.WriteString("id", task.Id.ToString());
        writer.WriteString("title", task.Title);
        writer.WriteString("description", task.Description);
        writer.WriteString("status", task.Status.Wire());
        if (task.Parent is { } parent)
        {
            writer.WriteString("parent", parent.ToString());
        }
        writer.WriteNumber("created_at", task.CreatedAt);
        writer.WriteNumber("updated_at", task.UpdatedAt);
        if (task.External is { } external)
        {
            writer.WriteStartObject("external");
            writer.WriteString("adapter", external.Adapter);
            writer.WriteNumber("number", external.Number);
            writer.WriteString("url", external.Url);
            if (external.NodeId is { } node)
            {
                writer.WriteString("node_id", node);
            }
            writer.WriteEndObject();
        }
        if (task.Priority != PlanTask.DefaultPriority)
        {
            writer.WriteNumber("priority", task.Priority);
        }
        if (task.Assignee is { } assignee)
        {
            writer.WriteString("assignee", assignee);
        }
        if (task.BlockedBy.Count > 0)
        {
            Ids(writer, "blocked_by", task.BlockedBy);
        }
        if (task.ConflictsWith.Count > 0)
        {
            Ids(writer, "conflicts_with", task.ConflictsWith);
        }
        if (task.Parallel)
        {
            writer.WriteBoolean("parallel", true);
        }
    }

    private static void OptionalId(Utf8JsonWriter writer, string name, TaskId? id)
    {
        if (id is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, id.ToString());
        }
    }

    private static void Ids(Utf8JsonWriter writer, string name, IEnumerable<TaskId> ids)
    {
        writer.WriteStartArray(name);
        foreach (var id in ids)
        {
            writer.WriteStringValue(id.ToString());
        }
        writer.WriteEndArray();
    }
}
