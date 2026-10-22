using System.Text.RegularExpressions;
using Rtok.Plugins.Cmd;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any text through the family formatter arm the first byte picks (its top bit marks a failed
/// run): the text is never longer than the input, keeps every WARN/ERROR line and, after a
/// failed run, every stderr verdict; a <c>git log</c> listing names every commit's short hash.
/// The pre-passes (mise, loops) run too and keep every WARN/ERROR line.
/// </summary>
internal static partial class CmdFormatters
{
    private static readonly string[] Verdicts = ["error:", "error[", "fatal:", "Error:"];

    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        var (bin, sub) = FamilyFormatters.Arms[(pick & 0x7f) % FamilyFormatters.Arms.Length];
        var exit = (pick & 0x80) == 0 ? 0 : 1;
        string[] argv = bin switch
        {
            "ls" => ["ls", "-l"],
            "grep" => ["grep", "-rn", "x", "."],
            "rg" => ["rg", "-n", "x"],
            _ => [bin, sub is "*" ? "x" : sub],
        };

        var input = Text.Lines(text);
        if (FamilyFormatters.Tidy(bin, text) is { } tidy)
        {
            KeepsProblems(input, tidy, "the pre-pass");
        }

        if (FamilyFormatters.Format(argv, text, exit) is not { } got)
        {
            return;
        }

        FuzzInput.Check(Text.Bytes(got) <= Text.Bytes(text), $"{bin} {sub} grew the text");
        KeepsProblems(input, got, $"{bin} {sub}");
        if (exit != 0)
        {
            var kept = Text.Lines(got).ToHashSet(StringComparer.Ordinal);
            FuzzInput.Check(
                input.TrueForAll(l =>
                    !Array.Exists(Verdicts, v => l.StartsWith(v, StringComparison.Ordinal))
                    || kept.Contains(l)
                ),
                $"{bin} {sub} dropped a stderr verdict"
            );
        }

        // GitLog's listing (not the head cut of another shape) names every commit up to its cap.
        if (bin is "git" && sub is "log" && got.StartsWith('['))
        {
            var hashes = Commit().Matches(text).Select(m => m.Groups["hash"].Value).ToList();
            FuzzInput.Check(
                hashes
                    .Take(FamilyFormatters.GitLogMaxCommits)
                    .All(h => got.Contains(h[..7], StringComparison.Ordinal)),
                "git log dropped a commit hash"
            );
        }
    }

    private static void KeepsProblems(List<string> input, string got, string who)
    {
        var kept = Text.Lines(got).ToHashSet(StringComparer.Ordinal);
        FuzzInput.Check(
            input.TrueForAll(l =>
                !(
                    l.Contains("ERROR", StringComparison.Ordinal)
                    || l.Contains("WARN", StringComparison.Ordinal)
                ) || kept.Contains(l)
            ),
            $"{who} dropped a WARN/ERROR line"
        );
    }

    [GeneratedRegex(
        @"^commit (?<hash>[0-9a-f]{40})\b",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Commit();
}
