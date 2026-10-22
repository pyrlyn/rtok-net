using System.Collections;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Helpers;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Pyrlyn.Config.Layered;

/// <summary>Reads TOML text into a value tree with the line of every leaf, and writes values.</summary>
public static partial class TomlText
{
    /// <summary>Parses TOML text into tables, arrays and scalars.</summary>
    /// <param name="text">The TOML text.</param>
    /// <param name="file">The file name for errors, or null.</param>
    /// <returns>The root table.</returns>
    /// <exception cref="ConfigFileException">The text is not valid TOML.</exception>
    public static TomlTable Parse(string text, string? file = null) => Read(text, file).Table;

    /// <summary>
    /// Reads TOML into a tree and maps each leaf's dotted key to its 1-based line. An array
    /// of tables is one leaf, at its first header.
    /// </summary>
    internal static (TomlTable Table, Dictionary<string, int> Lines) Read(string text, string? file)
    {
        var doc = ParseSyntax(text, file);
        var root = new TomlTable();
        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kv in doc.KeyValues)
        {
            AddKeyValue(root, [], kv, lines, file);
        }
        foreach (var header in doc.Tables)
        {
            var path = KeyPath.Parts(header.Name!);
            var line = header.Name!.Span.Start.Line + 1;
            TomlTable table;
            string[]? prefix;
            if (header is TableArraySyntax)
            {
                var parent = Descend(root, path[..^1], file, line);
                if (!parent.TryGetValue(path[^1], out var existing))
                {
                    existing = new TomlTableArray();
                    parent[path[^1]] = existing;
                    lines.TryAdd(KeyPath.Join(path), line);
                }
                if (existing is not TomlTableArray array)
                {
                    throw new ConfigFileException(
                        file,
                        line,
                        0,
                        $"{KeyPath.Join(path)} is not an array of tables"
                    );
                }
                table = new TomlTable();
                array.Add(table);
                prefix = null;
            }
            else
            {
                table = Descend(root, path, file, line);
                prefix = path;
            }
            foreach (var kv in header.Items)
            {
                AddKeyValue(table, prefix, kv, lines, file);
            }
        }
        return (root, lines);
    }

    internal static DocumentSyntax ParseSyntax(string text, string? file)
    {
        ArgumentNullException.ThrowIfNull(text);
        DocumentSyntax doc;
        try
        {
            doc = SyntaxParser.Parse(text, file, validate: true);
        }
        catch (ArgumentOutOfRangeException e)
        {
            // Tomlyn throws instead of reporting an escape of a surrogate code point (\uD800).
            var at = SurrogateEscape().Match(text);
            var line = at.Success ? text.AsSpan(0, at.Index).Count('\n') + 1 : 1;
            throw new ConfigFileException(file, line, 0, $"invalid escape: {e.Message}");
        }
        if (doc.HasErrors)
        {
            var error = doc.Diagnostics.First(d => d.Kind == DiagnosticMessageKind.Error);
            throw new ConfigFileException(
                file,
                error.Span.Start.Line + 1,
                error.Span.Start.Column + 1,
                error.Message
            );
        }
        return doc;
    }

    /// <summary>Writes a value as a TOML literal: strings quoted, arrays and tables inline.</summary>
    /// <param name="value">
    /// A string, bool, integer, float, <see cref="TomlDateTime"/>, <see cref="DateTime"/>,
    /// <see cref="DateTimeOffset"/>, a table (<see cref="IDictionary{TKey, TValue}"/> with
    /// string keys) or a sequence of these.
    /// </param>
    /// <returns>The literal.</returns>
    /// <exception cref="ArgumentException">The value has no TOML form.</exception>
    public static string Format(object value) =>
        value switch
        {
            string s => TomlFormatHelper.ToString(s, TomlPropertyDisplayKind.Default),
            bool b => TomlFormatHelper.ToString(b),
            int i => TomlFormatHelper.ToString(i, TomlPropertyDisplayKind.Default),
            long l => TomlFormatHelper.ToString(l, TomlPropertyDisplayKind.Default),
            uint u => TomlFormatHelper.ToString(u, TomlPropertyDisplayKind.Default),
            ulong u => TomlFormatHelper.ToString(u, TomlPropertyDisplayKind.Default),
            double d => TomlFormatHelper.ToString(d),
            float f => TomlFormatHelper.ToString(f),
            TomlDateTime t => TomlFormatHelper.ToString(t),
            DateTime t => TomlFormatHelper.ToString(t, TomlPropertyDisplayKind.Default),
            DateTimeOffset t => TomlFormatHelper.ToString(t, TomlPropertyDisplayKind.Default),
            IDictionary<string, object> table => table.Count == 0
                ? "{}"
                : "{ "
                    + string.Join(
                        ", ",
                        table.Select(p => $"{KeyPath.Segment(p.Key)} = {Format(p.Value)}")
                    )
                    + " }",
            IEnumerable items => "[" + string.Join(", ", items.Cast<object>().Select(Format)) + "]",
            _ => throw new ArgumentException(
                $"no TOML form for {value.GetType().Name}",
                nameof(value)
            ),
        };

    /// <summary>A parsed value as JSON: tables become objects, arrays and arrays of tables arrays; dates become their text.</summary>
    /// <param name="value">A value from <see cref="Parse"/>.</param>
    /// <returns>The JSON node; null for null.</returns>
    public static JsonNode? ToJson(object? value) =>
        value switch
        {
            TomlTable t => new JsonObject(
                t.Select(kv => KeyValuePair.Create(kv.Key, ToJson(kv.Value)))
            ),
            TomlTableArray a => new JsonArray([.. a.Select(ToJson)]),
            TomlArray a => new JsonArray([.. a.Select(ToJson)]),
            string s => s,
            bool b => b,
            long l => l,
            double d => d,
            null => null,
            _ => value.ToString(),
        };

    /// <summary>
    /// Reads a raw string as a TOML value when it is one (<c>8080</c>, <c>true</c>,
    /// <c>["a", "b"]</c>, <c>"quoted"</c>), else takes it as a plain string.
    /// </summary>
    /// <param name="raw">The text, for example a command-line argument.</param>
    /// <returns>The value.</returns>
    public static object ParseValueOrString(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.AsSpan().ContainsAny('\r', '\n'))
        {
            return raw;
        }
        DocumentSyntax doc;
        try
        {
            doc = ParseSyntax($"v = {raw}", null);
        }
        catch (ConfigFileException)
        {
            return raw;
        }
        if (doc.Tables.ChildrenCount != 0 || doc.KeyValues.ChildrenCount != 1)
        {
            return raw;
        }
        var table = new TomlTable();
        AddKeyValue(table, null, doc.KeyValues.GetChild(0)!, [], null);
        return table["v"];
    }

    private static void AddKeyValue(
        TomlTable table,
        string[]? prefix,
        KeyValueSyntax kv,
        Dictionary<string, int> lines,
        string? file
    )
    {
        var key = KeyPath.Parts(kv.Key!);
        var line = kv.Key!.Span.Start.Line + 1;
        var container = Descend(table, key[..^1], file, line);
        string[]? path = prefix is null ? null : [.. prefix, .. key];
        container[key[^1]] = Value(kv.Value!, path, lines, file);
        if (path is not null && container[key[^1]] is not TomlTable)
        {
            lines[KeyPath.Join(path)] = line;
        }
    }

    private static object Value(
        ValueSyntax value,
        string[]? path,
        Dictionary<string, int> lines,
        string? file
    )
    {
        switch (value)
        {
            case StringValueSyntax s:
                return s.Value ?? "";
            case IntegerValueSyntax i:
                return i.Value;
            case FloatValueSyntax f:
                return f.Value;
            case BooleanValueSyntax b:
                return b.Value;
            case DateTimeValueSyntax d:
                return d.Value;
            case ArraySyntax a:
                var array = new TomlArray();
                foreach (var item in a.Items)
                {
                    array.Add(Value(item.Value!, null, lines, file));
                }
                return array;
            case InlineTableSyntax t:
                var table = new TomlTable(inline: true);
                foreach (var item in t.Items)
                {
                    AddKeyValue(table, path, item.KeyValue!, lines, file);
                }
                return table;
            default:
                throw new ConfigFileException(
                    file,
                    value.Span.Start.Line + 1,
                    value.Span.Start.Column + 1,
                    $"unsupported value: {value}"
                );
        }
    }

    /// <summary>The table at <paramref name="path"/>, created where missing; an array of tables resolves to its last table.</summary>
    private static TomlTable Descend(TomlTable table, string[] path, string? file, int line)
    {
        for (var i = 0; i < path.Length; i++)
        {
            if (!table.TryGetValue(path[i], out var next))
            {
                next = new TomlTable();
                table[path[i]] = next;
            }
            table = next switch
            {
                TomlTable t => t,
                TomlTableArray { Count: > 0 } a => a[^1],
                _ => throw new ConfigFileException(
                    file,
                    line,
                    0,
                    $"{KeyPath.Join(path[..(i + 1)])} is not a table"
                ),
            };
        }
        return table;
    }

    [GeneratedRegex(@"\\(?:u|U0000)[dD][89a-fA-F][0-9a-fA-F]{2}", RegexOptions.None, 1000)]
    private static partial Regex SurrogateEscape();
}
