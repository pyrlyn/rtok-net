using Rtok.Config;
using Rtok.Plugins.Read;
using Rtok.Testing;

namespace Rtok.Plugins.Graph.Tests;

// rtok graph/walk.rs: include, exclude, extensions and nested worktrees.
public sealed class MatcherTests
{
    private static Matcher Matcher(
        string dir,
        string[]? exclude = null,
        string[]? include = null,
        Dictionary<string, string>? extensions = null
    ) =>
        new(
            dir,
            new GraphSection
            {
                Exclude = exclude ?? [],
                Include = include ?? [],
                Extensions = extensions ?? new Dictionary<string, string>(StringComparer.Ordinal),
            }
        );

    private static List<string> Indexed(Matcher m) =>
        m.Walk()
            .Where(e => !e.IsDirectory && m.Indexable(e.Path))
            .Select(e => e.RelativePath.Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

    // A repository whose .gitignore covers wanted/, unwanted/ and vendor/.
    private static TempDir IgnoredTree()
    {
        var dir = new TempDir("rtok-graph-walk-");
        Directory.CreateDirectory(dir.Combine(".git"));
        dir.Write(".gitignore", "wanted/\nunwanted/\nvendor/\n");
        foreach (
            var path in (string[])
                [
                    "wanted/a.rs",
                    "wanted/sub/b.rs",
                    "unwanted/a.rs",
                    "vendor/pkg/lib.rs",
                    "vendor/other/lib.rs",
                    "src/lib.rs",
                    "src/generated/g.rs",
                ]
        )
        {
            dir.Write(path, "fn f() {}\n");
        }

        return dir;
    }

    [Fact]
    public void Exclude_drops_a_subtree_and_include_brings_gitignored_back()
    {
        using var dir = new TempDir("rtok-graph-walk-");
        dir.Write("skip/a.rs", "fn a() {}\n");
        dir.Write("keep.rs", "fn keep() {}\n");
        dir.Write(".gitignored/hidden.rs", "fn hidden() {}\n");
        dir.Write(".gitignore", ".gitignored/\n");
        var m = Matcher(dir.Path, exclude: ["skip/"], include: [".gitignored/"]);
        var root = m.Root;

        Assert.False(m.Indexable(Path.Join(root, "skip/a.rs")));
        Assert.True(m.Indexable(Path.Join(root, "keep.rs")));
        Assert.True(m.Indexable(Path.Join(root, ".gitignored/hidden.rs")));
        Assert.Equal([".gitignored/hidden.rs", "keep.rs"], Indexed(m));
    }

    [Theory]
    [InlineData("", "src/generated/g.rs,src/lib.rs")]
    [InlineData("wanted/", "src/generated/g.rs,src/lib.rs,wanted/a.rs,wanted/sub/b.rs")]
    [InlineData("vendor/pkg/", "src/generated/g.rs,src/lib.rs,vendor/pkg/lib.rs")]
    [InlineData("wanted/**/b.rs", "src/generated/g.rs,src/lib.rs,wanted/sub/b.rs")]
    public void An_include_does_not_revive_unrelated_ignored_files(string include, string want)
    {
        using var dir = IgnoredTree();
        var m = Matcher(dir.Path, include: include.Length == 0 ? [] : [include]);
        Assert.Equal(want.Split(','), Indexed(m), StringComparer.Ordinal);
    }

    [Fact]
    public void Exclude_wins_over_include_and_understands_globs()
    {
        using var dir = IgnoredTree();
        var m = Matcher(
            dir.Path,
            exclude: ["**/generated/**", "wanted/sub/"],
            include: ["wanted/"]
        );
        Assert.Equal(["src/lib.rs", "wanted/a.rs"], Indexed(m));
    }

    // A linked worktree nested in the project is another checkout's files; a submodule and a
    // nested clone stay, and the worktree itself, as a root, is walked.
    [Fact]
    public void A_nested_linked_worktree_is_not_walked_into()
    {
        using var dir = IgnoredTree();
        var nested = dir.Combine(".rtok", "worktrees", "x");
        var admin = dir.Combine(".git", "worktrees", "x");
        Directory.CreateDirectory(admin);
        File.WriteAllText(Path.Join(admin, "commondir"), "../..\n");
        dir.Write(".rtok/worktrees/x/.git", $"gitdir: {admin}\n");
        Directory.CreateDirectory(dir.Combine(".git", "modules", "sub"));
        dir.Write("mods/sub/.git", "gitdir: ../../.git/modules/sub\n");
        Directory.CreateDirectory(dir.Combine("deps", "clone", ".git"));
        dir.Write(".rtok/worktrees/x/n.rs", "fn f() {}\n");
        dir.Write("mods/sub/s.rs", "fn f() {}\n");
        dir.Write("deps/clone/c.rs", "fn f() {}\n");
        dir.Write(".rtok/notes.rs", "fn notes() {}\n");

        Assert.Equal(
            [
                ".rtok/notes.rs",
                "deps/clone/c.rs",
                "mods/sub/s.rs",
                "src/generated/g.rs",
                "src/lib.rs",
            ],
            Indexed(Matcher(dir.Path))
        );
        Assert.Equal(["n.rs"], Indexed(Matcher(nested)));
    }

    [Fact]
    public void An_extension_map_indexes_a_custom_suffix()
    {
        using var dir = new TempDir("rtok-graph-ext-");
        dir.Write("tpl.rs", "fn rs() {}\n");
        dir.Write("weird.tpl", "fn tpl() {}\n");
        var m = Matcher(
            dir.Path,
            extensions: new Dictionary<string, string>(StringComparer.Ordinal) { ["tpl"] = "rust" }
        );

        Assert.False(Outline.Supported("x.tpl"));
        Assert.True(m.Indexable(Path.Join(m.Root, "weird.tpl")));
        Assert.True(m.UsesExtensionMap(Path.Join(m.Root, "weird.tpl")));
        Assert.False(m.UsesExtensionMap(Path.Join(m.Root, "tpl.rs")));
    }

    [Fact]
    public void A_path_outside_the_root_is_not_indexable()
    {
        using var dir = new TempDir("rtok-graph-out-");
        using var other = new TempDir("rtok-graph-other-");
        var file = other.Write("a.rs", "fn a() {}\n");
        Assert.False(Matcher(dir.Path).Indexable(file));
    }
}
