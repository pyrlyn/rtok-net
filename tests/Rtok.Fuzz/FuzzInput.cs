using System.Text;

namespace Rtok.Fuzz;

/// <summary>
/// Turns libFuzzer bytes into the inputs a target needs: text, NUL-separated parts and a
/// selector byte. Invalid UTF-8 becomes U+FFFD, as it would after a lossy read.
/// </summary>
internal static class FuzzInput
{
    public static string Text(ReadOnlySpan<byte> data) => Encoding.UTF8.GetString(data);

    /// <summary>The first byte picks a variant; the rest is the text.</summary>
    public static (byte Pick, string Text) Picked(ReadOnlySpan<byte> data) =>
        data.IsEmpty ? ((byte)0, "") : (data[0], Text(data[1..]));

    /// <summary>The text split at NUL into exactly <paramref name="count"/> parts.</summary>
    public static string[] Parts(ReadOnlySpan<byte> data, int count)
    {
        var parts = Text(data).Split('\0', count);
        if (parts.Length == count)
        {
            return parts;
        }
        var full = new string[count];
        Array.Fill(full, "");
        parts.CopyTo(full, 0);
        return full;
    }

    /// <summary>Fails the run: the code under test broke one of its own invariants.</summary>
    public static void Check(bool condition, string what)
    {
        if (!condition)
        {
            throw new FuzzInvariantException(what);
        }
    }
}
