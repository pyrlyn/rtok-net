using System.Text.Json;
using Pyrlyn.FsKit;

namespace Rtok.Plugins.Docs.Tests;

public sealed class LocalDocsTests : IDisposable
{
    private readonly DocsHarness _h = new();

    public LocalDocsTests()
    {
        _h.Write(
            "llms.txt",
            """
            # Widget
            Widgets for every workspace.

            - [Guide](docs/guide.md#install): getting started
            - [Reference](<reference/> "all of it")
            - [Outside](../outside.md)
            - [Site](https://example.com/llms-full.txt)
            """
        );
        _h.Write(
            "docs/guide.md",
            "# Install\nRun the installer once.\n\n## Upgrade\nRun it again.\n"
        );
        _h.Write("reference/gears.md", "Gears turn the crank.\n");
        _h.Write("reference/image.png", "crank crank crank");
        _h.Write("../outside.md", "Secret crank outside the workspace.\n");
    }

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Llms_txt_and_its_local_links_are_one_package_scoped_to_the_canonical_workspace()
    {
        Assert.Equal(
            """{"name":"llms","version":"local","cached":true}""",
            _h.Service.Resolve("llms", _h.Workspace)
        );
        Assert.True(_h.Store.DocCrateCached("llms", "local:" + FsPath.Canonicalize(_h.Workspace)));
        Assert.Equal(["docs/guide.md#Install"], Paths(_h.Query("llms", "installer")));
        Assert.Equal(["reference/gears.md"], Paths(_h.Query("llms", "crank")));
        Assert.Equal(["Widget"], Paths(_h.Query("llms", "widgets", "local")));
    }

    [Fact]
    public void Another_workspace_does_not_see_them()
    {
        var other = Path.Join(_h.Workspace, "..", "other");
        Directory.CreateDirectory(other);
        _h.Service.Resolve("llms", _h.Workspace);

        Assert.Equal(
            "not cached: rtok docs fetch llms",
            _h.Service.Query("llms", null, "installer", other, _h.Cx)
        );
    }

    [Fact]
    public void Unchanged_files_keep_their_ids_and_an_edit_is_searched_at_once()
    {
        var first = Ids(_h.Query("llms", "installer"));
        Assert.Equal(first, Ids(_h.Query("llms", "installer")));

        _h.Write("docs/guide.md", "# Install\nUse the package manager.\n");

        Assert.Equal("[]", _h.Query("llms", "installer"));
        Assert.Single(Ids(_h.Query("llms", "manager")));
    }

    [Fact]
    public void Removing_llms_txt_drops_the_package()
    {
        _h.Service.Resolve("llms", _h.Workspace);
        File.Delete(Path.Join(_h.Workspace, "llms.txt"));

        Assert.Equal(
            """{"name":"llms","version":"local","cached":false}""",
            _h.Service.Resolve("llms", _h.Workspace)
        );
    }

    [Fact]
    public void Llms_txt_splits_on_heading_lines()
    {
        var items = LlmsText.Split("lead\n# One\nbody a\n## Two\r\nbody b\n#\nbody c\n");

        Assert.Equal(
            [("llms", "lead"), ("One", "body a"), ("Two", "body b"), ("llms", "body c")],
            items.Select(i => (i.Path, i.Docs))
        );
    }

    internal static string[] Paths(string reply) =>
        [
            .. JsonDocument
                .Parse(reply)
                .RootElement.EnumerateArray()
                .Select(h => h.GetProperty("path").GetString()!),
        ];

    internal static long[] Ids(string reply) =>
        [
            .. JsonDocument
                .Parse(reply)
                .RootElement.EnumerateArray()
                .Select(h => h.GetProperty("id").GetInt64()),
        ];
}
