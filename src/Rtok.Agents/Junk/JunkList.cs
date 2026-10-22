using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Rtok.Agents.Junk;

/// <summary>The text and JSON of <c>rtok agents junk list</c>.</summary>
public static class JunkList
{
    /// <summary>How many paths a folder's kind shows in JSON.</summary>
    private const int Sample = 3;

    /// <summary>Every item of the report, counted or not.</summary>
    public static IReadOnlyList<JunkRow> Rows(JunkReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return
        [
            .. report.Own.Select(JunkRow.OfOwn),
            .. report.Agents.SelectMany(a => a.Items.Select(i => JunkRow.OfItem(a.Name, i))),
        ];
    }

    /// <summary>The list text.</summary>
    public static string Text(JunkReport report, JunkView view)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(view);
        var groups = JunkGroup.Of(Rows(report), view);
        var sb = new StringBuilder();
        foreach (var a in report.Agents)
        {
            sb.Append(a.Name);
            if (a.Host && !a.Installed)
            {
                sb.Append(" (not installed)");
            }
            sb.Append('\n');
            Folders(sb, a, view);
            foreach (var k in a.Kinds)
            {
                sb.Append(
                    Inv,
                    $"    {k.Kind} ({k.Class}): {k.Items} items, {view.Size(k.SizeBytes)}\n"
                );
                if (groups.Find(g => Same(g.Agent, a.Name) && Same(g.Kind, k.Kind)) is { } g)
                {
                    sb.Append(g.Lines(view, "      "));
                }
            }
            foreach (var note in a.Notes)
            {
                sb.Append("  ").Append(note).Append('\n');
            }
            Kept(sb, a, view);
            if (!a.Host || a.Kinds.Count > 0)
            {
                sb.Append(Inv, $"  Freed by `clear`: {view.Size(a.FreedDefaultBytes)}\n");
                sb.Append(
                    Inv,
                    $"  Freed with `--include review`: {view.Size(a.FreedReviewBytes)}\n"
                );
            }
        }
        sb.Append("total\n");
        sb.Append(Inv, $"  Folders: {view.Size(report.TotalBytes)}\n");
        sb.Append(Inv, $"  Freed by `clear`: {view.Size(report.FreedDefaultBytes)}\n");
        sb.Append(Inv, $"  Freed with `--include review`: {view.Size(report.FreedReviewBytes)}\n");
        return sb.ToString();
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    private static void Folders(StringBuilder sb, JunkAgent a, JunkView view)
    {
        foreach (var f in a.Folders)
        {
            var path = view.Links ? JunkView.Link(f.Path, f.Path) : f.Path;
            sb.Append("  ")
                .Append(path)
                .Append("  ")
                .Append(f.Role)
                .Append("  ")
                .Append(view.Size(f.SizeBytes));
            if (!f.Documented)
            {
                sb.Append("  not documented: not cleared");
            }
            if (f.SharedWith.Count > 0)
            {
                sb.Append("  shared with ").AppendJoin(", ", f.SharedWith);
            }
            if (f.Note is { } note)
            {
                sb.Append("  (").Append(note).Append(')');
            }
            sb.Append('\n');
        }
        if (a.Host && a.Folders.Count == 0)
        {
            sb.Append("  no folders found\n");
        }
    }

    /// <summary>The items that stay: an undocumented find one line per kind, a kept item one line per reason when several share it.</summary>
    private static void Kept(StringBuilder sb, JunkAgent a, JunkView view)
    {
        var notCounted = a.Items.Where(i => !i.Counted).ToList();
        // A worktree can hold hundreds of finds nothing documents.
        foreach (var kind in JunkScan.Kinds)
        {
            var listed = notCounted.FindAll(i =>
                Same(i.Kind, kind) && i.Kept is JunkEvidence.NotDocumented
            );
            if (listed.Count > 0)
            {
                sb.Append(
                    Inv,
                    $"    {kind}: {listed.Count} listed, {view.Size(listed.Sum(i => i.Bytes))}  {JunkEvidence.NotDocumented}\n"
                );
            }
        }
        // A log folder keeps hundreds of fresh files.
        foreach (
            var kept in notCounted
                .Where(i => i.Kept is not JunkEvidence.NotDocumented)
                .GroupBy(i => (i.Kind, Why: i.Kept!))
        )
        {
            var bytes = view.Size(kept.Sum(i => i.Bytes));
            var many = kept.ToList();
            sb.Append(
                many.Count == 1
                    ? $"    kept {many[0].Path}  {bytes}  ({kept.Key.Why})\n"
                    : string.Create(
                        Inv,
                        $"    kept {kept.Key.Kind}: {many.Count} items, {bytes}  ({kept.Key.Why})\n"
                    )
            );
        }
    }

    /// <summary>
    /// <c>--json</c>: <c>agents[]{name,id?,folders[]{path,size_bytes,kinds[]},freed_default_bytes,freed_review_bytes}</c>
    /// and <c>totals</c>, with every item of each agent under <c>items</c>. No trailing newline.
    /// </summary>
    public static string Json(JunkReport report, JunkView view)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(view);
        var rows = Rows(report).Where(r => r.SizeBytes >= view.MinSize).ToList();
        rows.ForEach(r => r.Stamp(view));
        rows = JunkGroup.Order(rows, view);
        return Write(w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("agents");
            foreach (var a in report.Agents)
            {
                Agent(w, a, rows);
            }
            w.WriteEndArray();
            w.WriteStartObject("totals");
            w.WriteNumber("folders_bytes", report.TotalBytes);
            w.WriteNumber("freed_default_bytes", report.FreedDefaultBytes);
            w.WriteNumber("freed_review_bytes", report.FreedReviewBytes);
            w.WriteEndObject();
            w.WriteEndObject();
        });
    }

    private static void Agent(Utf8JsonWriter w, JunkAgent a, List<JunkRow> rows)
    {
        w.WriteStartObject();
        w.WriteString("name", a.Host ? HostNames.Of(a.Name) : a.Name);
        if (a.Host)
        {
            w.WriteString("id", a.Name);
        }
        w.WriteBoolean("installed", a.Installed);
        w.WriteStartArray("folders");
        foreach (var f in a.Folders)
        {
            Folder(w, f, a);
        }
        w.WriteEndArray();
        w.WriteNumber("total_bytes", a.TotalBytes);
        w.WriteStartArray("kinds");
        foreach (var k in a.Kinds)
        {
            w.WriteStartObject();
            w.WriteString("kind", k.Kind);
            w.WriteString("class", k.Class);
            w.WriteNumber("items", k.Items);
            w.WriteNumber("size_bytes", k.SizeBytes);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        if (a.Notes.Count > 0)
        {
            w.WriteStartArray("notes");
            a.Notes.ToList().ForEach(w.WriteStringValue);
            w.WriteEndArray();
        }
        w.WriteNumber("freed_default_bytes", a.FreedDefaultBytes);
        w.WriteNumber("freed_review_bytes", a.FreedReviewBytes);
        w.WriteStartArray("items");
        foreach (var r in rows.Where(r => Same(r.Agent, a.Name)))
        {
            Row(w, r);
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>A folder with the counted kinds of the items whose deepest listed folder it is.</summary>
    private static void Folder(Utf8JsonWriter w, JunkFolder f, JunkAgent a)
    {
        w.WriteStartObject();
        w.WriteString("path", f.Path);
        w.WriteString("role", f.Role);
        w.WriteNumber("size_bytes", f.SizeBytes);
        w.WriteBoolean("documented", f.Documented);
        if (f.SharedWith.Count > 0)
        {
            w.WriteStartArray("shared_with");
            f.SharedWith.ToList().ForEach(w.WriteStringValue);
            w.WriteEndArray();
        }
        if (f.Note is { } note)
        {
            w.WriteString("note", note);
        }
        var mine = a.Items.Where(i =>
            i.Counted
            && ReferenceEquals(
                a.Folders.Where(o => JunkTree.Under(i.Path, o.Path)).MaxBy(o => o.Path.Length),
                f
            )
        );
        w.WriteStartArray("kinds");
        foreach (var k in JunkScan.KindRows([.. mine]))
        {
            w.WriteStartObject();
            w.WriteString("kind", k.Kind);
            w.WriteString("class", k.Class);
            w.WriteNumber("size_bytes", k.SizeBytes);
            w.WriteNumber("items", k.Items);
            w.WriteStartArray("paths_sample");
            foreach (
                var p in mine.Where(i => Same(i.Kind, k.Kind) && Same(i.Class, k.Class))
                    .OrderByDescending(i => i.Bytes)
                    .ThenBy(i => i.Path, StringComparer.Ordinal)
                    .Take(Sample)
            )
            {
                w.WriteStringValue(p.Path);
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    internal static void Row(Utf8JsonWriter w, JunkRow r)
    {
        w.WriteStartObject();
        w.WriteString("kind", r.Kind);
        if (r.Class.Length > 0)
        {
            w.WriteString("class", r.Class);
        }
        w.WriteString("path", r.Path);
        w.WriteNumber("size_bytes", r.SizeBytes);
        WriteTime(w, "last_used", r.LastUsed);
        w.WriteString("reason", r.Reason);
        w.WriteBoolean("will_clear", r.WillClear);
        if (r.SkipReason is { } why)
        {
            w.WriteString("skip_reason", why);
        }
        w.WriteEndObject();
    }

    /// <summary>A UTC time as Unix seconds, or null.</summary>
    internal static void WriteTime(Utf8JsonWriter w, string name, DateTime? t)
    {
        if (t is { } at)
        {
            w.WriteNumber(
                name,
                new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)).ToUnixTimeSeconds()
            );
        }
        else
        {
            w.WriteNull(name);
        }
    }

    internal static string Write(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (
            var w = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Indented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }
            )
        )
        {
            body(w);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
