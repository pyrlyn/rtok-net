namespace Pyrlyn.Ignore.Tests;

/// <summary>
/// Seeded random rules and trees against <c>git ls-files</c>. Each case spawns git, so the
/// seeds are split over two classes: xUnit runs a class's tests in sequence but runs classes
/// (test collections) in parallel.
/// </summary>
public abstract class GitCorpusRandomTests : IDisposable
{
    private readonly Scratch dir = new();

    public void Dispose()
    {
        dir.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Same(string relative = "") =>
        Assert.Equal(dir.Listed(relative), dir.Walked(relative));

    protected void RandomRulesAndTrees(int seed)
    {
        // Seeded, so a failure names its case; each case is a fresh repo with nested ignore files.
        var random = new Random(seed);
        string[] names = ["a", "b", "ab", "x.log", "d.md", "c"];
        string[] globs = ["*", "?", "**", "[ab]", "[!a]", "a*", "*.log", "*b", "d.*"];
        string Part() =>
            random.Next(3) == 0
                ? globs[random.Next(globs.Length)]
                : names[random.Next(names.Length)];
        string Rule()
        {
            var parts = Enumerable.Range(0, random.Next(1, 4)).Select(_ => Part());
            var rule = string.Join('/', parts);
            if (random.Next(4) == 0)
            {
                rule = "/" + rule;
            }
            if (random.Next(4) == 0)
            {
                rule += "/";
            }
            return random.Next(4) == 0 ? "!" + rule : rule;
        }
        dir.Init();
        var files = RandomFiles(random, names);
        foreach (var f in files)
        {
            dir.Write(f, f);
        }
        var dirs = files
            .SelectMany(f =>
                Enumerable
                    .Range(1, f.Count(c => c == '/'))
                    .Select(k => string.Join('/', f.Split('/').Take(k)))
            )
            .Prepend("")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var d in dirs.Where(_ => random.Next(2) == 0))
        {
            var rules = string.Join(
                '\n',
                Enumerable.Range(0, random.Next(1, 5)).Select(_ => Rule())
            );
            dir.Write(d.Length == 0 ? ".gitignore" : d + "/.gitignore", rules + "\n");
        }
        Same();
    }

    private static SortedSet<string> RandomFiles(Random random, string[] names)
    {
        var files = new SortedSet<string>(StringComparer.Ordinal);
        for (var n = 0; n < 40; n++)
        {
            var path = string.Join(
                '/',
                Enumerable.Range(0, random.Next(1, 5)).Select(_ => names[random.Next(names.Length)])
            );
            // A path cannot be a file and a directory at once.
            if (
                !files.Any(f =>
                    path.StartsWith(f + "/", StringComparison.Ordinal)
                    || f.StartsWith(path + "/", StringComparison.Ordinal)
                )
            )
            {
                files.Add(path);
            }
        }
        return files;
    }
}
