using System.Globalization;
using System.Text;

namespace Rtok.Report;

/// <summary>
/// A report table as a TOON tabular array: <c>[n]{k1,k2}:</c>, then one indented row per line.
/// A text cell is quoted when it is empty, has a delimiter, quote, brace or control character,
/// leading or trailing space, or would read as a number, boolean or null. The TOON plugin
/// (plan T28) owns a full encoder; the report needs tables only.
/// </summary>
internal static class Toon
{
    public static string Table(Table table)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"[{table.Rows.Count}]{{")
            .AppendJoin(',', table.Columns.Select(c => Cell(c.Name, numeric: false)))
            .Append("}:");
        foreach (var row in table.Rows)
        {
            sb.Append("\n  ")
                .AppendJoin(
                    ',',
                    row.Select(
                        (cell, i) =>
                            Cell(cell, numeric: i < table.Columns.Count && table.Columns[i].Numeric)
                    )
                );
        }
        return sb.Append('\n').ToString();
    }

    internal static string Cell(string text, bool numeric)
    {
        if (
            numeric
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
        )
        {
            return text;
        }
        return NeedsQuotes(text) ? Quote(text) : text;
    }

    private static bool NeedsQuotes(string text) =>
        text.Length == 0
        || text[0] == ' '
        || text[^1] == ' '
        || text.Any(c =>
            c is ',' or '{' or '}' or '[' or ']' or ':' or '"' or '\\' || char.IsControl(c)
        )
        || text is "true" or "false" or "null"
        || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static string Quote(string text)
    {
        var sb = new StringBuilder("\"", text.Length + 2);
        foreach (var c in text)
        {
            sb.Append(
                c switch
                {
                    '\\' => "\\\\",
                    '"' => "\\\"",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    _ when char.IsControl(c) => $"\\u{(int)c:x4}",
                    _ => c.ToString(),
                }
            );
        }
        return sb.Append('"').ToString();
    }
}
