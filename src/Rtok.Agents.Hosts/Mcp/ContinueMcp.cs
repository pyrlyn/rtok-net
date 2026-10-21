using System.Text.Json.Nodes;

namespace Rtok.Agents.Hosts.Mcp;

/// <summary>
/// Continue's <c>config.yaml</c> (VS Code, JetBrains, <c>cn</c>): rtok is the <c>mcpServers</c>
/// list item named <c>rtok</c>. Only that item's lines change.
/// </summary>
internal sealed class ContinueMcp(string path) : McpFile("continue", path)
{
    private const string Key = "mcpServers";

    /// <summary>The keys compared: <c>env</c>, <c>cwd</c>, <c>connectionTimeout</c> are the user's.</summary>
    private static readonly string[] Keys = ["name", "type", "command", "args"];

    public override string At => "mcpServers.rtok";

    public override JsonObject Ours(string command) =>
        new()
        {
            ["name"] = "rtok",
            ["type"] = "stdio",
            ["command"] = command,
            ["args"] = Args(),
        };

    public override JsonNode? Entry(string text)
    {
        var lines = Parsed(text);
        return Locate(lines)?.Rtok is { } rtok ? Core(rtok.Value, Keys) : null;
    }

    public override string With(string text, string command)
    {
        var lines = Parsed(text);
        var eol = YamlText.Eol(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"name: Local Config{eol}version: 1.0.0{eol}schema: v1{eol}{Key}:{eol}{Render(2, command, eol)}";
        }
        return Locate(lines) switch
        {
            { Rtok: { } r } b => YamlText.Splice(
                lines,
                r.Start,
                r.End,
                Render(b.Indent, command, eol),
                eol
            ),
            { EmptyFlow: true } b => YamlText.Splice(
                lines,
                b.Header,
                b.Header + 1,
                $"{Key}:{eol}{Render(2, command, eol)}",
                eol
            ),
            { } b => YamlText.Splice(lines, b.End, b.End, Render(b.Indent, command, eol), eol),
            null => YamlText.Splice(
                lines,
                lines.Count,
                lines.Count,
                $"{Key}:{eol}{Render(2, command, eol)}",
                eol
            ),
        };
    }

    public override string Without(string text)
    {
        var lines = Parsed(text);
        if (Locate(lines) is not { Rtok: { } r } b)
        {
            return text;
        }
        return b.Items == 1
            ? YamlText.Splice(lines, b.Header, b.End, "", "")
            : YamlText.Splice(lines, r.Start, r.End, "", "");
    }

    private List<string> Parsed(string text)
    {
        YamlText.Validate(text, Path, Key, v => v is JsonArray, "a list");
        return YamlText.Lines(text);
    }

    private static string Render(int indent, string command, string eol)
    {
        var p = new string(' ', indent);
        var q = new string(' ', indent + 2);
        return string.Concat(
            $"{p}- name: rtok{eol}",
            $"{q}type: stdio{eol}",
            $"{q}command: {YamlText.Scalar(command)}{eol}",
            $"{q}args: [mcp, --host, continue]{eol}"
        );
    }

    /// <summary>The <c>mcpServers:</c> block: its header, the line past it, the items' indent, how many items, and rtok's item.</summary>
    private sealed record Block(
        int Header,
        int End,
        int Indent,
        int Items,
        bool EmptyFlow,
        Item? Rtok
    );

    private sealed record Item(int Start, int End, JsonNode? Value);

    /// <exception cref="InvalidDataException"><c>mcpServers:</c> holds a flow list other than <c>[]</c>, or a scalar.</exception>
    private static Block? Locate(List<string> lines)
    {
        var header = lines.FindIndex(l => l.StartsWith(Key + ":", StringComparison.Ordinal));
        if (header < 0)
        {
            return null;
        }
        var tail = YamlText.Text(lines[header])[(Key.Length + 1)..].Trim();
        if (string.Equals(tail, "[]", StringComparison.Ordinal))
        {
            return new Block(header, header + 1, 2, 0, EmptyFlow: true, null);
        }
        if (tail.Length > 0 && !tail.StartsWith('#'))
        {
            throw new InvalidDataException(
                $"`{Key}:` is not a block list (`{tail}`); add the rtok item by hand"
            );
        }
        // A list may sit at its key's own indent: only an unindented line that is not an item ends it.
        var stop = header + 1;
        while (
            stop < lines.Count
            && (
                YamlText.Skippable(lines[stop])
                || YamlText.Indent(lines[stop]) > 0
                || YamlText.Text(lines[stop]).StartsWith('-')
            )
        )
        {
            stop++;
        }
        var end = YamlText.PastLastContent(lines, header + 1, stop);
        var first = lines.FindIndex(header + 1, end - header - 1, l => !YamlText.Skippable(l));
        var indent = first < 0 ? 2 : YamlText.Indent(lines[first]);
        var (count, rtok) = Items(lines, header + 1, end, indent);
        return new Block(header, end, indent, count, EmptyFlow: false, rtok);
    }

    /// <summary>The number of items at <paramref name="indent"/> in <c>[from, end)</c>, and rtok's item among them.</summary>
    private static (int Count, Item? Rtok) Items(List<string> lines, int from, int end, int indent)
    {
        var starts = Enumerable
            .Range(from, end - from)
            .Where(i =>
                !YamlText.Skippable(lines[i])
                && YamlText.Indent(lines[i]) == indent
                && YamlText.Text(lines[i])[indent..] is var t
                && (
                    string.Equals(t, "-", StringComparison.Ordinal)
                    || t.StartsWith("- ", StringComparison.Ordinal)
                )
            )
            .ToList();
        Item? rtok = null;
        for (var n = 0; n < starts.Count && rtok is null; n++)
        {
            var next = n + 1 < starts.Count ? starts[n + 1] : end;
            var itemEnd = YamlText.PastLastContent(lines, starts[n], next);
            var value = (
                YamlText.ParseBlock(lines, starts[n], itemEnd, indent) as JsonArray
            )?.FirstOrDefault();
            if (
                value is JsonObject o
                && o["name"] is JsonValue name
                && name.TryGetValue<string>(out var named)
                && string.Equals(named, "rtok", StringComparison.Ordinal)
            )
            {
                rtok = new Item(starts[n], itemEnd, value);
            }
        }
        return (starts.Count, rtok);
    }
}
