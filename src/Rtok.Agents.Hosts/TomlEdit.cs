using Pyrlyn.Config.Layered;
using Rtok.Mcp.Install;
using Tomlyn.Model;
using Tomlyn.Syntax;
using Cut = Rtok.Mcp.Install.TomlServers.Cut;

namespace Rtok.Agents.Hosts;

/// <summary>
/// A TOML file under edit (hooks, proxy), as text: Tomlyn's syntax tree finds the tables and
/// their values, and every edit splices only those bytes (the way
/// <see cref="TomlServers"/> edits MCP tables). Edits are collected, then applied at once.
/// </summary>
internal sealed class TomlEdit
{
    private readonly string path;
    private readonly string bom;
    private readonly string text;
    private readonly List<Cut> cuts = [];
    private readonly List<string> blocks = [];

    /// <exception cref="InvalidDataException">The text is not TOML.</exception>
    public TomlEdit(string path, string text)
    {
        this.path = path;
        // Tomlyn's offsets start after a BOM.
        (bom, this.text) = text.StartsWith('﻿') ? ("﻿", text[1..]) : ("", text);
        Nl = this.text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        Doc = TomlServers.Syntax(path, this.text);
        Model = Parse(this.text);
    }

    /// <summary>The file's line break.</summary>
    public string Nl { get; }

    /// <summary>The syntax tree of the text as read.</summary>
    public DocumentSyntax Doc { get; }

    /// <summary>The values of the text as read.</summary>
    public TomlTable Model { get; }

    /// <summary>One <c>[name]</c> or <c>[[name]]</c> table: its lines and its single-part keys.</summary>
    public sealed record Table(int Start, int End, IReadOnlyDictionary<string, KeyValueSyntax> Keys)
    {
        public string? Str(string key) =>
            Keys.TryGetValue(key, out var kv) && kv.Value is StringValueSyntax s ? s.Value : null;

        public long? Int(string key) =>
            Keys.TryGetValue(key, out var kv) && kv.Value is IntegerValueSyntax i ? i.Value : null;
    }

    /// <summary>The <c>[[&lt;name&gt;]]</c> tables, in file order.</summary>
    public List<Table> Tables(params string[] name) => Tables(true, name);

    /// <summary>The <c>[&lt;name&gt;]</c> tables (<paramref name="arrays"/> false) or <c>[[&lt;name&gt;]]</c> ones, in file order.</summary>
    public List<Table> Tables(bool arrays, params string[] name)
    {
        var found = new List<Table>();
        foreach (var t in Doc.Tables)
        {
            if (
                t is TableArraySyntax != arrays
                || !TomlServers.Parts(t.Name!).AsSpan().SequenceEqual(name, StringComparer.Ordinal)
            )
            {
                continue;
            }
            var keys = new Dictionary<string, KeyValueSyntax>(StringComparer.Ordinal);
            foreach (var kv in t.Items)
            {
                if (TomlServers.Parts(kv.Key!) is [var key])
                {
                    keys.TryAdd(key, kv);
                }
            }
            var last =
                t.Items.ChildrenCount > 0
                    ? (SyntaxNode)t.Items.GetChild(t.Items.ChildrenCount - 1)!
                    : t.Name!;
            found.Add(
                new Table(
                    TomlServers.LineStart(text, t.Span.Offset),
                    TomlServers.LineEnd(text, TomlServers.End(last)),
                    keys
                )
            );
        }
        return found;
    }

    /// <summary>Takes <paramref name="table"/> out.</summary>
    public void Drop(Table table) => cuts.Add(new Cut(table.Start, table.End));

    /// <summary>Sets <paramref name="key"/> of <paramref name="table"/>: the value in place, or a line at its end.</summary>
    public void Set(Table table, string key, object value)
    {
        var formatted = TomlText.Format(value);
        if (table.Keys.TryGetValue(key, out var kv) && kv.Value is { } old)
        {
            cuts.Add(new Cut(old.Span.Offset, TomlServers.End(old), Insert: formatted));
            return;
        }
        var at = table.End;
        var lead = at > 0 && text[at - 1] != '\n' ? Nl : "";
        cuts.Add(new Cut(at, at, Insert: $"{lead}{key} = {formatted}{Nl}"));
    }

    /// <summary>The root key <paramref name="key"/> (before the first table); null when absent.</summary>
    public KeyValueSyntax? Root(string key) =>
        Doc.KeyValues.FirstOrDefault(kv =>
            TomlServers.Parts(kv.Key!) is [var k] && string.Equals(k, key, StringComparison.Ordinal)
        );

    /// <summary>What follows <paramref name="kv"/>'s value on its line, trimmed: its comment.</summary>
    public string Tail(KeyValueSyntax kv) =>
        text[TomlServers.End(kv.Value!)..TomlServers.LineEnd(text, TomlServers.End(kv.Value!))]
            .Trim();

    /// <summary>Rewrites <paramref name="kv"/>'s value and the rest of its line to <paramref name="rest"/>.</summary>
    public void SetLine(KeyValueSyntax kv, string rest)
    {
        var end = TomlServers.LineEnd(text, TomlServers.End(kv.Value!));
        while (end > 0 && text[end - 1] is '\n' or '\r')
        {
            end--;
        }
        cuts.Add(new Cut(kv.Value!.Span.Offset, end, Insert: rest));
    }

    /// <summary>Takes the root key <paramref name="kv"/>'s line out.</summary>
    public void DropLine(KeyValueSyntax kv) =>
        cuts.Add(
            new Cut(
                TomlServers.LineStart(text, kv.Span.Offset),
                TomlServers.LineEnd(text, TomlServers.End(kv.Value!))
            )
        );

    /// <summary>Inserts a root <paramref name="line"/>: after the last root key, else before the first table, else at the end.</summary>
    public void InsertRoot(string line)
    {
        if (Doc.KeyValues.ChildrenCount > 0)
        {
            InsertAfter(Doc.KeyValues.GetChild(Doc.KeyValues.ChildrenCount - 1)!, line);
            return;
        }
        if (Doc.Tables.ChildrenCount > 0)
        {
            var at = TomlServers.LineStart(text, Doc.Tables.GetChild(0)!.Span.Offset);
            cuts.Add(new Cut(at, at, Insert: line + Nl));
            return;
        }
        var lead = text.Length > 0 && !text.EndsWith('\n') ? Nl : "";
        cuts.Add(new Cut(text.Length, text.Length, Insert: lead + line + Nl));
    }

    /// <summary>Inserts <paramref name="line"/> right after the line holding <paramref name="node"/>.</summary>
    public void InsertAfter(SyntaxNode node, string line)
    {
        var at = TomlServers.LineEnd(text, TomlServers.End(node));
        var lead = at > 0 && text[at - 1] != '\n' ? Nl : "";
        cuts.Add(new Cut(at, at, Insert: lead + line + Nl));
    }

    /// <summary>Appends a table headed <paramref name="header"/> of <paramref name="values"/>, a blank line before it.</summary>
    public void Append(string header, IEnumerable<(string Key, object Value)> values) =>
        blocks.Add(Block(header, values));

    /// <summary>Puts a table headed <paramref name="header"/> of <paramref name="values"/> where <paramref name="table"/> is.</summary>
    public void Replace(
        Table table,
        string header,
        IEnumerable<(string Key, object Value)> values
    ) => cuts.Add(new Cut(table.Start, table.End, Insert: Block(header, values)));

    private string Block(string header, IEnumerable<(string Key, object Value)> values) =>
        string.Join(Nl, [header, .. values.Select(v => $"{v.Key} = {TomlText.Format(v.Value)}")])
        + Nl;

    /// <summary>The edited text; it must still parse.</summary>
    /// <exception cref="InvalidDataException">The edit would break the file.</exception>
    public string Edited()
    {
        var edited = TomlServers.Splice(text, cuts);
        foreach (var block in blocks)
        {
            var gap =
                edited.Length == 0 ? ""
                : edited.EndsWith('\n') ? Nl
                : Nl + Nl;
            edited = edited + gap + block;
        }
        try
        {
            Parse(edited);
        }
        catch (InvalidDataException e)
        {
            throw new InvalidDataException(
                $"{path}: editing rtok's entries would break the file ({e.Message}); rtok leaves it as it is",
                e
            );
        }
        return bom + edited;
    }

    /// <summary>The error for a file whose hooks are not spelled as rtok edits them.</summary>
    public InvalidDataException Shape(string what) =>
        new($"{path}: {what}; rtok leaves the file as it is, edit it by hand");

    private TomlTable Parse(string toml)
    {
        try
        {
            return TomlText.Parse(toml, path);
        }
        catch (ConfigFileException e)
        {
            throw new InvalidDataException(e.Message, e);
        }
    }
}
