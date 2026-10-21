using System.Net;

namespace Pyrlyn.CommandLine.Docs;

/// <summary>One parsed <c>--help</c> page of ConsoleAppFramework 5.</summary>
/// <param name="Usage">The names in the usage line: <c>""</c> for the root, <c>config get</c>, or <c>ls|list</c> for a command with aliases.</param>
/// <param name="Description">The summary, one line per <c>\n</c>.</param>
/// <param name="Arguments">The Arguments section.</param>
/// <param name="Options">The Options section.</param>
/// <param name="Commands">The Commands section (root only): each entry's names, primary first, and its description.</param>
internal sealed record HelpPage(
    string Usage,
    string Description,
    IReadOnlyList<ArgumentInfo> Arguments,
    IReadOnlyList<OptionInfo> Options,
    IReadOnlyList<(IReadOnlyList<string> Names, string Description)> Commands
)
{
    /// <summary>Parses one help page.</summary>
    /// <param name="text">The page, as the program printed it.</param>
    /// <returns>The page.</returns>
    /// <exception cref="HelpFormatException">A line is not part of the format.</exception>
    public static HelpPage Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        var usage = Patterns.Usage().Match(lines[0]);
        if (!usage.Success)
        {
            throw Error(1, lines[0], "expected \"Usage: ...\"");
        }
        var parser = new Parser();
        for (var i = 1; i < lines.Length; i++)
        {
            parser.Feed(i + 1, lines[i]);
        }
        parser.Flush();
        return new HelpPage(
            usage.Groups["names"].Value,
            Plain(string.Join('\n', parser.Description).TrimEnd('\n')),
            parser.Arguments,
            parser.Options,
            parser.Commands
        );
    }

    /// <summary>The page after the usage line: the description, then the sections' rows.</summary>
    private sealed class Parser
    {
        private readonly List<string> _seen = [];
        private string? _section;
        private Row? _row;

        internal List<string> Description { get; } = [];
        internal List<ArgumentInfo> Arguments { get; } = [];
        internal List<OptionInfo> Options { get; } = [];
        internal List<(IReadOnlyList<string> Names, string Description)> Commands { get; } = [];

        internal void Feed(int number, string line)
        {
            if (line.Length == 0)
            {
                Flush();
                if (_section is null && Description.Count > 0)
                {
                    Description.Add("");
                }
                return;
            }
            if (line is "Arguments:" or "Options:" or "Commands:")
            {
                Flush();
                var name = line[..^1];
                if (_seen.Count > 0 && Order(name) <= Order(_seen[^1]))
                {
                    throw Error(number, line, "sections out of order or repeated");
                }
                _seen.Add(name);
                _section = name;
                return;
            }
            if (_section is null)
            {
                if (number == 2)
                {
                    throw Error(number, line, "expected a blank line after the usage line");
                }
                Description.Add(line.Trim());
                return;
            }
            var match = Patterns.Row().Match(line);
            if (match.Success)
            {
                Flush();
                _row = new Row(number, line, match.Groups["head"].Value, []);
                if (match.Groups["text"].Success)
                {
                    _row.Text.Add(match.Groups["text"].Value.Trim());
                }
                return;
            }
            if (_row is not null && line.StartsWith("   ", StringComparison.Ordinal))
            {
                // A <param> or <summary> written over several lines keeps its indent.
                _row.Text.Add(line.Trim());
                return;
            }
            throw Error(number, line, $"not a row of the {_section} section");
        }

        internal void Flush()
        {
            if (_row is null)
            {
                return;
            }
            switch (_section)
            {
                case "Arguments":
                    Arguments.Add(ParseArgument(_row));
                    break;
                case "Options":
                    Options.Add(ParseOption(_row));
                    break;
                default:
                    Commands.Add((_row.Head.Split(", "), Plain(string.Join('\n', _row.Text))));
                    break;
            }
            _row = null;
        }
    }

    private static int Order(string section) =>
        section switch
        {
            "Arguments" => 0,
            "Options" => 1,
            _ => 2,
        };

    private static ArgumentInfo ParseArgument(Row row)
    {
        var head = Patterns.ArgumentHead().Match(row.Head);
        if (!head.Success)
        {
            throw Error(row.Number, row.Line, "expected \"[<index>] <type>\"");
        }
        var (text, _, _) = SplitMarker(row);
        return new ArgumentInfo(head.Groups["type"].Value, text);
    }

    private static OptionInfo ParseOption(Row row)
    {
        var head = Patterns.OptionHead().Match(row.Head);
        if (!head.Success)
        {
            throw Error(row.Number, row.Line, "expected \"-x, --name <type>\"");
        }
        var (text, value, required) = SplitMarker(row);
        return new OptionInfo(
            head.Groups["names"].Value.Split(", "),
            head.Groups["type"].Success ? head.Groups["type"].Value : null,
            text,
            value,
            required
        );
    }

    /// <summary>Splits <c>[Default: …]</c> or <c>[Required]</c> off the end of a row's text.</summary>
    private static (string Text, string? Default, bool Required) SplitMarker(Row row)
    {
        var text = string.Join('\n', row.Text);
        if (text.EndsWith("[Required]", StringComparison.Ordinal))
        {
            return (Plain(text[..^"[Required]".Length].TrimEnd()), null, true);
        }
        var at = text.LastIndexOf("[Default: ", StringComparison.Ordinal);
        if (at < 0 || !text.EndsWith(']'))
        {
            return (Plain(text), null, false);
        }
        var raw = text[(at + "[Default: ".Length)..^1];
        string? value = raw switch
        {
            "null" => null,
            _ when raw.StartsWith("@\"", StringComparison.Ordinal)
                    && raw.EndsWith('"')
                    && raw.Length >= 3 => raw[2..^1]
                .Replace("\"\"", "\"", StringComparison.Ordinal),
            _ => raw,
        };
        return (Plain(text[..at].TrimEnd()), value, false);
    }

    /// <summary>
    /// Doc-comment XML as plain text: ConsoleAppFramework prints <c>&lt;c&gt;7d&lt;/c&gt;</c> and
    /// <c>&amp;amp;</c> as written. References keep their name, other tags go, entities decode.
    /// </summary>
    internal static string Plain(string text) =>
        text.Contains('<', StringComparison.Ordinal) || text.Contains('&', StringComparison.Ordinal)
            ? WebUtility.HtmlDecode(Patterns.Tag().Replace(text, m => m.Groups["name"].Value))
            : text;

    private static HelpFormatException Error(int number, string line, string why) =>
        new($"help line {number}: {why}: \"{line}\"");

    private sealed record Row(int Number, string Line, string Head, List<string> Text);
}
