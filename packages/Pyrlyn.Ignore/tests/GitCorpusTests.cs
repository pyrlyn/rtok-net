namespace Pyrlyn.Ignore.Tests;

/// <summary>The walker against <c>git ls-files --others --cached --exclude-standard</c> on fixture repos: no differences.</summary>
public sealed class GitCorpusTests : IDisposable
{
    private const string RootRules = """
        # comment
        *.log
        !keep.log
        build/
        !build/keep.txt
        /root-only.txt
        doc/*.tmp
        **/cache
        a/**/z.txt
        \#hash.txt
        \!bang.txt
        foo?.c
        [ab]x.md
        *.[!o]bj
        onlydir/
        logs/**
        !logs/important/
        """;

    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    private void Same(string relative = "") =>
        Assert.Equal(dir.Listed(relative), dir.Walked(relative));

    [Fact]
    public void RootGitignoreRules()
    {
        dir.Init();
        // Trailing spaces are dropped, an escaped one is kept.
        dir.Write(".gitignore", RootRules + "\ntrailing.txt   \nspaced\\ \n");
        if (!OperatingSystem.IsWindows())
        {
            dir.Files("spaced ", "spaced");
        }
        dir.Files(
            "a.log",
            "keep.log",
            "sub/b.log",
            "sub/keep.log",
            "build/out.bin",
            "build/keep.txt",
            "sub/build/x",
            "src/build",
            "root-only.txt",
            "sub/root-only.txt",
            "doc/x.tmp",
            "doc/sub/y.tmp",
            "x/cache/f",
            "cache/g",
            "cachet",
            "a/z.txt",
            "a/b/z.txt",
            "a/b/c/z.txt",
            "b/z.txt",
            "trailing.txt",
            "#hash.txt",
            "!bang.txt",
            "foo1.c",
            "foo12.c",
            "ax.md",
            "bx.md",
            "cx.md",
            "a.obj",
            "a.pbj",
            "onlydir",
            "x/onlydir/f",
            "logs/a.txt",
            "logs/important/i.txt",
            ".hidden/file",
            ".env",
            "empty/"
        );
        Same();
    }

    [Fact]
    public void NestedGitignoresAndPrecedence()
    {
        dir.Init();
        dir.Write(".gitignore", "*.log\n*.txt\n/top/\n");
        dir.Write("sub/.gitignore", "!b.log\n!*.txt\nlocal/\n");
        dir.Write("sub/deeper/.gitignore", "*.txt\r\n!keep.txt\r\n");
        dir.Write("sub/deeper/more/.gitignore", "﻿/anchored.md\n");
        dir.Files(
            "a.log",
            "a.txt",
            "top/x",
            "sub/top/x",
            "sub/b.log",
            "sub/c.log",
            "sub/n.txt",
            "sub/local/f",
            "sub/x/local/f",
            "sub/deeper/n.txt",
            "sub/deeper/keep.txt",
            "sub/deeper/more/anchored.md",
            "sub/deeper/more/m/anchored.md"
        );
        Same();
    }

    [Fact]
    public void InfoExcludeAndTheDefaultGlobalFile()
    {
        dir.Init();
        File.AppendAllText(Path.Combine(dir.Root, ".git", "info", "exclude"), "by-info.txt\n");
        Directory.CreateDirectory(Path.Combine(dir.Home, ".config", "git"));
        File.WriteAllText(
            Path.Combine(dir.Home, ".config", "git", "ignore"),
            "*.global\n!keep.global\n"
        );
        dir.Write(".gitignore", "!by-info-kept.txt\n");
        dir.Files("by-info.txt", "sub/by-info.txt", "a.global", "keep.global", "x.txt");
        Same();
    }

    [Fact]
    public void ExcludesFileFromTheGlobalConfig()
    {
        dir.Init();
        var custom = Path.Combine(dir.Home, "my-excludes");
        File.WriteAllText(custom, "*.custom\n");
        File.WriteAllText(
            Path.Combine(dir.Home, ".gitconfig"),
            "[core]\n\texcludesFile = ~/my-excludes\n"
        );
        dir.Files("a.custom", "b.txt");
        Same();
    }

    [Fact]
    public void ExcludesFileFromTheRepoConfigWins()
    {
        dir.Init();
        File.WriteAllText(Path.Combine(dir.Home, "global"), "*.a\n");
        File.WriteAllText(Path.Combine(dir.Home, "local"), "*.b\n");
        File.WriteAllText(
            Path.Combine(dir.Home, ".gitconfig"),
            $"[core]\nexcludesfile = {Path.Combine(dir.Home, "global")}\n"
        );
        dir.Git(dir.Root, "config", "core.excludesFile", Path.Combine(dir.Home, "local"));
        dir.Files("x.a", "x.b", "x.c");
        Same();
    }

    [Fact]
    public void AWalkBelowTheRootReadsTheParentsIgnoreFiles()
    {
        dir.Init();
        dir.Write(".gitignore", "*.log\nsub/inner/gone/\n");
        dir.Write("sub/.gitignore", "*.tmp\n");
        File.AppendAllText(Path.Combine(dir.Root, ".git", "info", "exclude"), "*.info\n");
        dir.Files(
            "sub/inner/a.log",
            "sub/inner/b.tmp",
            "sub/inner/c.info",
            "sub/inner/d.txt",
            "sub/inner/gone/x"
        );
        Same("sub/inner");
    }

    [Fact]
    public void SymlinksAreListedNotFollowed()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "symlinks need Developer Mode on Windows");
        dir.Init();
        dir.Write(".gitignore", "ignored-link\n");
        dir.Files("target/a.txt");
        File.CreateSymbolicLink(dir["dir-link"], dir["target"]);
        File.CreateSymbolicLink(dir["file-link"], dir["target/a.txt"]);
        File.CreateSymbolicLink(dir["ignored-link"], dir["target"]);
        Same();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void RandomRulesAndTrees(int seed)
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
