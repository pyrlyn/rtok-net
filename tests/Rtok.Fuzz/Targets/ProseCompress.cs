using System.Text;
using Rtok.Plugins.Inject;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any Markdown through <see cref="ProseCompressor"/> at the intensity the first byte picks:
/// the output is the same on a second run, never longer in characters or UTF-8 bytes, and holds
/// every fenced block of the input, in order and byte for byte.
/// </summary>
internal static class ProseCompress
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        var intensity = (ProseIntensity)(pick % 3);
        var output = ProseCompressor.Compress(text, intensity);
        FuzzInput.Check(
            string.Equals(
                output,
                ProseCompressor.Compress(text, intensity),
                StringComparison.Ordinal
            ),
            "two runs differ"
        );
        FuzzInput.Check(
            output.Length <= text.Length
                && Encoding.UTF8.GetByteCount(output) <= Encoding.UTF8.GetByteCount(text),
            "the output grew"
        );
        var from = 0;
        var rest = text;
        while (ProseCompressor.FindFence(rest) is (var start, var end))
        {
            var block = rest[start..end];
            var at = output.IndexOf(block, from, StringComparison.Ordinal);
            FuzzInput.Check(at >= 0, $"a fenced block was changed: {block}");
            from = at + block.Length;
            rest = rest[end..];
        }
    }
}
