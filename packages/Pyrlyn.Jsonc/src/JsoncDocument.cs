using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pyrlyn.Jsonc;

/// <summary>
/// A JSONC document (comments, trailing commas) edited losslessly: <see cref="Set"/> and
/// <see cref="Remove"/> change one entry and keep every other byte, comment, comma and line
/// break as it was. Documents are immutable; every edit returns a new one, parsed again before
/// it is returned.
/// </summary>
public sealed class JsoncDocument
{
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 256,
    };

    private readonly byte[] utf8;
    private readonly int bom;
    private readonly Node? root;
    private string? text;

    private JsoncDocument(byte[] utf8, string? file)
    {
        this.utf8 = utf8;
        File = file;
        bom = utf8.AsSpan().StartsWith("﻿"u8) ? 3 : 0;
        try
        {
            root = Syntax.Parse(utf8, bom);
        }
        catch (JsonException e)
        {
            var reason = e.Message;
            var cut = reason.IndexOf(" LineNumber:", StringComparison.Ordinal);
            reason = (cut < 0 ? reason : reason[..cut]).TrimEnd('.', ' ');
            throw new JsoncException(
                file,
                (e.LineNumber ?? 0) + 1,
                (e.BytePositionInLine ?? 0) + 1,
                reason,
                e
            );
        }
    }

    /// <summary>Parse <paramref name="text"/>; <paramref name="file"/> only names it in errors.</summary>
    /// <exception cref="JsoncException">The text is not JSONC.</exception>
    public static JsoncDocument Parse(string text, string? file = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new JsoncDocument(Encoding.UTF8.GetBytes(text), file) { text = text };
    }

    /// <summary>Parse UTF-8 bytes; <paramref name="file"/> only names them in errors.</summary>
    /// <exception cref="JsoncException">The bytes are not JSONC.</exception>
    public static JsoncDocument Parse(ReadOnlySpan<byte> utf8, string? file = null) =>
        new(utf8.ToArray(), file);

    /// <summary>Read and parse the file at <paramref name="path"/>.</summary>
    /// <exception cref="JsoncException">The file is not JSONC; the message names it with line and column.</exception>
    public static JsoncDocument Load(string path) => new(System.IO.File.ReadAllBytes(path), path);

    /// <summary>The file named at parse time, if any.</summary>
    public string? File { get; }

    /// <summary>The document as UTF-8, byte for byte (a BOM included).</summary>
    public ReadOnlyMemory<byte> Utf8 => utf8;

    /// <summary>The document text.</summary>
    public string Text => text ??= Encoding.UTF8.GetString(utf8);

    /// <summary>Only whitespace and comments: no value yet.</summary>
    public bool IsEmpty => root is null;

    /// <inheritdoc/>
    public override string ToString() => Text;

    /// <summary>Whether something is at <paramref name="path"/>.</summary>
    public bool Contains(params ReadOnlySpan<JsoncSegment> path) => Walk(path).Found;

    /// <summary>The exact text of the value at <paramref name="path"/>, comments inside it included; null when absent.</summary>
    public string? GetRaw(params ReadOnlySpan<JsoncSegment> path) =>
        Walk(path) is { Found: true, Value: { } v }
            ? Encoding.UTF8.GetString(utf8, v.Start, v.End - v.Start)
            : null;

    /// <summary>The value at <paramref name="path"/>; null when absent.</summary>
    public JsonElement? Get(params ReadOnlySpan<JsoncSegment> path) =>
        Walk(path) is { Found: true, Value: { } v } ? Element(v) : null;

    /// <summary>
    /// Put <paramref name="value"/> at <paramref name="path"/>: replace the value that is there, or
    /// insert a new member (or, at index <c>Count</c>, a new array element) after the last one, in
    /// the indentation, line breaks and comma style around it. Missing objects on the way are
    /// created. A value equal to the one there returns this document unchanged.
    /// </summary>
    /// <exception cref="InvalidOperationException">Something on the way is not an object or array.</exception>
    public JsoncDocument Set(ReadOnlySpan<JsoncSegment> path, JsonNode? value)
    {
        var hit = Walk(path);
        if (hit.Found && hit.Value is { } existing)
        {
            return JsonElement.DeepEquals(Element(existing), Element(value))
                ? this
                : Apply([Replace(hit, value)]);
        }
        if (hit.Container is null)
        {
            return Apply([CreateRoot(path, value)]);
        }
        var container = hit.Container;
        var step = path[hit.Depth];
        var rest = path[(hit.Depth + 1)..];
        if (container.IsObject && !step.IsIndex)
        {
            var nested = Nest(rest, value, path);
            var key = Key(step.Key!);
            return Apply(
                Insert(
                    container,
                    hit.At,
                    (indent, multiline) => key + ": " + Render(nested, indent, multiline)
                )
            );
        }
        if (container.IsArray && step.IsIndex && step.Index == container.Entries.Count)
        {
            var nested = Nest(rest, value, path);
            return Apply(
                Insert(container, hit.At, (indent, multiline) => Render(nested, indent, multiline))
            );
        }
        var at = JsoncSegment.Describe(path[..hit.Depth]);
        throw new InvalidOperationException(
            container.IsArray && step.IsIndex
                ? $"{at} has {container.Entries.Count} elements; {step} is past the end"
                : $"{at} is {Describe(container)}, not {(step.IsIndex ? "an array" : "an object")}"
        );
    }

    /// <summary><see cref="Set"/> with the value given as JSON (comments allowed).</summary>
    public JsoncDocument SetJson(ReadOnlySpan<JsoncSegment> path, string json) =>
        Set(path, JsonNode.Parse(json, documentOptions: ReadOptions));

    /// <summary>
    /// Remove the entry at <paramref name="path"/> (every one, if a key is duplicated) with its comma
    /// and, when it has lines of its own, those lines. Comments around it stay. Nothing there
    /// returns this document unchanged.
    /// </summary>
    public JsoncDocument Remove(params ReadOnlySpan<JsoncSegment> path)
    {
        if (path.IsEmpty)
        {
            throw new ArgumentException("the root cannot be removed", nameof(path));
        }
        var doc = this;
        while (doc.Walk(path) is { Found: true } hit)
        {
            doc = doc.Apply(doc.Delete(hit.Container!, hit.Index));
            if (path[^1].IsIndex)
            {
                break;
            }
        }
        return doc;
    }

    /// <summary>
    /// Where a walk stopped. <see cref="At"/> is where the line of <see cref="Container"/> (when not
    /// found) or of the entry (when found) is judged: its key, or the root value's start.
    /// </summary>
    private readonly record struct Hit(
        bool Found,
        Node? Container,
        int Index,
        Node? Value,
        int Depth,
        int At
    );

    /// <summary>How far <paramref name="path"/> reaches: the entry when found, else the deepest value on the way.</summary>
    private Hit Walk(ReadOnlySpan<JsoncSegment> path)
    {
        if (root is null)
        {
            return new Hit(false, null, -1, null, 0, -1);
        }
        Node? container = null;
        var index = -1;
        var node = root;
        var at = root.Start;
        for (var depth = 0; depth < path.Length; depth++)
        {
            var step = path[depth];
            var i = step.IsIndex
                ? node.IsArray && step.Index < node.Entries.Count
                    ? step.Index
                    : -1
                : node.IsObject
                    ? node.Entries.FindLastIndex(e =>
                        string.Equals(e.Key, step.Key, StringComparison.Ordinal)
                    )
                    : -1;
            if (i < 0)
            {
                return new Hit(false, node, -1, null, depth, at);
            }
            container = node;
            index = i;
            node = node.Entries[i].Value;
            at = container.Entries[i].KeyStart;
        }
        return new Hit(true, container, index, node, path.Length, at);
    }

    private JsonElement Element(Node node)
    {
        using var doc = JsonDocument.Parse(
            utf8.AsMemory(node.Start, node.End - node.Start),
            ReadOptions
        );
        return doc.RootElement.Clone();
    }

    private static JsonElement Element(JsonNode? value)
    {
        using var doc = JsonDocument.Parse(Render(value, "", "  ", "\n", multiline: false));
        return doc.RootElement.Clone();
    }

    private static string Describe(Node node) =>
        node.Kind switch
        {
            JsonTokenType.StartObject => "an object",
            JsonTokenType.StartArray => "an array",
            JsonTokenType.String => "a string",
            JsonTokenType.Number => "a number",
            JsonTokenType.Null => "null",
            _ => "a boolean",
        };

    /// <summary><paramref name="value"/> wrapped in one object per key of <paramref name="rest"/>.</summary>
    private static JsonNode? Nest(
        ReadOnlySpan<JsoncSegment> rest,
        JsonNode? value,
        ReadOnlySpan<JsoncSegment> path
    )
    {
        var nested = value?.DeepClone();
        for (var i = rest.Length - 1; i >= 0; i--)
        {
            if (rest[i].IsIndex)
            {
                throw new InvalidOperationException(
                    $"{JsoncSegment.Describe(path)}: an array cannot be created at {rest[i]}"
                );
            }
            nested = new JsonObject { [rest[i].Key!] = nested };
        }
        return nested;
    }

    // ---- edits -------------------------------------------------------------------------------

    private readonly record struct Splice(int Start, int Length, string Insert);

    private JsoncDocument Apply(IEnumerable<Splice> splices)
    {
        var output = new ArrayBufferWriter<byte>(utf8.Length + 256);
        var at = 0;
        foreach (var splice in splices.OrderBy(s => s.Start))
        {
            output.Write(utf8.AsSpan(at, splice.Start - at));
            output.Write(Encoding.UTF8.GetBytes(splice.Insert));
            at = splice.Start + splice.Length;
        }
        output.Write(utf8.AsSpan(at));
        try
        {
            return new JsoncDocument(output.WrittenSpan.ToArray(), File);
        }
        catch (JsoncException e)
        {
            throw new InvalidOperationException(
                $"an edit of {File ?? "the document"} produced invalid JSONC",
                e
            );
        }
    }

    private Splice Replace(Hit hit, JsonNode? value)
    {
        var v = hit.Value!;
        var multiline = NewLine is not null && Bytes.StartsLine(utf8, hit.At);
        return new Splice(
            v.Start,
            v.End - v.Start,
            Render(value, Bytes.Indent(utf8, hit.At), multiline)
        );
    }

    private Splice CreateRoot(ReadOnlySpan<JsoncSegment> path, JsonNode? value)
    {
        if (!path.IsEmpty && path[0].IsIndex)
        {
            throw new InvalidOperationException(
                $"{JsoncSegment.Describe(path)}: the document has no array to index"
            );
        }
        var nl = NewLine ?? "\n";
        var body = Render(Nest(path, value, path), "", true) + nl;
        var end = utf8.Length;
        var needsBreak = end > bom && utf8[end - 1] != (byte)'\n';
        return new Splice(end, 0, needsBreak ? nl + body : body);
    }

    /// <summary>
    /// A new entry after the last one of <paramref name="c"/>, rendered by <paramref name="entry"/> for
    /// an indent. An empty container opens onto lines when its own line starts at <paramref name="at"/>.
    /// </summary>
    private Splice[] Insert(Node c, int at, Func<string, bool, string> entry)
    {
        ReadOnlySpan<byte> s = utf8;
        if (c.Entries.Count == 0)
        {
            var open = c.Start + 1;
            var close = c.End - 1;
            if (NewLine is not { } br || !Bytes.StartsLine(s, at))
            {
                return [new Splice(open, 0, entry("", false))];
            }
            var outer = Bytes.Indent(s, at);
            var inner = outer + Unit;
            if (IsBlank(s[open..close]))
            {
                return
                [
                    new Splice(open, close - open, br + inner + entry(inner, true) + br + outer),
                ];
            }
            // Only comments inside: after them, before the line of the closing bracket.
            var lineEnd = Bytes.LineStart(s, close) - 1;
            if (!Bytes.StartsLine(s, close) || lineEnd < open)
            {
                return [new Splice(open, 0, br + inner + entry(inner, true))];
            }
            if (lineEnd > open && s[lineEnd - 1] == (byte)'\r')
            {
                lineEnd--;
            }
            return [new Splice(lineEnd, 0, br + inner + entry(inner, true))];
        }
        var last = c.Entries[^1];
        var trailing = last.Comma >= 0;
        var anchor = trailing ? last.Comma + 1 : last.Value.End;
        if (NewLine is { } nl && Bytes.StartsLine(s, last.KeyStart))
        {
            var indent = Bytes.Indent(s, last.KeyStart);
            var line = nl + indent + entry(indent, true) + (trailing ? "," : "");
            // After a comment that ends the last entry's line, so the comment stays with its entry.
            var eol = Bytes.EndOfLine(s, anchor, comment: true);
            if (eol < 0)
            {
                return [new Splice(anchor, 0, (trailing ? "" : ",") + line)];
            }
            return trailing
                ? [new Splice(eol, 0, line)]
                : [new Splice(last.Value.End, 0, ","), new Splice(eol, 0, line)];
        }
        var item = entry("", false);
        return trailing
            ? [new Splice(anchor, 0, " " + item + ",")]
            : [new Splice(anchor, 0, ", " + item)];
    }

    /// <summary>
    /// Entry <paramref name="k"/> of <paramref name="c"/> removed with one comma: its whole lines when
    /// nothing else is on them, else only its bytes, so comments next to it stay.
    /// </summary>
    private Splice[] Delete(Node c, int k)
    {
        ReadOnlySpan<byte> s = utf8;
        var e = c.Entries[k];
        var start = e.KeyStart;
        var valueEnd = e.Value.End;
        var end = e.Comma >= 0 ? e.Comma + 1 : valueEnd;
        var commaNext = e.Comma < 0 || IsBlank(s[valueEnd..e.Comma]);
        if (
            c.Entries.Count == 1
            && commaNext
            && IsBlank(s[(c.Start + 1)..start])
            && IsBlank(s[end..(c.End - 1)])
        )
        {
            return [new Splice(c.Start + 1, c.End - c.Start - 2, "")];
        }
        var startsLine = Bytes.StartsLine(s, start);
        var eol = startsLine && commaNext ? Bytes.EndOfLine(s, end, comment: false) : -1;
        if (eol >= 0 && (k == 0 || e.Comma >= 0))
        {
            var line = Bytes.LineStart(s, start);
            return [new Splice(line, eol + Bytes.BreakLength(s, eol) - line, "")];
        }
        if (eol >= 0)
        {
            // The last entry on lines of its own: the comma before it goes, and the break before its line.
            var from = Bytes.LineStart(s, start) - 1;
            if (from > 0 && s[from - 1] == (byte)'\r')
            {
                from--;
            }
            return [new Splice(c.Entries[k - 1].Comma, 1, ""), new Splice(from, eol - from, "")];
        }
        if (e.Comma >= 0 && commaNext && (k == 0 || startsLine))
        {
            return [new Splice(start, end - start, "")];
        }
        if (k > 0)
        {
            // The comma before it goes; its own, and any comment before that, stay.
            var before = Bytes.BlanksBefore(s, start);
            return
            [
                new Splice(c.Entries[k - 1].Comma, 1, ""),
                new Splice(before, valueEnd - before, ""),
            ];
        }
        // The first entry, a comment between its value and its comma: the comment stays.
        return e.Comma < 0
            ? [new Splice(start, valueEnd - start, "")]
            : [new Splice(start, valueEnd - start, ""), new Splice(e.Comma, 1, "")];
    }

    private static bool IsBlank(ReadOnlySpan<byte> s) => s.IndexOfAnyExcept(" \t\r\n"u8) < 0;

    // ---- style -------------------------------------------------------------------------------

    /// <summary>The document's line break, or null for a one-line document, which edits keep on one line.</summary>
    private string? NewLine =>
        utf8.AsSpan().IndexOf("\r\n"u8) >= 0 ? "\r\n"
        : utf8.AsSpan().IndexOf((byte)'\n') >= 0 ? "\n"
        : null;

    /// <summary>One level of indentation: the first nested entry's indent beyond its container's, else two spaces.</summary>
    private string Unit => root is null ? "  " : FindUnit(root) ?? "  ";

    private string? FindUnit(Node node)
    {
        if (node.Entries.Count > 0 && Bytes.StartsLine(utf8, node.Entries[0].KeyStart))
        {
            var outer = Bytes.Indent(utf8, node.Start);
            var inner = Bytes.Indent(utf8, node.Entries[0].KeyStart);
            var unit = inner.StartsWith(outer, StringComparison.Ordinal)
                ? inner[outer.Length..]
                : "";
            if (
                unit.Length > 0
                && (
                    unit.AsSpan().IndexOfAnyExcept(' ') < 0
                    || unit.AsSpan().IndexOfAnyExcept('\t') < 0
                )
            )
            {
                return unit;
            }
        }
        foreach (var entry in node.Entries)
        {
            if (FindUnit(entry.Value) is { } unit)
            {
                return unit;
            }
        }
        return null;
    }

    private string Render(JsonNode? value, string indent, bool multiline) =>
        Render(value, indent, Unit, NewLine ?? "\n", multiline);

    /// <summary>
    /// <paramref name="value"/> as JSON: indented by <paramref name="unit"/>, continuation lines
    /// starting at <paramref name="indent"/>, or on one line. Non-ASCII stays readable.
    /// </summary>
    private static string Render(
        JsonNode? value,
        string indent,
        string unit,
        string newLine,
        bool multiline
    )
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (
            var writer = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Indented = multiline,
                    IndentCharacter = unit[0],
                    IndentSize = unit.Length,
                    NewLine = newLine,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }
            )
        )
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                value.WriteTo(writer);
            }
        }
        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        return multiline && indent.Length > 0
            ? json.Replace(newLine, newLine + indent, StringComparison.Ordinal)
            : json;
    }

    private static string Key(string key) =>
        "\""
        + JsonEncodedText.Encode(key, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).Value
        + "\"";
}
