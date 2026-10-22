using Pyrlyn.TreeSitter;
using Pyrlyn.TreeSitter.Tags;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any source bytes through the tags query of the grammar the first byte picks (T24): no native
/// crash, and every tag's node, name and line ranges lie inside the source, the name inside the
/// node.
/// </summary>
internal static class Outline
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var grammar = Grammar.All[data.IsEmpty ? 0 : data[0] % Grammar.All.Count];
        var source = data.IsEmpty ? [] : data[1..].ToArray();
        using var context = new TagsContext();
        foreach (var tag in context.GenerateTags(grammar.Tags, source).Tags)
        {
            FuzzInput.Check(
                Inside(tag.Range, source.Length)
                    && Inside(tag.NameRange, source.Length)
                    && Inside(tag.LineRange, source.Length),
                $"{grammar.Id}: a range leaves the source"
            );
            FuzzInput.Check(
                tag.Range.Start <= tag.NameRange.Start && tag.NameRange.End <= tag.Range.End,
                $"{grammar.Id}: the name leaves its node"
            );
        }
    }

    private static bool Inside(TextRange range, int length) =>
        range.Start >= 0 && range.Start <= range.End && range.End <= length;
}
