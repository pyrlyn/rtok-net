using System.Text.Json.Nodes;

namespace Rtok.Agents.Hosts.Mcp;

/// <summary>
/// Goose's <c>config.yaml</c>, shared by the CLI and Desktop: rtok is the <c>extensions.rtok</c>
/// mapping (rtok T413.x goose). Only rtok's lines change.
/// </summary>
internal sealed class GooseMcp(string path) : McpFile("goose", path)
{
    private const string Key = "extensions";

    /// <summary>The keys compared: Goose re-saves <c>envs</c>, <c>timeout</c> and more around them.</summary>
    private static readonly string[] Keys = ["name", "type", "cmd", "args", "enabled"];

    public override string At => "extensions.rtok";

    public override JsonObject Ours(string command) =>
        new()
        {
            ["name"] = "rtok",
            ["type"] = "stdio",
            ["cmd"] = command,
            ["args"] = Args(),
            ["enabled"] = true,
        };

    public override JsonNode? Entry(string text)
    {
        var lines = Parsed(text);
        if (Locate(lines) is not { Rtok: { } rtok } block)
        {
            return null;
        }
        var parsed =
            YamlText.ParseBlock(lines, rtok.Start, rtok.End, block.Indent)
            ?? throw new InvalidDataException($"{At} is not readable YAML; remove it by hand");
        return Core(
            (parsed as JsonObject)?["rtok"] ?? throw new InvalidDataException($"{At} is empty"),
            Keys
        );
    }

    public override string With(string text, string command)
    {
        var lines = Parsed(text);
        var eol = YamlText.Eol(text);
        return Locate(lines) switch
        {
            { Rtok: { } r } => YamlText.Splice(
                lines,
                r.Start,
                r.End,
                Render(YamlText.Indent(lines[r.Start]), command, eol),
                eol
            ),
            { } b => YamlText.Splice(lines, b.End, b.End, Render(b.Indent, command, eol), eol),
            null => YamlText.Splice(
                lines,
                lines.Count,
                lines.Count,
                Key + ":" + eol + Render(2, command, eol),
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
        var alone = Enumerable
            .Range(b.Header + 1, b.End - b.Header - 1)
            .Where(i => i < r.Start || i >= r.End)
            .All(i => YamlText.Text(lines[i]).Trim().Length == 0);
        return alone
            ? YamlText.Splice(lines, b.Header, b.End, "", "")
            : YamlText.Splice(lines, r.Start, r.End, "", "");
    }

    private List<string> Parsed(string text)
    {
        YamlText.Validate(text, Path, Key, v => v is JsonObject, "a mapping");
        return YamlText.Lines(text);
    }

    private static string Render(int indent, string command, string eol)
    {
        var p = new string(' ', indent);
        var q = new string(' ', indent + 2);
        return string.Concat(
            $"{p}rtok:{eol}",
            $"{q}name: rtok{eol}",
            $"{q}type: stdio{eol}",
            $"{q}cmd: {YamlText.Scalar(command)}{eol}",
            $"{q}args: [mcp, --host, goose]{eol}",
            $"{q}enabled: true{eol}"
        );
    }

    /// <summary>The <c>extensions:</c> block: its header line, the line past its last child, the children's indent, and rtok's child.</summary>
    private sealed record Block(int Header, int End, int Indent, Range? Rtok);

    private sealed record Range(int Start, int End);

    /// <exception cref="InvalidDataException"><c>extensions:</c> holds a flow mapping or a scalar on its own line.</exception>
    private static Block? Locate(List<string> lines)
    {
        var header = lines.FindIndex(l => l.StartsWith(Key + ":", StringComparison.Ordinal));
        if (header < 0)
        {
            return null;
        }
        var tail = YamlText.Text(lines[header])[(Key.Length + 1)..].Trim();
        if (tail.Length > 0 && !tail.StartsWith('#'))
        {
            throw new InvalidDataException(
                $"`{Key}:` is not a block mapping (`{tail}`); add extensions.rtok by hand"
            );
        }
        var stop = header + 1;
        while (
            stop < lines.Count
            && (YamlText.Skippable(lines[stop]) || YamlText.Indent(lines[stop]) > 0)
        )
        {
            stop++;
        }
        var end = YamlText.PastLastContent(lines, header + 1, stop);
        var first = lines.FindIndex(header + 1, end - header - 1, l => !YamlText.Skippable(l));
        var indent = first < 0 ? 2 : YamlText.Indent(lines[first]);
        Range? rtok = null;
        for (var i = header + 1; i < end; i++)
        {
            if (YamlText.Skippable(lines[i]) || YamlText.Indent(lines[i]) != indent)
            {
                continue;
            }
            var key = YamlText.Text(lines[i])[indent..];
            if (
                !(
                    key.StartsWith("rtok:", StringComparison.Ordinal)
                    || key.StartsWith("\"rtok\":", StringComparison.Ordinal)
                    || key.StartsWith("'rtok':", StringComparison.Ordinal)
                )
            )
            {
                continue;
            }
            var last = i + 1;
            for (var j = i + 1; j < end; j++)
            {
                if (YamlText.Skippable(lines[j]))
                {
                    continue;
                }
                if (YamlText.Indent(lines[j]) <= indent)
                {
                    break;
                }
                last = j + 1;
            }
            rtok = new Range(i, last);
            break;
        }
        return new Block(header, end, indent, rtok);
    }
}
