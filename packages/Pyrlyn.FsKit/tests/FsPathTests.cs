namespace Pyrlyn.FsKit.Tests;

public sealed class FsPathTests : IDisposable
{
    private static readonly char S = Path.DirectorySeparatorChar;
    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    private static string P(string unix) => unix.Replace('/', S);

    [Fact]
    public void AFileKeepsItsIdAcrossARenameAndANewFileGetsAnother()
    {
        var log = dir.Write("app.log", "a");
        Assert.True(FsPath.TryGetFileId(log, out var first));

        File.Move(log, log + ".1");
        dir.Write("app.log", "b");

        Assert.True(FsPath.TryGetFileId(log + ".1", out var moved));
        Assert.True(FsPath.TryGetFileId(log, out var fresh));
        Assert.Equal(first, moved);
        Assert.NotEqual(first, fresh);
        Assert.False(FsPath.TryGetFileId(dir["missing"], out _));
    }

    [Fact]
    public void VerbatimPrefixIsDroppedWhereThePlainPathMeansTheSame()
    {
        Assert.Equal(@"C:\Users\a\b.txt", FsPath.StripVerbatim(@"\\?\C:\Users\a\b.txt"));
        Assert.Equal(@"\\server\share\x", FsPath.StripVerbatim(@"\\?\UNC\server\share\x"));
        Assert.Equal(@"C:\x", FsPath.StripVerbatim(@"C:\x"));
    }

    [Theory]
    [InlineData(@"\\?\C:\a\..\b")]
    [InlineData(@"\\?\C:\a\.\b")]
    [InlineData(@"\\?\C:\trailing.")]
    [InlineData(@"\\?\C:\trailing ")]
    [InlineData(@"\\?\C:\dir\CON")]
    [InlineData(@"\\?\C:\dir\nul.txt")]
    [InlineData(@"\\?\C:\dir\com1")]
    [InlineData(@"\\?\C:\a/b")]
    [InlineData(@"\\?\C:")]
    [InlineData(@"\\?\Volume{0b1c}\x")]
    public void VerbatimPrefixStaysWhereDroppingItChangesTheMeaning(string path) =>
        Assert.Equal(path, FsPath.StripVerbatim(path));

    [Fact]
    public void ALongVerbatimPathKeepsItsPrefix()
    {
        var path = @"\\?\C:\" + new string('a', 300);
        Assert.Equal(path, FsPath.StripVerbatim(path));
    }

    [Fact]
    public void ComparisonsFoldCaseOnlyWhenAsked()
    {
        var path = "/Repo/Src/main.cs";
        Assert.True(FsPath.StartsWith(path, "/repo/src", fold: true));
        Assert.False(FsPath.StartsWith(path, "/repo/src", fold: false));
        Assert.False(FsPath.StartsWith(path, "", fold: true));
        Assert.False(FsPath.StartsWith("/repo", "/repo/src", fold: true));
        Assert.False(FsPath.StartsWith("/repo-sibling/x", "/repo", fold: false));
        Assert.True(FsPath.StartsWith("/repo/", "/repo", fold: false));
        Assert.True(FsPath.SamePath("/A/b", "/a/B", fold: true));
        Assert.False(FsPath.SamePath("/A/b", "/a/B", fold: false));
        Assert.True(FsPath.SamePath("/a/./b/", "/a/b", fold: false));
        Assert.Equal(P("Src/main.cs"), FsPath.StripPrefix(path, "/repo", fold: true));
        Assert.Null(FsPath.StripPrefix(path, "/repo", fold: false));
        Assert.Null(FsPath.StripPrefix(path, "/other", fold: true));
        Assert.Equal("", FsPath.StripPrefix("/repo", "/repo", fold: false));
    }

    [Fact]
    public void SuffixMatchesWholeComponents()
    {
        var abs = "/Repo/Src/Main.cs";
        Assert.True(FsPath.EndsWith(abs, "src/main.cs", fold: true));
        Assert.False(FsPath.EndsWith(abs, "src/main.cs", fold: false));
        Assert.True(FsPath.EndsWith(abs, "Src/Main.cs", fold: false));
        Assert.False(FsPath.EndsWith(abs, "ain.cs", fold: true));
        Assert.False(FsPath.EndsWith(abs, "", fold: true));
        Assert.False(FsPath.EndsWith("src/main.cs", abs, fold: true));
    }

    [Fact]
    public void NormalizePopsAndDropsWithoutTouchingDisk()
    {
        Assert.Equal(P("/w/repo/src/a.cs"), FsPath.Normalize("/w/repo", "src/./a.cs"));
        Assert.Equal(P("/main/.git"), FsPath.Normalize("/w/repo", "../../main/.git"));
        Assert.Equal(P("/etc/x"), FsPath.Normalize("/w/repo", "/etc/x"));
        Assert.Equal(P("/"), FsPath.Normalize("/w", "../../.."));
    }

    [Fact]
    public void CanonResolvesLinksAndKeepsAMissingPath()
    {
        var real = FsPath.Canonicalize(dir.Root);
        Assert.Equal(real, FsPath.Canon(Path.Combine(dir.Root, ".")));
        var missing = dir["nope"];
        Assert.Equal(missing, FsPath.Canon(missing));
        Assert.Throws<FileNotFoundException>(() => FsPath.Canonicalize(missing));
        Assert.True(FsPath.SameFile(dir.Root, real));
        Assert.False(FsPath.SameFile(missing, dir.Root));
        Assert.True(FsPath.IsUnwalkableRoot(Path.GetPathRoot(dir.Root)!, null));
        Assert.True(FsPath.IsUnwalkableRoot(dir.Root, real));
        Assert.False(FsPath.IsUnwalkableRoot(dir.Root, null));
    }

    [Fact]
    public void CanonicalPathsCarryNoVerbatimPrefix()
    {
        var real = FsPath.Canonicalize(dir.Write("a/b.txt"));
        Assert.False(real.StartsWith(@"\\?\", StringComparison.Ordinal), real);
        Assert.Equal(dir["a/b.txt"], real);
    }

    [Fact]
    public void ContainAcceptsWhatStaysInside()
    {
        var root = dir.Root;
        dir.Write("a/b.txt");
        Assert.Equal(dir["a/b.txt"], FsPath.Contain(root, P("a/b.txt")));
        Assert.Equal(dir["a/b.txt"], FsPath.Contain(root, P("a/../a/b.txt")));
        Assert.Equal(root, FsPath.Contain(root, "."));
        Assert.Equal(root, FsPath.Contain(root, root));
        Assert.Equal(dir["a/new/deeper.txt"], FsPath.Contain(root, dir["a/new/deeper.txt"]));
    }

    [Theory]
    [InlineData("../other/secret")]
    [InlineData("a/../../other/secret")]
    [InlineData("/other/secret")]
    [InlineData("/root-sibling/x")]
    public void ContainRefusesWhatLeaves(string path)
    {
        var root = dir["root"];
        dir.Write("root/a/b.txt");
        dir.Write("other/secret");
        // A leading slash means "beside root", so the absolute cases cannot be anything else.
        var probe = path.StartsWith('/') ? dir.Root + P(path) : P(path);
        var err = Assert.Throws<UnauthorizedAccessException>(() => FsPath.Contain(root, probe));
        Assert.Contains(root, err.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainFollowsSymlinks()
    {
        var root = dir["root"];
        dir.Write("root/real/file.txt");
        dir.Write("other/secret");
        Directory.CreateDirectory(dir["other/inner"]);
        dir.Symlink("root/escape", dir["other"]);
        dir.Symlink("root/alias", dir["root/real"]);
        dir.Symlink("root/hop", dir["other/inner"]);

        Assert.Throws<UnauthorizedAccessException>(() => FsPath.Contain(root, P("escape/secret")));
        // Not yet existing below an escaping link is still an escape.
        Assert.Throws<UnauthorizedAccessException>(() =>
            FsPath.Contain(root, P("escape/new/file"))
        );
        Assert.Equal(dir["root/real/file.txt"], FsPath.Contain(root, P("alias/file.txt")));
        if (!OperatingSystem.IsWindows())
        {
            // POSIX takes `..` after the link, not before it: lexically this is `root/secret`.
            Assert.Throws<UnauthorizedAccessException>(() =>
                FsPath.Contain(root, P("hop/../secret"))
            );
            Assert.Throws<UnauthorizedAccessException>(() =>
                FsPath.Contain(root, P("hop/../made/up"))
            );
        }
    }

    [Fact]
    public void ContainAcceptsALinkThatStaysInside()
    {
        var root = dir["root"];
        dir.Write("root/real/file.txt");
        dir.Symlink("root/alias", "real");
        Assert.Equal(dir["root/real/new.txt"], FsPath.Contain(root, P("alias/new.txt")));
    }
}
