using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.Config.Layered;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Rtok.Mcp.Install;

/// <summary>
/// TOML: rtok's entry is a standard table, <c>[mcp_servers.rtok]</c>, the shape Codex and Grok
/// hold; parents on the way stay implicit. Edits splice text found through Tomlyn's syntax
/// tree, so every other byte of the file stays.
/// </summary>
internal static class TomlServers
{
    public static JsonObject? Servers(McpSpec spec, string text)
    {
        object node = Model(spec, text);
        foreach (var key in spec.KeyPath)
        {
            if (node is not TomlTable t || !t.TryGetValue(key, out node!))
            {
                return null;
            }
        }
        return node is TomlTable table ? (JsonObject)Json(table)! : null;
    }

    /// <summary>The text with rtok's table in place of whatever spelled the entry before, or appended.</summary>
    public static string With(McpSpec spec, string text, JsonObject entry)
    {
        if (text.StartsWith('\uFEFF'))
        {
            // Tomlyn's offsets start after a BOM.
            return "\uFEFF" + With(spec, text[1..], entry);
        }
        var nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var block = Block(spec, entry, nl);
        var cuts = Cuts(spec, text);
        var header = cuts.FindIndex(c => c.Header);
        string edited;
        if (header >= 0)
        {
            cuts[header] = cuts[header] with { Insert = block };
            edited = Splice(text, cuts);
        }
        else
        {
            edited = Splice(text, cuts);
            var gap =
                edited.Length == 0 ? ""
                : edited.EndsWith('\n') ? nl
                : nl + nl;
            edited = edited + gap + block;
        }
        Check(spec, edited, want: true);
        return edited;
    }

    public static string Without(McpSpec spec, string text)
    {
        if (text.StartsWith('\uFEFF'))
        {
            return "\uFEFF" + Without(spec, text[1..]);
        }
        var edited = Splice(text, Cuts(spec, text));
        Check(spec, edited, want: false);
        return edited;
    }

    /// <summary>A span of text to cut, and what goes in its place.</summary>
    private sealed record Cut(int Start, int End, bool Header, string Insert = "");

    /// <summary>
    /// Every piece of text that defines the entry: tables at or under <c>&lt;key&gt;.rtok</c>, and
    /// key-values whose full path starts there (<c>rtok = { … }</c>, <c>rtok.command = …</c>).
    /// <see cref="Cut.Header"/> marks the <c>[&lt;key&gt;.rtok]</c> table itself.
    /// </summary>
    private static List<Cut> Cuts(McpSpec spec, string text)
    {
        var doc = Syntax(spec, text);
        string[] entry = [.. spec.KeyPath, spec.Server.Name];
        var cuts = new List<Cut>();
        foreach (var kv in doc.KeyValues)
        {
            if (Under(Parts(kv.Key!), entry))
            {
                cuts.Add(Lines(text, kv, header: false));
            }
        }
        foreach (var table in doc.Tables)
        {
            var path = Parts(table.Name!);
            if (Under(path, entry))
            {
                var last =
                    table.Items.ChildrenCount > 0
                        ? (SyntaxNode)table.Items.GetChild(table.Items.ChildrenCount - 1)!
                        : table.Name!;
                var header =
                    table is TableSyntax
                    && path.AsSpan().SequenceEqual(entry, StringComparer.Ordinal);
                cuts.Add(
                    new Cut(LineStart(text, table.Span.Offset), LineEnd(text, End(last)), header)
                );
                continue;
            }
            if (table is TableArraySyntax)
            {
                continue;
            }
            foreach (var kv in table.Items)
            {
                if (Under([.. path, .. Parts(kv.Key!)], entry))
                {
                    cuts.Add(Lines(text, kv, header: false));
                }
            }
        }
        return cuts;
    }

    private static Cut Lines(string text, SyntaxNode node, bool header) =>
        new(LineStart(text, node.Span.Offset), LineEnd(text, End(node)), header);

    /// <summary>Cuts the spans back to front; a cut that ends the file takes the blank line before it too.</summary>
    private static string Splice(string text, List<Cut> cuts)
    {
        var edited = text;
        foreach (var cut in cuts.OrderByDescending(c => c.Start))
        {
            var start = cut.Start;
            if (cut.Insert.Length == 0 && cut.End == edited.Length)
            {
                if (edited.AsSpan(0, start).EndsWith("\r\n\r\n"))
                {
                    start -= 2;
                }
                else if (edited.AsSpan(0, start).EndsWith("\n\n"))
                {
                    start -= 1;
                }
            }
            edited = string.Concat(edited.AsSpan(0, start), cut.Insert, edited.AsSpan(cut.End));
        }
        return edited;
    }

    /// <summary><c>[mcp_servers.rtok]</c> and one <c>key = value</c> line per entry key.</summary>
    private static string Block(McpSpec spec, JsonObject entry, string nl)
    {
        var lines = new List<string> { $"[{KeyPath.Join([.. spec.KeyPath, spec.Server.Name])}]" };
        foreach (var (key, value) in entry)
        {
            lines.Add($"{KeyPath.Segment(key)} = {TomlText.Format(Value(value!))}");
        }
        return string.Join(nl, lines) + nl;
    }

    /// <summary>The edit must parse and leave the entry present (or absent), or it is refused.</summary>
    private static void Check(McpSpec spec, string edited, bool want)
    {
        JsonObject? servers;
        try
        {
            servers = Servers(spec, edited);
        }
        catch (InvalidDataException e)
        {
            throw new InvalidDataException(
                $"{spec.Path}: editing {spec.At} would break the file ({e.Message}); rtok leaves it as it is",
                e
            );
        }
        var has = servers?.ContainsKey(spec.Server.Name) == true;
        if (has != want)
        {
            throw new InvalidDataException(
                $"{spec.Path}: rtok cannot {(want ? "set" : "remove")} {spec.At} in this file (an inline table?); edit it by hand"
            );
        }
    }

    private static TomlTable Model(McpSpec spec, string text)
    {
        try
        {
            return TomlText.Parse(text, spec.Path);
        }
        catch (ConfigFileException e)
        {
            throw new InvalidDataException(e.Message, e);
        }
    }

    private static DocumentSyntax Syntax(McpSpec spec, string text)
    {
        DocumentSyntax doc;
        try
        {
            doc = SyntaxParser.Parse(text, spec.Path, validate: true);
        }
        catch (ArgumentOutOfRangeException e)
        {
            // Tomlyn throws on a \u escape that names a surrogate.
            throw new InvalidDataException($"{spec.Path}: invalid escape: {e.Message}", e);
        }
        if (doc.HasErrors)
        {
            var error = doc.Diagnostics.First(d => d.Kind == DiagnosticMessageKind.Error);
            throw new InvalidDataException(
                $"{spec.Path}:{error.Span.Start.Line + 1}:{error.Span.Start.Column + 1}: {error.Message}"
            );
        }
        return doc;
    }

    private static string[] Parts(KeySyntax key) =>
        [Part(key.Key), .. key.DotKeys.Select(d => Part(d.Key))];

    private static string Part(BareKeyOrStringValueSyntax? key) =>
        key switch
        {
            BareKeySyntax bare => bare.Key?.Text ?? "",
            StringValueSyntax quoted => quoted.Value ?? "",
            _ => key?.ToString()?.Trim() ?? "",
        };

    private static bool Under(string[] path, string[] prefix) =>
        path.Length >= prefix.Length
        && path.AsSpan(0, prefix.Length).SequenceEqual(prefix, StringComparer.Ordinal);

    private static int End(SyntaxNode node) => node.Span.Offset + node.Span.Length;

    private static int LineStart(string text, int at) =>
        at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;

    /// <summary>Past the line break that ends the line <paramref name="at"/> is on; a node's span may already end past it.</summary>
    private static int LineEnd(string text, int at)
    {
        if (at > 0 && text[at - 1] == '\n')
        {
            return at;
        }
        var nl = text.IndexOf('\n', at);
        return nl < 0 ? text.Length : nl + 1;
    }

    private static JsonNode? Json(object? value) =>
        value switch
        {
            TomlTable t => new JsonObject(
                t.Select(kv => KeyValuePair.Create(kv.Key, Json(kv.Value)))
            ),
            TomlTableArray a => new JsonArray([.. a.Select(Json)]),
            TomlArray a => new JsonArray([.. a.Select(Json)]),
            string s => s,
            bool b => b,
            long l => l,
            double d => d,
            null => null,
            _ => value.ToString(),
        };

    private static object Value(JsonNode node) =>
        node switch
        {
            JsonObject o => o.ToDictionary(
                kv => kv.Key,
                kv => Value(kv.Value!),
                StringComparer.Ordinal
            ),
            JsonArray a => a.Select(x => Value(x!)).ToList(),
            JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
            JsonValue v when v.GetValueKind() is JsonValueKind.True or JsonValueKind.False =>
                v.GetValue<bool>(),
            JsonValue v when v.TryGetValue<long>(out var l) => l,
            JsonValue v => v.GetValue<double>(),
            _ => throw new ArgumentException($"no TOML form for {node}", nameof(node)),
        };
}
