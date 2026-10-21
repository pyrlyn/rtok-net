using System.Text;

namespace Pyrlyn.TreeSitter.Tests;

public sealed class QueryTests
{
    private const string Source = "foo(1); bar(2); foo(bar); baz(baz);";

    [Theory]
    [InlineData("""((identifier) @f (#eq? @f "foo"))""", "foo,foo")]
    [InlineData("""((identifier) @f (#not-eq? @f "foo"))""", "bar,bar,baz,baz")]
    [InlineData("""((identifier) @f (#match? @f "^ba"))""", "bar,bar,baz,baz")]
    [InlineData("""((identifier) @f (#not-match? @f "^ba"))""", "foo,foo")]
    [InlineData("""((identifier) @f (#any-of? @f "foo" "baz"))""", "foo,foo,baz,baz")]
    [InlineData("""((identifier) @f (#not-any-of? @f "foo" "baz"))""", "bar,bar")]
    public void TextPredicatesFilterMatches(string query, string expected)
    {
        Assert.Equal(expected, Names(query));
    }

    [Fact]
    public void EqBetweenCapturesComparesTheirText()
    {
        const string query = """
            (call_expression
              function: (identifier) @f
              arguments: (arguments (identifier) @a)
              (#eq? @f @a))
            """;

        Assert.Equal("baz", Names(query));
    }

    [Fact]
    public void PlainFormsNeedEveryNodeAndAnyFormsNeedOne()
    {
        // Both quantified captures hold `a` and `b`.
        const string all = """((program (expression_statement (identifier) @x)+) (#eq? @x "a"))""";
        const string any =
            """((program (expression_statement (identifier) @x)+) (#any-eq? @x "a"))""";

        Assert.Equal(0, MatchCount(all, "a; b;"));
        Assert.Equal(1, MatchCount(any, "a; b;"));
        Assert.Equal(1, MatchCount(all, "a; a;"));
    }

    [Fact]
    public void PropertyAndGeneralPredicatesAreExposedPerPattern()
    {
        using var query = new Query(
            Language.JavaScript,
            """
            ((identifier) @x (#set! kind "name") (#is-not? local) (#strip! @x "^_"))
            """
        );

        var setting = Assert.Single(query.PropertySettings(0));
        var (property, isPositive) = Assert.Single(query.PropertyPredicates(0));
        var general = Assert.Single(query.GeneralPredicates(0));

        Assert.Equal(new QueryProperty("kind", "name", null), setting);
        Assert.Equal("local", property.Key);
        Assert.False(isPositive);
        Assert.Equal("strip!", general.Operator);
        Assert.Equal(
            [new QueryPredicateArg(0, null), new QueryPredicateArg(null, "^_")],
            general.Args
        );
        Assert.Equal(0, query.CaptureIndex("x"));
        Assert.Null(query.CaptureIndex("y"));
    }

    [Theory]
    [InlineData("(no_such_node) @x", QueryErrorKind.NodeType, "no_such_node")]
    [InlineData("(call_expression nope: (identifier)) @x", QueryErrorKind.Field, "nope")]
    [InlineData("((identifier) @x (#eq? @y \"a\"))", QueryErrorKind.Capture, "y")]
    [InlineData("(identifier", QueryErrorKind.Syntax, null)]
    [InlineData("((identifier) @x (#eq? @x))", QueryErrorKind.Predicate, null)]
    [InlineData("((identifier) @x (#match? @x \"(\"))", QueryErrorKind.Predicate, null)]
    public void BadQueriesThrowWithKindAndDetail(string text, QueryErrorKind kind, string? detail)
    {
        var error = Assert.Throws<QueryException>(() => new Query(Language.JavaScript, text));

        Assert.Equal(kind, error.Kind);
        if (detail is not null)
        {
            Assert.Equal(detail, error.Detail);
        }
    }

    [Fact]
    public void SyntaxErrorsPointAtTheLine()
    {
        var error = Assert.Throws<QueryException>(() =>
            new Query(Language.JavaScript, "(identifier) @x\n(identifier))")
        );

        Assert.Equal(1, error.Row);
        Assert.Equal("(identifier))\n            ^", error.Detail);
    }

    [Fact]
    public void MatchLimitIsSettable()
    {
        using var cursor = new QueryCursor { MatchLimit = 8 };

        Assert.Equal(8, cursor.MatchLimit);
        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.MatchLimit = 0);
    }

    private static string Names(string queryText)
    {
        var source = Encoding.UTF8.GetBytes(Source);
        using var parser = new Parser(Language.JavaScript);
        using var tree = parser.Parse(source)!;
        using var query = new Query(Language.JavaScript, queryText);
        using var cursor = new QueryCursor();
        var index = query.CaptureIndex("f")!.Value;
        return string.Join(
            ',',
            cursor
                .Matches(query, tree.Root, source)
                .SelectMany(m => m.NodesForCapture(index))
                .Select(n => Encoding.UTF8.GetString(n.Text(source)))
        );
    }

    private static int MatchCount(string queryText, string text)
    {
        var source = Encoding.UTF8.GetBytes(text);
        using var parser = new Parser(Language.JavaScript);
        using var tree = parser.Parse(source)!;
        using var query = new Query(Language.JavaScript, queryText);
        using var cursor = new QueryCursor();
        return cursor.Matches(query, tree.Root, source).Count();
    }
}
