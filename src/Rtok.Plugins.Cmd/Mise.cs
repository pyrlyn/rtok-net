using System.Text.RegularExpressions;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// <c>mise</c> output (rtok T677): the tool-install and download chatter in front of a task,
/// gone. Everything else (the <c>[task] $ cmd</c> echo, the command's own output,
/// <c>✓ installed</c>, every WARN/ERROR line) is kept byte for byte, so the inner command's
/// formatter still sees what it needs.
/// </summary>
internal static partial class Mise
{
    private static readonly string[] Problems = ["error", "warn", "fail", "panic"];

    /// <summary><paramref name="output"/> without install/progress lines, or null when there
    /// were none or dropping them would not make it shorter. One marker line says how many
    /// went.</summary>
    public static string? Tidy(string output)
    {
        var lines = new List<string>();
        var dropped = 0;
        int? markerAt = null;
        foreach (var raw in Text.Lines(output))
        {
            var line = Visible(raw);
            if (!Problem(line) && (Step().IsMatch(line) || Progress().IsMatch(line)))
            {
                dropped++;
                markerAt ??= lines.Count;
            }
            else
            {
                lines.Add(raw);
            }
        }

        if (dropped == 0)
        {
            return null;
        }

        lines.Insert(markerAt ?? 0, $"mise: {dropped} install/progress lines dropped");
        var got = string.Join('\n', lines);
        return Text.Bytes(got) < Text.Bytes(output) ? got : null;
    }

    private static bool Problem(string line)
    {
        var low = Text.AsciiLower(line);
        return Array.Exists(Problems, w => low.Contains(w, StringComparison.Ordinal));
    }

    // The last `\r` frame of a line (what the terminal shows), without colour codes.
    private static string Visible(string raw) =>
        Families.StripAnsi(raw.Split('\r').LastOrDefault(s => s.Length > 0) ?? "");

    // `mise node@20.1.0 download …`: a step the install prints, not a result.
    [GeneratedRegex(
        @"^mise [\w.:/+-]+@\S+ (download|downloading|extract|extracting|checksum|verify|verifying|install|installing|compile|configure|make)\b",
        RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Step();

    // A progress bar (`[=====>   ]`) or a byte counter (`12.3MiB/45MiB`).
    [GeneratedRegex(
        @"\[[#=>. -]{4,}\]|\d+(\.\d+)? ?[KMGT]i?B ?/ ?\d+(\.\d+)? ?[KMGT]i?B",
        RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Progress();
}
