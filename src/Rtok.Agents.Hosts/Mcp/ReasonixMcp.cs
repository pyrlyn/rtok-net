using System.Text.Json.Nodes;
using Pyrlyn.Config.Layered;
using Rtok.Mcp.Install;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Rtok.Agents.Hosts.Mcp;

/// <summary>
/// Reasonix's <c>config.toml</c>: MCP servers are <c>[[plugins]]</c> tables, rtok's the one named
/// <c>rtok</c>. Any table under that name counts as installed, whoever wrote it; edits splice
/// text, so every other byte stays.
/// </summary>
internal sealed class ReasonixMcp(string path) : McpFile("reasonix", path)
{
    private const string Key = "plugins";
    private const string Name = "rtok";

    public override string At => "plugins.rtok";

    public override JsonObject Ours(string command) =>
        new()
        {
            ["name"] = Name,
            ["command"] = command,
            ["args"] = Args(),
        };

    public override JsonNode? Entry(string text) =>
        Plugins(text)?.FirstOrDefault(IsOurs) is { } table ? TomlServers.Json(table) : null;

    /// <summary>A table named <c>rtok</c> is kept as it is, whoever wrote it.</summary>
    public override bool Current(JsonNode have, string command) => true;

    public override bool Serves(JsonNode have, Func<string, bool> isBin) => true;

    public override string With(string text, string command)
    {
        Plugins(text);
        var nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string[] block =
        [
            $"[[{Key}]]",
            $"name = {TomlText.Format(Name)}",
            $"command = {TomlText.Format(command)}",
            $"args = {TomlText.Format(new List<object> { "mcp", "--host", Host })}",
        ];
        var gap =
            text.Length == 0 ? ""
            : text.EndsWith('\n') ? nl
            : nl + nl;
        return text + gap + string.Join(nl, block) + nl;
    }

    public override string Without(string text)
    {
        Plugins(text);
        var doc = SyntaxParser.Parse(text, Path, validate: true);
        foreach (var table in doc.Tables)
        {
            if (
                table is not TableArraySyntax array
                || !string.Equals(TomlName(array), Key, StringComparison.Ordinal)
                || !Named(array)
            )
            {
                continue;
            }
            var last =
                array.Items.ChildrenCount > 0
                    ? (SyntaxNode)array.Items.GetChild(array.Items.ChildrenCount - 1)!
                    : array.Name!;
            var start = TomlServers.LineStart(text, array.Span.Offset);
            var end = TomlServers.LineEnd(text, TomlServers.End(last));
            // The blank line that set the table apart goes with it.
            var before = text.AsSpan(0, start);
            if (end == text.Length && before.EndsWith("\r\n\r\n"))
            {
                start -= 2;
            }
            else if (end == text.Length && before.EndsWith("\n\n"))
            {
                start -= 1;
            }
            return string.Concat(text.AsSpan(0, start), text.AsSpan(end));
        }
        return text;
    }

    /// <summary>The <c>[[plugins]]</c> tables; null when there are none.</summary>
    /// <exception cref="InvalidDataException">The file does not parse, or <c>plugins</c> is something else.</exception>
    private TomlTableArray? Plugins(string text)
    {
        TomlTable doc;
        try
        {
            doc = TomlText.Parse(text, Path);
        }
        catch (ConfigFileException e)
        {
            throw new InvalidDataException(e.Message, e);
        }
        return doc.TryGetValue(Key, out var plugins)
            ? plugins as TomlTableArray
                ?? throw new InvalidDataException($"{Path}: `{Key}` is not an array of tables")
            : null;
    }

    private static bool IsOurs(TomlTable table) =>
        table.TryGetValue("name", out var name) && name is Name;

    private static string TomlName(TableArraySyntax array) => array.Name?.ToString()?.Trim() ?? "";

    private static bool Named(TableArraySyntax array) =>
        array.Items.Any(kv =>
            string.Equals(kv.Key?.ToString()?.Trim(), "name", StringComparison.Ordinal)
            && kv.Value is StringValueSyntax { Value: Name }
        );
}
