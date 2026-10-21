using Pyrlyn.Shlex;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// <see cref="ShellWords"/> on any text: <c>TrySplit</c> never throws and agrees with
/// <c>Split</c>, and <c>Join</c> of any words (the input split at NUL) splits back to them.
/// </summary>
internal static class ShellWordsText
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var text = FuzzInput.Text(data);
        if (ShellWords.TrySplit(text, out var words))
        {
            FuzzInput.Check(
                ShellWords.Split(text).SequenceEqual(words, StringComparer.Ordinal),
                "Split and TrySplit differ"
            );
            FuzzInput.Check(
                ShellWords
                    .Split(ShellWords.Join(words))
                    .SequenceEqual(words, StringComparer.Ordinal),
                "Join of split words"
            );
        }
        else
        {
            try
            {
                ShellWords.Split(text);
                FuzzInput.Check(false, "Split accepted what TrySplit refused");
            }
            catch (FormatException) { }
        }
        var raw = text.Split('\0');
        FuzzInput.Check(
            ShellWords.Split(ShellWords.Join(raw)).SequenceEqual(raw, StringComparer.Ordinal),
            "Join of raw words"
        );
    }
}
