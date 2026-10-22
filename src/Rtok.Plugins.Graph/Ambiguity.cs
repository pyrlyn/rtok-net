using System.Globalization;
using System.Text;

namespace Rtok.Plugins.Graph;

/// <summary>Marks for a name with several definitions: a leading banner, and <c> ?</c> after
/// every line the answer lists for it.</summary>
internal static class Ambiguity
{
    public static string Banner(int names) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{names} names ambiguous (?): narrow with path or kind, or backend = \"lsp\"\n"
        );

    public static string MarkLines(string text)
    {
        if (text.Length == 0)
        {
            return "";
        }

        var output = new StringBuilder();
        foreach (var line in Rtok.Plugins.Read.TextLines.Split(text))
        {
            output.Append(line).Append(" ?\n");
        }

        return output.ToString();
    }

    /// <summary>Banner and marks when <paramref name="defs"/> definitions share the name — all
    /// of them in the answer's scope, not only the shown ones.</summary>
    public static string Flag(int defs, string text) =>
        defs > 1 ? Banner(1) + MarkLines(text) : text;
}
