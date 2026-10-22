namespace Rtok.Plugins.Read;

/// <summary>Lines as rtok counts them: split on <c>\n</c> or <c>\r\n</c>, and
/// no empty last line after a final newline. Line numbers in every outline and graph answer
/// come from this.</summary>
public static class TextLines
{
    /// <summary>The lines of <paramref name="text"/>.</summary>
    public static IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var nl = text.IndexOf('\n', start);
            var end = nl < 0 ? text.Length : nl;
            var cut = nl >= 0 && end > start && text[end - 1] == '\r' ? end - 1 : end;
            lines.Add(text[start..cut]);
            if (nl < 0)
            {
                break;
            }

            start = nl + 1;
        }

        return lines;
    }
}
