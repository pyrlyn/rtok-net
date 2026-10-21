using System.Text;
using Wcwidth;

namespace Pyrlyn.OperationIcons;

/// <summary>
/// Icons for what a status-line verb is doing, and the gutter they sit in. Tone-agnostic: a
/// caller with its own fallback icon per line kind (success, warning, …) keeps that table and
/// looks here first.
/// </summary>
public static class VerbIcons
{
    /// <summary>
    /// Columns every icon is padded to. Each icon is still measured rather than assumed, so a
    /// narrower one pads out instead of pulling its line left.
    /// </summary>
    public const int IconWidth = 2;

    /// <summary>
    /// The shared rows, matched as substrings of the lowercased verb in order, so
    /// <c>installing</c>, <c>installed</c> and <c>already installed</c> share one row.
    /// <c>uninstall</c> comes before <c>install</c> because it contains it.
    /// </summary>
    public static IReadOnlyList<VerbIcon> Shared { get; } =
    [
        new("uninstall", "🧹"),
        new("remov", "🧹"),
        new("prun", "🧹"),
        new("install", "📦"),
        new("upgrad", "⏫"),
        new("updat", "⏫"),
        new("download", "⏬"),
        new("fetch", "⏬"),
        new("link", "🔗"),
        new("roll", "⏪"),
        new("search", "🔍"),
        new("doctor", "🩺"),
    ];

    /// <summary>The icon of the first <see cref="Shared"/> row whose key <paramref name="verb"/> contains.</summary>
    /// <param name="verb">The verb, any case, for example <c>Uninstalled</c>.</param>
    /// <returns>The icon, or null so the caller falls back to its line kind's icon.</returns>
    public static string? Icon(string verb) => Icon(Shared, verb);

    /// <summary>
    /// Like <see cref="Icon(string)"/> over a caller's own rows, so a program adds verbs of its own
    /// after the shared ones without the shared table growing for everyone.
    /// </summary>
    /// <param name="rows">The rows, in match order.</param>
    /// <param name="verb">The verb, any case.</param>
    /// <returns>The icon of the first row whose key the verb contains, or null.</returns>
    public static string? Icon(IEnumerable<VerbIcon> rows, string verb)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(verb);
        var lower = verb.ToLowerInvariant();
        foreach (var row in rows)
        {
            if (lower.Contains(row.Key, StringComparison.Ordinal))
            {
                return row.Icon;
            }
        }
        return null;
    }

    /// <summary>
    /// Terminal columns <paramref name="text"/> takes. A narrow symbol followed by U+FE0F
    /// (<c>⚠️</c>) counts as two, as the emoji presentation asks; control characters count as zero.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The width in columns.</returns>
    public static int Width(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var width = 0;
        var previous = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == 0xFE0F && previous == 1)
            {
                width++;
                previous = 2;
                continue;
            }
            previous = Math.Max(0, UnicodeCalculator.GetWidth(rune));
            width += previous;
        }
        return width;
    }

    /// <summary>Blanks that bring <paramref name="icon"/> up to <see cref="IconWidth"/> columns.</summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The number of blanks; zero for an icon already that wide or wider.</returns>
    public static int Padding(string icon) => Math.Max(0, IconWidth - Width(icon));

    /// <summary>
    /// The gutter in front of a verb column: the icon, its padding and one space, or as many
    /// blanks when the line has no icon, so lines with and without one align.
    /// </summary>
    /// <param name="icon">The icon, or null.</param>
    /// <returns>The gutter.</returns>
    public static string Gutter(string? icon)
    {
        icon ??= "";
        return new StringBuilder(icon).Append(' ', Padding(icon) + 1).ToString();
    }
}
