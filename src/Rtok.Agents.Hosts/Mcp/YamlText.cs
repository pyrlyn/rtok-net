using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharpYaml;
using SharpYaml.Events;

namespace Rtok.Agents.Hosts.Mcp;

/// <summary>
/// Line-level helpers for hosts whose config is YAML that the user and the host both edit
/// (Goose and Continue <c>config.yaml</c>). They never re-serialise the document: a host finds
/// its own lines and swaps only those, so comments, ordering and quoting stay (rtok yaml_text).
/// </summary>
internal static class YamlText
{
    /// <summary>
    /// A scalar as YAML text: plain when that is unambiguous, else a double-quoted string (JSON's
    /// escapes are valid YAML ones), so a Windows path or a path with spaces survives.
    /// </summary>
    public static string Scalar(string s) =>
        s.Length > 0 && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '/' or '-')
            ? s
            : Quoted(s);

    private static string Quoted(string s)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (
            var writer = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
            )
        )
        {
            writer.WriteStringValue(s);
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>The text split after each <c>\n</c>; every line keeps its own line end.</summary>
    public static List<string> Lines(string raw)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < raw.Length)
        {
            var nl = raw.IndexOf('\n', start);
            var end = nl < 0 ? raw.Length : nl + 1;
            lines.Add(raw[start..end]);
            start = end;
        }
        return lines;
    }

    /// <summary>The line without its line end.</summary>
    public static string Text(string line) => line.TrimEnd('\n', '\r');

    /// <summary>How many spaces the line starts with.</summary>
    public static int Indent(string line)
    {
        var text = Text(line);
        return text.Length - text.TrimStart(' ').Length;
    }

    /// <summary>A blank line or a comment: neither starts nor ends a block.</summary>
    public static bool Skippable(string line)
    {
        var t = Text(line).TrimStart();
        return t.Length == 0 || t.StartsWith('#');
    }

    /// <summary>The file's line end: <c>\r\n</c> when it has one, else <c>\n</c>.</summary>
    public static string Eol(string raw) =>
        raw.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary><paramref name="lines"/> with <c>[start, end)</c> swapped for <paramref name="block"/>; a last kept line without a newline gets one first.</summary>
    public static string Splice(List<string> lines, int start, int end, string block, string eol)
    {
        var head = string.Concat(lines.Take(start));
        if (block.Length > 0 && head.Length > 0 && !head.EndsWith('\n'))
        {
            head += eol;
        }
        return head + block + string.Concat(lines.Skip(end));
    }

    /// <summary>
    /// Checks <paramref name="raw"/> before any edit: blank is fine; otherwise it must parse,
    /// and <paramref name="key"/> must be absent, null or of the kind <paramref name="fits"/> accepts.
    /// </summary>
    /// <exception cref="InvalidDataException">It does not parse, or <paramref name="key"/> does not fit.</exception>
    public static void Validate(
        string raw,
        string path,
        string key,
        Func<JsonNode, bool> fits,
        string kind
    )
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }
        JsonNode? doc;
        try
        {
            doc = Parse(raw);
        }
        catch (YamlException e)
        {
            throw new InvalidDataException($"{path} is not valid YAML; fix it first", e);
        }
        if (doc is JsonObject o && o[key] is { } value && !fits(value))
        {
            throw new InvalidDataException($"{path}: `{key}` is not {kind}");
        }
    }

    /// <summary>
    /// The lines <c>[start, end)</c> parsed as one YAML document, shifted left by
    /// <paramref name="indent"/>; null when they do not parse.
    /// </summary>
    public static JsonNode? ParseBlock(List<string> lines, int start, int end, int indent)
    {
        var text = string.Concat(
            lines.Skip(start).Take(end - start).Select(l => l[Math.Min(indent, Indent(l))..])
        );
        try
        {
            return Parse(text);
        }
        catch (YamlException)
        {
            return null;
        }
    }

    /// <summary>The index past the last line in <c>[from, to)</c> that is not <see cref="Skippable"/>; <paramref name="from"/> when there is none.</summary>
    public static int PastLastContent(List<string> lines, int from, int to)
    {
        var end = from;
        for (var i = from; i < to; i++)
        {
            if (!Skippable(lines[i]))
            {
                end = i + 1;
            }
        }
        return end;
    }

    /// <summary>
    /// The first document of <paramref name="text"/> as JSON: mappings, sequences, and plain
    /// scalars typed by YAML's core schema; null for an empty document.
    /// </summary>
    /// <exception cref="YamlException">The text is not valid YAML.</exception>
    public static JsonNode? Parse(string text)
    {
        var parser = Parser.CreateParser(new StringReader(text));
        JsonNode? root = null;
        var open = new Stack<JsonNode>();
        string? key = null;
        while (parser.MoveNext())
        {
            JsonNode? node;
            switch (parser.Current)
            {
                case MappingStart:
                    node = new JsonObject();
                    break;
                case SequenceStart:
                    node = new JsonArray();
                    break;
                case MappingEnd or SequenceEnd:
                    open.Pop();
                    key = null;
                    continue;
                case Scalar s when open.TryPeek(out var top) && top is JsonObject && key is null:
                    key = s.Value;
                    continue;
                case Scalar s:
                    node = Value(s);
                    break;
                case AnchorAlias:
                    node = null;
                    break;
                case DocumentEnd when root is not null || open.Count > 0:
                    return root;
                default:
                    continue;
            }
            if (open.TryPeek(out var parent))
            {
                if (parent is JsonObject map)
                {
                    map[key!] = node;
                    key = null;
                }
                else
                {
                    ((JsonArray)parent).Add(node);
                }
            }
            else
            {
                root = node;
            }
            if (node is JsonObject or JsonArray)
            {
                open.Push(node);
            }
        }
        return root;
    }

    private static JsonValue? Value(Scalar s)
    {
        if (s.Style != ScalarStyle.Plain || !s.IsPlainImplicit)
        {
            return JsonValue.Create(s.Value);
        }
        var v = s.Value;
        return v switch
        {
            "" or "~" or "null" or "Null" or "NULL" => null,
            "true" or "True" or "TRUE" => JsonValue.Create(true),
            "false" or "False" or "FALSE" => JsonValue.Create(false),
            _ when long.TryParse(
                    v,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var n
                ) => JsonValue.Create(n),
            _ when double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                    && v.Any(char.IsAsciiDigit) => JsonValue.Create(d),
            _ => JsonValue.Create(v),
        };
    }
}
