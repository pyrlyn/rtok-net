using System.Text;

namespace Pyrlyn.TreeSitter.Tests;

public sealed class ParserTests
{
    [Fact]
    public void ParseGivesATreeWithKindsPositionsAndFields()
    {
        using var parser = new Parser(Language.Rust);
        var source = "fn main() {\n    let x = 1;\n}\n"u8.ToArray();
        using var tree = parser.Parse(source)!;

        var root = tree.Root;
        var function = root.NamedChild(0);
        var name = function.ChildByFieldName("name");

        Assert.Equal("source_file", root.Kind);
        Assert.False(root.HasError);
        Assert.Equal("function_item", function.Kind);
        Assert.Equal("main", Encoding.UTF8.GetString(name.Text(source)));
        Assert.Equal(new Point(0, 3), name.StartPoint);
        Assert.Equal(new Point(2, 1), function.EndPoint);
        Assert.Equal(function, name.Parent);
        Assert.True(function.ChildByFieldName("nope").IsNull);
    }

    [Fact]
    public void ChildrenAndDescendantsWalkTheTreeInOrder()
    {
        using var parser = new Parser(Language.JavaScript);
        using var tree = parser.Parse("a(b);")!;

        var kinds = tree.Root.Descendants.Where(n => n.IsNamed).Select(n => n.Kind);
        var children = tree.Root.Child(0).Children.Select(n => n.Kind);

        Assert.Equal(
            [
                "program",
                "expression_statement",
                "call_expression",
                "identifier",
                "arguments",
                "identifier",
            ],
            kinds,
            StringComparer.Ordinal
        );
        Assert.Equal(["call_expression", ";"], children, StringComparer.Ordinal);
    }

    [Fact]
    public void DescendantsStopAtTheStartNode()
    {
        using var parser = new Parser(Language.JavaScript);
        using var tree = parser.Parse("f(x); g(y);")!;

        var first = tree.Root.NamedChild(0);

        Assert.DoesNotContain(first.Descendants, n => n.StartByte >= first.EndByte);
    }

    [Fact]
    public void BrokenSourceParsesWithErrors()
    {
        using var parser = new Parser(Language.Python);
        using var tree = parser.Parse("def (:\n")!;

        Assert.True(tree.Root.HasError);
    }

    [Fact]
    public void AParserWithoutALanguageReturnsNoTree()
    {
        using var parser = new Parser();

        Assert.Null(parser.Parse("x"));
    }

    [Fact]
    public void NodesOfADisposedTreeThrow()
    {
        using var parser = new Parser(Language.Go);
        var tree = parser.Parse("package main\n")!;
        var root = tree.Root;
        tree.Dispose();

        Assert.Throws<ObjectDisposedException>(() => root.Kind);
    }

    [Fact]
    public void EveryLanguageLoads()
    {
        Assert.All(
            Grammar.All,
            g =>
            {
                using var parser = new Parser(g.Language);
                Assert.InRange(g.Language.AbiVersion, 13u, 15u);
                Assert.NotNull(parser.Parse(""));
            }
        );
    }
}
