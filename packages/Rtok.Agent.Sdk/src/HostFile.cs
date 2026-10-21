using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.FsKit;

namespace Rtok.Agent.Sdk;

/// <summary>Reading and writing a host's config file under an <see cref="Apply"/>.</summary>
public static class HostFile
{
    private static readonly JsonWriterOptions Pretty = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Write <paramref name="body"/> at <paramref name="path"/>, gated by <paramref name="apply"/>
    /// and <paramref name="report"/>: a dry run and a <see cref="Apply.NoChanges"/> report write
    /// nothing, and the previous file is backed up first when asked. The write is atomic and
    /// lands on the file a symlink chain points to, so the links survive.
    /// </summary>
    public static void Write(Apply apply, string path, string body, string report)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(body);
        if (!apply.Writes(report))
        {
            return;
        }
        RefuseTilde(path);
        if (apply.Backup)
        {
            if (apply.Run is { } run)
            {
                run.Take(path);
            }
            else
            {
                Backups.Take(path, apply.BackupFiles);
            }
        }
        AtomicFile.Write(path, body, new AtomicWriteOptions { CreateParent = true });
    }

    /// <summary><see cref="Write"/> for a JSON document: two-space indent and the trailing newline host files carry.</summary>
    public static void WriteJson(Apply apply, string path, JsonNode root, string report)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(root);
        if (!apply.Writes(report))
        {
            return;
        }
        Write(apply, path, ToJson(root), report);
    }

    /// <summary><paramref name="root"/> as <see cref="WriteJson"/> writes it.</summary>
    public static string ToJson(JsonNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Pretty))
        {
            root.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    /// <summary>
    /// A host's JSON config as an object to edit. A missing or empty file is an empty object: setup
    /// on a host the user never configured is the common case. A file that cannot be read throws,
    /// so it is refused rather than replaced. Strict JSON only; see <see cref="ParseStrictJson"/>.
    /// </summary>
    public static JsonNode ReadJson(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return File.Exists(path) ? ParseStrictJson(path, File.ReadAllText(path)) : new JsonObject();
    }

    /// <summary>
    /// <paramref name="raw"/>, read from <paramref name="path"/>, as JSON. Empty text is an empty
    /// object. JSONC (comments, trailing commas) is refused, never rewritten, since a rewrite from
    /// a parsed value would delete the comments; the error names the file, line and column.
    /// </summary>
    /// <exception cref="InvalidDataException">The text is not strict JSON.</exception>
    public static JsonNode ParseStrictJson(string path, string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new JsonObject();
        }
        JsonNode node;
        try
        {
            node = JsonNode.Parse(raw) ?? new JsonObject();
        }
        catch (JsonException e)
        {
            var line = (int)(e.LineNumber ?? 0);
            var column = (e.BytePositionInLine ?? 0) + 1;
            var src = raw.Split('\n').ElementAtOrDefault(line)?.Trim() ?? "";
            throw new InvalidDataException(
                $"{path}:{line + 1}:{column}: not strict JSON. rtok refuses to rewrite this file "
                    + "(a rewrite would delete its comments); add the entry by hand at that line "
                    + $"and re-run. The line: {src}",
                e
            );
        }
        try
        {
            // The parser keeps string escapes as written; a lone surrogate (\ud800) fails only on write.
            _ = ToJson(node);
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidDataException(
                $"{path}: a string holds a lone surrogate escape; rtok refuses to rewrite this file",
                e
            );
        }
        return node;
    }

    /// <summary>
    /// Every JSON host installer: <see cref="ReadJson"/>, let <paramref name="edit"/> change the
    /// root object and report what it did, then <see cref="WriteJson"/> under <paramref name="apply"/>.
    /// A root that is not an object becomes one.
    /// </summary>
    public static string EditJson(Apply apply, string path, Func<JsonObject, string> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var root = ReadJson(path) as JsonObject ?? [];
        var report = edit(root);
        WriteJson(apply, path, root, report);
        return report;
    }

    /// <summary>
    /// <c>parent[key]</c> as an object, created when absent; a value of another shape becomes
    /// <c>{}</c>, because a host config is user data and setup must not throw on it.
    /// </summary>
    public static JsonObject ObjectAt(JsonObject parent, string key)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent[key] is JsonObject o)
        {
            return o;
        }
        var made = new JsonObject();
        parent[key] = made;
        return made;
    }

    /// <summary><see cref="ObjectAt"/> for an array.</summary>
    public static JsonArray ArrayAt(JsonObject parent, string key)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent[key] is JsonArray a)
        {
            return a;
        }
        var made = new JsonArray();
        parent[key] = made;
        return made;
    }

    /// <summary>A path starting with a literal <c>~</c> would create a <c>~</c> folder in the working directory; callers expand the home first.</summary>
    private static void RefuseTilde(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (
            string.Equals(path, "~", StringComparison.Ordinal)
            || path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal)
        )
        {
            throw new ArgumentException(
                $"{path}: expand ~ to the home directory first",
                nameof(path)
            );
        }
    }
}
