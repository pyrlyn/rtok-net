using System.Text.Json;
using LibGit2Sharp;
using Rtok.Plugin.Sdk;
using Rtok.Plugins.Graph;
using Rtok.Plugins.Graph.Tests;
using Rtok.Store;

namespace Rtok.AotTests;

// The graph plugin on NativeAOT (T24): a Rust and a C# file indexed through the statically
// linked grammars and answered through the plugin's tool call; co-change read through the
// libgit2 that ships beside the binary (N21).
public sealed class GraphTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("rtok-aot-graph-");

    public void Dispose() => _dir.Delete(recursive: true);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_dir.FullName, "repo", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private RtokStore Open() =>
        RtokStore.Open(
            new StoreOptions(Path.Combine(_dir.FullName, "data", "rtok.db"))
            {
                ArchiveDir = Path.Combine(_dir.FullName, "data", "archive"),
            }
        );

    private static string? Call(Ctx cx, string tool, string args)
    {
        using var doc = JsonDocument.Parse(args);
        return new GraphPlugin().CallTool(tool, doc.RootElement, cx);
    }

    [Fact]
    public void The_graph_indexes_and_answers_through_the_linked_grammars()
    {
        Write("chain.rs", "fn a() {\n    b();\n}\nfn b() {\n    c();\n}\nfn c() {}\n");
        Write("other.rs", "fn d() {\n    c();\n    c();\n}\n");
        Write(
            "Shapes.cs",
            "public record Square(double Side);\n"
                + "public static class Shapes\n{\n"
                + "    public static Square Make() => new Square(1);\n}\n"
        );
        using var store = Open();
        var cx = new Ctx(new StoreHost(store) { Cwd = Path.Combine(_dir.FullName, "repo") });

        Assert.Equal(
            "chain.rs  b ×1 (L5)\nother.rs  d ×2 (L2)\n",
            Call(cx, "callers", """{"name":"c"}""")
        );
        Assert.Equal(
            "1  chain.rs  b\n1  other.rs  d\n2  chain.rs  a\n",
            Call(cx, "impact", """{"name":"c","depth":2}""")
        );
        Assert.StartsWith(
            "Shapes.cs::Square#class@1 Shapes.cs:1 class\n",
            Call(cx, "symbol", """{"name":"Square"}"""),
            StringComparison.Ordinal
        );
        Assert.Equal("Shapes.cs  Make ×2 (L4)\n", Call(cx, "callers", """{"name":"Square"}"""));
    }

    [Fact]
    public void Impact_reads_co_change_through_libgit2()
    {
        var root = Path.Combine(_dir.FullName, "repo");
        Directory.CreateDirectory(root);
        Repository.Init(root);
        using (var repo = new Repository(root))
        {
            var who = new Signature("t", "t@t", DateTimeOffset.UnixEpoch);
            foreach (var message in (string[])["one", "two"])
            {
                Write("core.rs", "pub fn core_fn() {}\n// " + message + "\n");
                Write("schema.sql", message + "\n");
                Commands.Stage(repo, "*");
                repo.Commit(message, who, who);
            }
        }

        Write("caller.rs", "fn go() {\n    core_fn();\n}\n");
        using var store = Open();
        var cx = new Ctx(new StoreHost(store) { Cwd = root });

        Assert.EndsWith(
            "changes with: schema.sql (2)",
            Call(cx, "impact", """{"name":"core_fn"}""")!.TrimEnd(),
            StringComparison.Ordinal
        );
    }
}
