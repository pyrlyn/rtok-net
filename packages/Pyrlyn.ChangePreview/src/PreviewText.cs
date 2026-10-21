using System.Globalization;
using System.Text;

namespace Pyrlyn.ChangePreview;

/// <summary>The text pieces a preview is built from, each usable on its own.</summary>
public static class PreviewText
{
    internal static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private const string Reset = "\e[0m";
    private const string ResetColor = "\e[39m";

    /// <summary>
    /// Colours diff-shaped text: bold file headers, cyan hunk headers, green additions, red
    /// removals; other lines pass through. Whether colour is on is the caller's decision
    /// (the per-stream rule: tty, <c>NO_COLOR</c>, <c>CLICOLOR</c>, <c>TERM=dumb</c>).
    /// </summary>
    /// <param name="text">Text from <see cref="Preview.Render"/> or <see cref="UnifiedDiff.Render"/>.</param>
    /// <param name="color">False returns <paramref name="text"/> unchanged.</param>
    /// <returns>The text with ANSI escapes around each marked line.</returns>
    public static string Paint(string text, bool color)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!color || text.Length == 0)
        {
            return text;
        }
        var lines = text.Split('\n');
        var sb = new StringBuilder(text.Length + lines.Length * 10);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }
            var line = lines[i];
            if (
                line.StartsWith("+++", StringComparison.Ordinal)
                || line.StartsWith("---", StringComparison.Ordinal)
            )
            {
                sb.Append("\e[1m").Append(line).Append(Reset);
            }
            else if (line.StartsWith('@'))
            {
                sb.Append("\e[36m").Append(line).Append(ResetColor);
            }
            else if (line.StartsWith('+'))
            {
                sb.Append("\e[32m").Append(line).Append(ResetColor);
            }
            else if (line.StartsWith('-'))
            {
                sb.Append("\e[31m").Append(line).Append(ResetColor);
            }
            else
            {
                sb.Append(line);
            }
        }
        return sb.ToString();
    }

    /// <summary><c>1023 B</c>, <c>1.0 KB</c>, <c>1.5 MB</c>: one decimal past bytes, binary units, up to TB.</summary>
    /// <param name="bytes">A size in bytes.</param>
    /// <returns>The size for people.</returns>
    public static string HumanBytes(long bytes)
    {
        const double Kb = 1024;
        if (bytes < Kb)
        {
            return string.Create(Culture, $"{bytes} B");
        }
        var n = (double)bytes;
        foreach (var (unit, div) in new[] { ("KB", Kb), ("MB", Kb * Kb), ("GB", Kb * Kb * Kb) })
        {
            if (n < div * Kb)
            {
                return string.Create(Culture, $"{n / div:F1} {unit}");
            }
        }
        return string.Create(Culture, $"{n / (Kb * Kb * Kb * Kb):F1} TB");
    }

    /// <summary><c>1204311</c> as <c>1 204 311</c>: a seven-digit file count is unreadable ungrouped.</summary>
    /// <param name="n">A count.</param>
    /// <returns>The digits in groups of three, split by spaces.</returns>
    public static string Group(long n)
    {
        var digits = n.ToString(Culture);
        var sb = new StringBuilder(digits.Length + digits.Length / 3);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && char.IsAsciiDigit(digits[i - 1]) && (digits.Length - i) % 3 == 0)
            {
                sb.Append(' ');
            }
            sb.Append(digits[i]);
        }
        return sb.ToString();
    }

    internal static string Plural(long n, string noun) => $"{Group(n)} {noun}{(n == 1 ? "" : "s")}";

    internal static string EditTotals(PreviewTotals t)
    {
        var parts = new List<string> { $"{Plural(t.FilesChanged, "file")} changed" };
        if (t.Insertions > 0)
        {
            parts.Add($"{Plural(t.Insertions, "insertion")}(+)");
        }
        if (t.Deletions > 0)
        {
            parts.Add($"{Plural(t.Deletions, "deletion")}(-)");
        }
        return string.Join(", ", parts);
    }

    internal static string RemovalTotals(PreviewTotals t) =>
        $"{Plural(t.PathsRemoved, "path")}, {Plural(t.FilesRemoved, "file")}, -{HumanBytes(t.BytesRemoved)}";
}
