namespace Rtok.Core;

/// <summary>
/// The built-in plugins in dispatch order, each with whether it is on by default. This is the
/// one list; a linked built-in plugin's manifest has to agree with it.
/// </summary>
public static class Catalogue
{
    /// <summary>Every built-in plugin id and its default, in dispatch order.</summary>
    public static IReadOnlyList<(string Id, bool DefaultOn)> Entries { get; } =
    [
        ("measure", true),
        ("cmd", true),
        ("read", true),
        ("json_tree", false),
        ("archive", true),
        ("proxy", true),
        ("inject", true),
        ("guard", true),
        ("memory", true),
        ("graph", true),
        ("toon", true),
        ("compress", true),
        ("docs", false),
    ];

    /// <summary>The position of <paramref name="id"/> in <see cref="Entries"/>, or -1 for a
    /// plugin built outside the repository.</summary>
    public static int IndexOf(string id)
    {
        for (var i = 0; i < Entries.Count; i++)
        {
            if (string.Equals(Entries[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
