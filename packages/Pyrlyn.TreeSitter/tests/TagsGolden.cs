using System.Text;
using Pyrlyn.TreeSitter.Tags;

namespace Pyrlyn.TreeSitter.Tests;

// The golden of a fixture: its tags, one line each. Shared with tests/Rtok.AotTests, which
// runs the same fixtures through the published NativeAOT binary.
internal static class TagsGolden
{
    // fixtures/<grammar id>.<extension>, next to <name>.tags.
    public static IEnumerable<string> Fixtures(string directory) =>
        Directory
            .EnumerateFiles(directory)
            .Where(f =>
                !f.EndsWith(".tags", StringComparison.Ordinal)
                && !f.EndsWith(".received", StringComparison.Ordinal)
            )
            .Order(StringComparer.Ordinal);

    // The expected and the actual golden. A mismatch leaves <fixture>.tags.received beside the
    // copy in the output folder; review it and copy it over the checked-in .tags file.
    public static (string Expected, string Actual) Check(string fixture)
    {
        var name = Path.GetFileNameWithoutExtension(fixture);
        var grammar =
            Grammar.Find(Path.GetExtension(fixture).TrimStart('.'))
            ?? throw new InvalidOperationException($"no grammar for {fixture}");
        if (!string.Equals(grammar.Id, name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{fixture} selects grammar {grammar.Id}");
        }
        var actual = Render(grammar, File.ReadAllBytes(fixture));
        var golden = fixture + ".tags";
        var expected = File.Exists(golden) ? File.ReadAllText(golden) : "";
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            File.WriteAllText(golden + ".received", actual);
        }
        return (expected, actual);
    }

    public static string Render(Grammar grammar, byte[] source)
    {
        using var context = new TagsContext();
        var config = grammar.Tags;
        var result = context.GenerateTags(config, source);
        var text = new StringBuilder();
        text.Append("has_error=").Append(result.HasError ? "true" : "false").Append('\n');
        foreach (var tag in result.Tags)
        {
            text.Append(tag.SpanStart.Row + 1)
                .Append(':')
                .Append(tag.SpanStart.Column + 1)
                .Append(tag.IsDefinition ? " def " : " ref ")
                .Append(config.SyntaxTypeName(tag.SyntaxTypeId))
                .Append(' ')
                .Append(Slice(source, tag.NameRange))
                .Append(" range=")
                .Append(tag.Range.Start)
                .Append("..")
                .Append(tag.Range.End)
                .Append(" utf16=")
                .Append(tag.Utf16ColumnRange.Start)
                .Append("..")
                .Append(tag.Utf16ColumnRange.End)
                .Append(" line=")
                .Append(Quote(Slice(source, tag.LineRange)));
            if (tag.Docs is { } docs)
            {
                text.Append(" docs=").Append(Quote(docs));
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    private static string Slice(byte[] source, TextRange range) =>
        Encoding.UTF8.GetString(source, range.Start, range.Length);

    private static string Quote(string value) =>
        "\""
        + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
        + "\"";
}
