using Pyrlyn.Ignore;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// gitignore rules and paths (NUL-separated, one per line) on <see cref="IgnoreFile"/> and
/// <see cref="IgnorePattern"/>: nothing throws, a path matches the same absolute or relative,
/// and a pattern written back by <c>ToString</c> matches the same.
/// </summary>
internal static class IgnoreRules
{
    private const string Base = "/repo";

    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, rest) = FuzzInput.Picked(data);
        var ignoreCase = (pick & 1) != 0;
        var parts = rest.Split('\0', 2);
        var rules = parts[0];
        string[] paths = [.. (parts.Length > 1 ? parts[1] : "a/b\nx").Split('\n').Take(16)];
        var file = IgnoreFile.Parse(Base, rules, ignoreCase);
        var patterns = rules
            .Split('\n')
            .Take(64)
            .Select(line => IgnorePattern.Parse(line, ignoreCase))
            .OfType<IgnorePattern>()
            .Select(pattern =>
                (pattern, again: IgnorePattern.Parse(pattern.ToString(), ignoreCase))
            )
            .ToList();
        foreach (var (pattern, again) in patterns)
        {
            FuzzInput.Check(again is not null, $"pattern {pattern} written back does not parse");
        }
        foreach (var path in paths)
        {
            var relative = path.TrimStart('/');
            if (relative.Length == 0)
            {
                continue;
            }
            foreach (var isDirectory in (bool[])[false, true])
            {
                FuzzInput.Check(
                    file.Match($"{Base}/{relative}", isDirectory)
                        == file.Match(relative, isDirectory),
                    $"{relative} matches differently absolute and relative"
                );
                foreach (var (pattern, again) in patterns)
                {
                    FuzzInput.Check(
                        again!.IsMatch(relative, isDirectory)
                            == pattern.IsMatch(relative, isDirectory),
                        $"pattern {pattern} written back matches {relative} differently"
                    );
                }
            }
        }
    }
}
