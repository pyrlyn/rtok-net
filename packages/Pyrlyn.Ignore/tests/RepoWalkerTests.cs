namespace Pyrlyn.Ignore.Tests;

public sealed class RepoWalkerTests : IDisposable
{
    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    /// <summary>A repository without running git: a <c>.git</c> directory is all the walker looks for.</summary>
    private void FakeRepo(string relative = "") =>
        Directory.CreateDirectory(
            dir[relative.Length == 0 ? ".git/info" : relative + "/.git/info"]
        );

    [Fact]
    public void TheFilesystemRootAndHomeAreRefused()
    {
        Assert.Throws<ArgumentException>(() =>
            new RepoWalker(Path.GetPathRoot(dir.Root)!, dir.Options)
        );
        Assert.Throws<ArgumentException>(() => new RepoWalker(dir.Home, dir.Options));
        Assert.Throws<DirectoryNotFoundException>(() =>
            new RepoWalker(dir["missing"], dir.Options)
        );
    }

    [Fact]
    public void EntriesComeDepthFirstInNameOrderWithoutGit()
    {
        FakeRepo();
        dir.Files("b/x", "a/z", "a/y/1", "c", ".git/HEAD");
        Assert.Equal(
            ["a", "a/y", "a/y/1", "a/z", "b", "b/x", "c"],
            new RepoWalker(dir.Root, dir.Options).Walk().Select(e => e.RelativePath).ToArray()
        );
    }

    [Fact]
    public void MaxDepthStopsBelowIt()
    {
        dir.Files("a/b/c", "d");
        var walked = new RepoWalker(dir.Root, dir.Options with { MaxDepth = 1 }).Walk().ToList();
        Assert.Equal(["a", "d"], walked.Select(e => e.RelativePath).ToArray());
        Assert.All(walked, e => Assert.Equal(1, e.Depth));
    }

    [Fact]
    public void GitignoreNeedsARepositoryUnlessToldOtherwise()
    {
        dir.Write(".gitignore", "*.log\n");
        dir.Files("a.log", "b.txt");
        Assert.Equal([".gitignore", "a.log", "b.txt"], dir.Walked());
        Assert.Equal(
            [".gitignore", "b.txt"],
            dir.Walked(options: dir.Options with { RequireGit = false })
        );
    }

    [Fact]
    public void DotIgnoreAppliesEverywhereAndBeatsGitignore()
    {
        FakeRepo();
        dir.Write(".gitignore", "*.md\n");
        dir.Write(".ignore", "!readme.md\n*.gen\n");
        dir.Files("readme.md", "other.md", "x.gen", "plain/y.gen");
        Assert.Equal([".gitignore", ".ignore", "readme.md"], dir.Walked());
        Assert.Equal(
            [".gitignore", ".ignore", "other.md", "plain/y.gen", "readme.md", "x.gen"],
            dir.Walked(options: dir.Options with { DotIgnore = false, GitIgnore = false })
        );
    }

    [Fact]
    public void ANestedRepositoryStartsANewGitScope()
    {
        FakeRepo();
        dir.Write(".gitignore", "*.txt\n");
        dir.Write(".ignore", "*.tmp\n");
        FakeRepo("nested");
        dir.Files("a.txt", "nested/b.txt", "nested/c.tmp");
        Assert.Equal([".gitignore", ".ignore", "nested/b.txt"], dir.Walked());
    }

    [Fact]
    public void LinkedWorktreesAreSkippedSubmodulesAreNot()
    {
        FakeRepo();
        Directory.CreateDirectory(dir[".git/worktrees/wt"]);
        File.WriteAllText(dir[".git/worktrees/wt/commondir"], "../..\n");
        dir.Write(".worktrees/wt/.git", $"gitdir: {dir[".git/worktrees/wt"]}\n");
        dir.Files(".worktrees/wt/file");
        Directory.CreateDirectory(dir[".git/modules/sub"]);
        dir.Write("sub/.git", "gitdir: ../.git/modules/sub\n");
        dir.Files("sub/file");
        Assert.Equal(["sub/file"], dir.Walked());
        // The root itself is walked, worktree or not.
        Assert.Equal(["file"], dir.Walked(".worktrees/wt"));
    }

    [Fact]
    public void ARealLinkedWorktreeIsSkipped()
    {
        dir.Init();
        dir.Git(
            dir.Root,
            "-c",
            "user.name=t",
            "-c",
            "user.email=t@t",
            "commit",
            "-q",
            "--allow-empty",
            "-m",
            "x"
        );
        dir.Git(dir.Root, "worktree", "add", "-q", dir[".rtok/worktrees/x"]);
        dir.Files("kept.txt", ".rtok/worktrees/x/inside.txt");
        Assert.Equal(["kept.txt"], dir.Walked());
    }

    [Fact]
    public void HiddenEntriesCanBeSkipped()
    {
        dir.Files(".env", ".dir/x", "y");
        Assert.Equal(["y"], dir.Walked(options: dir.Options with { Hidden = false }));
    }

    [Fact]
    public void IncludeBringsBackWhatTheIgnoreFilesSkipAndExcludeBeatsIt()
    {
        FakeRepo();
        dir.Write(".gitignore", "vendor/\n*.gen.cs\n");
        dir.Files(
            "vendor/lib/a.c",
            "vendor/lib/b.h",
            "vendor/lib/drop/c.c",
            "x.gen.cs",
            "src/y.gen.cs",
            "src/z.cs"
        );
        var options = dir.Options with
        {
            Include = ["vendor/lib/*.c", "vendor/lib/drop/", "*.gen.cs"],
            Exclude = ["drop/", "src/y.gen.cs"],
        };
        Assert.Equal(
            [".gitignore", "src/z.cs", "vendor/lib/a.c", "x.gen.cs"],
            dir.Walked(options: options)
        );
    }

    [Fact]
    public void ExcludeAloneUsesGitignoreSyntax()
    {
        dir.Files("a/b.txt", "a/c.md", "d.txt");
        Assert.Equal(
            ["a/c.md"],
            dir.Walked(options: dir.Options with { Exclude = ["*.txt", "!a/c.md"] })
        );
    }

    [Fact]
    public void IsWalkedAgreesWithWalk()
    {
        FakeRepo();
        dir.Write(".gitignore", "*.log\nbuild/\n!sub/keep.log\n");
        dir.Write("sub/.gitignore", "!*.log\n");
        FakeRepo("nested");
        dir.Files(
            "a.log",
            "b.txt",
            "build/x",
            "sub/k.log",
            "sub/keep.log",
            "nested/n.log",
            "gen/g.cs",
            "gen/h.txt"
        );
        var options = dir.Options with { Include = ["build/x"], Exclude = ["gen/*.txt"] };
        var walker = new RepoWalker(dir.Root, options);
        var walked = walker
            .Walk()
            .Where(e => !e.IsDirectory)
            .Select(e => e.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var all = Directory
            .EnumerateFiles(dir.Root, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(dir.Root, p).Replace('\\', '/'));
        foreach (var file in all)
        {
            Assert.True(walked.Contains(file) == walker.IsWalked(file, false), file);
        }
        Assert.False(walker.IsWalked(dir.Top, true));
        Assert.True(walker.IsWalked("sub", true));
        Assert.False(walker.IsWalked("build", true));
    }
}
