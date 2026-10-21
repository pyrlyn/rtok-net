using Tomlyn.Model;
using Tomlyn.Syntax;

namespace Pyrlyn.Config.Layered;

/// <summary>
/// Sets one key in TOML text and leaves every other byte as it was: comments, blank lines,
/// key order, quoting and the padding around the value.
/// </summary>
/// <remarks>
/// The key's place comes from Tomlyn's syntax tree, so text inside a multi-line string is never
/// taken for a key, a header or a comment. In order:
/// <list type="number">
/// <item>A live key: only its value is replaced; a trailing comment stays.</item>
/// <item>A commented assignment (<c># key = value</c>) in the key's table, as a generated
/// reference file has: it is uncommented in place, then its value is replaced.</item>
/// <item>Otherwise a line is added after the last key of the table, or after a dotted key that
/// already defines the table, or a new <c>[table]</c> is appended at the end.</item>
/// </list>
/// The result is parsed again and must hold the key, or the edit is refused.
/// </remarks>
public static class TomlEditor
{
    /// <summary>Sets <paramref name="key"/> to <paramref name="value"/>.</summary>
    /// <param name="text">The TOML text.</param>
    /// <param name="key">The dotted key.</param>
    /// <param name="value">The value; see <see cref="TomlText.Format"/>.</param>
    /// <param name="file">The file name for errors, or null.</param>
    /// <returns>The edited text.</returns>
    /// <exception cref="ConfigFileException">The text is not valid TOML, or a part of the key is not a table.</exception>
    public static string Set(string text, string key, object value, string? file = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(value);
        var path = KeyPath.Split(key);
        var literal = TomlText.Format(value);
        var doc = TomlText.ParseSyntax(text, file);
        var edited =
            Replace(text, doc, path, literal, file)
            ?? Uncommented(text, doc, path, literal, file)
            ?? Insert(text, doc, path, literal);
        Check(edited, path, file);
        return edited;
    }

    private sealed record Live(
        string[] Path,
        KeyValueSyntax? KeyValue,
        int TableDepth,
        bool Inline
    );

    /// <summary>Every addressable key: its full path, its key-value node and how deep its table is.</summary>
    private static List<Live> Index(DocumentSyntax doc)
    {
        var live = new List<Live>();
        foreach (var kv in doc.KeyValues)
        {
            Add(live, [], kv, inline: false);
        }
        foreach (var table in doc.Tables)
        {
            var path = KeyPath.Parts(table.Name!);
            if (table is TableArraySyntax)
            {
                live.Add(new Live(path, null, path.Length, Inline: false));
                continue;
            }
            foreach (var kv in table.Items)
            {
                Add(live, path, kv, inline: false);
            }
        }
        return live;
    }

    private static void Add(List<Live> live, string[] prefix, KeyValueSyntax kv, bool inline)
    {
        string[] path = [.. prefix, .. KeyPath.Parts(kv.Key!)];
        live.Add(new Live(path, kv, prefix.Length, inline));
        if (kv.Value is InlineTableSyntax table)
        {
            foreach (var item in table.Items)
            {
                Add(live, path, item.KeyValue!, inline: true);
            }
        }
    }

    private static string? Replace(
        string text,
        DocumentSyntax doc,
        string[] path,
        string literal,
        string? file
    )
    {
        var live = Index(doc);
        foreach (var e in live)
        {
            if (KeyPath.Equal(e.Path, path))
            {
                if (e.KeyValue is null)
                {
                    throw Error(file, e, $"{KeyPath.Join(path)} is an array of tables");
                }
                var span = e.KeyValue.Value!.Span;
                return string.Concat(
                    text.AsSpan(0, span.Offset),
                    literal,
                    text.AsSpan(span.Offset + span.Length)
                );
            }
        }
        foreach (var e in live)
        {
            if (e.Path.Length < path.Length && KeyPath.StartsWith(path, e.Path))
            {
                var what = e.KeyValue switch
                {
                    null => "an array of tables",
                    { Value: InlineTableSyntax } => "an inline table",
                    _ => "not a table",
                };
                throw Error(file, e, $"{KeyPath.Join(e.Path)} is {what}");
            }
        }
        return null;
    }

    /// <summary>The text with a commented assignment of the key uncommented and set, or null when there is none.</summary>
    private static string? Uncommented(
        string text,
        DocumentSyntax doc,
        string[] path,
        string literal,
        string? file
    )
    {
        var headers = doc.Tables.ToDictionary(
            t => t.Name!.Span.Start.Line,
            t => t is TableSyntax ? KeyPath.Parts(t.Name!) : null
        );
        var payload = PayloadLines(doc);
        var starts = LineStarts(text);
        string[]? table = [];
        for (var n = 0; n < starts.Count; n++)
        {
            if (payload.Contains(n))
            {
                continue;
            }
            if (headers.TryGetValue(n, out var header))
            {
                table = header;
                continue;
            }
            if (table is null)
            {
                continue;
            }
            var end = n + 1 < starts.Count ? starts[n + 1] : text.Length;
            var line = text[starts[n]..end];
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith('#'))
            {
                continue;
            }
            var rest = trimmed[1..];
            rest = rest.StartsWith(' ') ? rest[1..] : rest;
            var eq = rest.IndexOf('=', StringComparison.Ordinal);
            if (
                eq <= 0
                || !TrySplit(rest[..eq].Trim(), out var key)
                || !KeyPath.Equal([.. table, .. key], path)
            )
            {
                continue;
            }
            var indent = line[..(line.Length - trimmed.Length)];
            var revealed = string.Concat(text.AsSpan(0, starts[n]), indent, rest, text.AsSpan(end));
            DocumentSyntax again;
            try
            {
                again = TomlText.ParseSyntax(revealed, file);
            }
            catch (ConfigFileException)
            {
                // Prose that only looks like an assignment; try the next candidate.
                continue;
            }
            if (Replace(revealed, again, path, literal, file) is { } set)
            {
                return set;
            }
        }
        return null;
    }

    private static string Insert(string text, DocumentSyntax doc, string[] path, string literal)
    {
        var nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var parent = path[..^1];
        if (parent.Length == 0)
        {
            return InsertLine(text, RootEnd(text, doc), $"{KeyPath.Join(path)} = {literal}", nl);
        }
        foreach (var table in doc.Tables.OfType<TableSyntax>())
        {
            if (KeyPath.Equal(KeyPath.Parts(table.Name!), parent))
            {
                var at =
                    table.Items.ChildrenCount > 0
                        ? End(table.Items.GetChild(table.Items.ChildrenCount - 1)!)
                        : End((SyntaxNodeBase?)table.EndOfLineToken ?? table);
                return InsertLine(text, at, $"{KeyPath.Segment(path[^1])} = {literal}", nl);
            }
        }
        var dotted = Index(doc)
            .FindLast(e =>
                e.KeyValue is not null
                && !e.Inline
                && e.TableDepth <= parent.Length
                && e.Path.Length > parent.Length
                && KeyPath.StartsWith(e.Path, parent)
            );
        if (dotted is not null)
        {
            var key = KeyPath.Join(path[dotted.TableDepth..]);
            return InsertLine(text, End(dotted.KeyValue!), $"{key} = {literal}", nl);
        }
        var gap =
            text.Length == 0 ? ""
            : text.EndsWith('\n') ? nl
            : nl + nl;
        return $"{text}{gap}[{KeyPath.Join(parent)}]{nl}{KeyPath.Segment(path[^1])} = {literal}{nl}";
    }

    /// <summary>Where a new root key goes: after the last root key, else on the line of the first header.</summary>
    private static int RootEnd(string text, DocumentSyntax doc)
    {
        if (doc.KeyValues.ChildrenCount > 0)
        {
            return End(doc.KeyValues.GetChild(doc.KeyValues.ChildrenCount - 1)!);
        }
        if (doc.Tables.ChildrenCount > 0)
        {
            var first = doc.Tables.GetChild(0)!.Name!.Span.Start;
            return first.Offset - first.Column;
        }
        return text.Length;
    }

    private static string InsertLine(string text, int at, string line, string nl)
    {
        var lead = at > 0 && text[at - 1] != '\n' ? nl : "";
        return string.Concat(text[..at], lead, line, nl, text[at..]);
    }

    private static int End(SyntaxNodeBase node) => node.Span.Offset + node.Span.Length;

    private static void Check(string edited, string[] path, string? file)
    {
        TomlTable table;
        try
        {
            table = TomlText.Parse(edited, file);
        }
        catch (ConfigFileException e)
        {
            throw new ConfigFileException(
                file,
                e.Line,
                e.Column,
                $"setting {KeyPath.Join(path)} would break the file: {e.Reason}"
            );
        }
        object node = table;
        foreach (var part in path)
        {
            if (node is not TomlTable t || !t.TryGetValue(part, out node!))
            {
                throw new ConfigFileException(
                    file,
                    1,
                    0,
                    $"cannot set {KeyPath.Join(path)} in this file"
                );
            }
        }
    }

    private static ConfigFileException Error(string? file, Live e, string reason)
    {
        var line = e.KeyValue?.Key?.Span.Start.Line ?? 0;
        return new ConfigFileException(file, line + 1, 0, reason);
    }

    private static bool TrySplit(string key, out string[] parts)
    {
        try
        {
            parts = KeyPath.Split(key);
            return true;
        }
        catch (ArgumentException)
        {
            parts = [];
            return false;
        }
    }

    /// <summary>Zero-based lines that start inside a multi-line string value.</summary>
    private static HashSet<int> PayloadLines(DocumentSyntax doc)
    {
        var lines = new HashSet<int>();
        foreach (var s in doc.Descendants().OfType<StringValueSyntax>())
        {
            for (var n = s.Span.Start.Line + 1; n <= s.Span.End.Line; n++)
            {
                lines.Add(n);
            }
        }
        return lines;
    }

    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' && i + 1 < text.Length)
            {
                starts.Add(i + 1);
            }
        }
        return starts;
    }
}
