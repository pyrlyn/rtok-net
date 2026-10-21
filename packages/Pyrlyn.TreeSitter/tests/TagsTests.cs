using System.Text;
using Pyrlyn.TreeSitter.Tags;

namespace Pyrlyn.TreeSitter.Tests;

public sealed class TagsTests
{
    private const string Locals = """
        (function_declaration) @local.scope
        (statement_block) @local.scope
        (variable_declarator name: (identifier) @local.definition)
        (formal_parameters (identifier) @local.definition)
        """;

    private const string Calls = """
        (call_expression function: (identifier) @name (#is-not? local)) @reference.call
        (function_declaration name: (identifier) @name) @definition.function
        """;

    [Fact]
    public void NamesDefinedInAnEnclosingScopeAreNotReferences()
    {
        var tags = Tags(Calls, Locals, "function f(g) { const h = 1; g(); h(); k(); }");

        Assert.Equal(["def function f", "ref call k"], tags);
    }

    [Fact]
    public void ScopeInheritsFalseStopsTheLookup()
    {
        const string locals = """
            (program) @local.scope
            (variable_declarator name: (identifier) @local.definition)
            ((statement_block) @local.scope (#set! local.scope-inherits false))
            """;

        var tags = Tags(Calls, locals, "const g = 1; g(); function f() { g(); }");

        Assert.Equal(["def function f", "ref call g"], tags);
    }

    [Fact]
    public void LowerPatternWinsWhenTwoPatternsTagOneName()
    {
        const string query = """
            (call_expression function: (identifier) @name) @reference.call
            (identifier) @name @reference.word
            """;

        Assert.Equal(["ref call f", "ref word x"], Tags(query, "", "f(x);"));
    }

    [Fact]
    public void DocsKeepTheAdjacentCommentsAndStripThem()
    {
        const string query = """
            ((comment)* @doc
             .
             (function_declaration name: (identifier) @name) @definition.function
             (#select-adjacent! @doc @definition.function)
             (#strip! @doc "^//\\s*"))
            """;
        const string source = "// far\n\n// near one\n// near two\nfunction f() {}\n";

        var tag = Assert.Single(Generate(query, "", source).Tags);

        Assert.Equal("near one\nnear two", tag.Docs);
    }

    [Fact]
    public void LineRangeTrimsWhitespaceAndStopsAtTheNewline()
    {
        var source = "  \t function f() {}  \nnext"u8.ToArray();

        var range = TagsContext.LineRange(source, 14, new Point(0, 14), 180);

        Assert.Equal(
            "function f() {}",
            Encoding.UTF8.GetString(source.AsSpan(range.Start, range.Length))
        );
    }

    [Fact]
    public void LineRangeIsCutAtTheLimitOnACharacterBoundary()
    {
        // 'é' is two bytes; a limit of 4 lands inside the second one.
        var source = "aéé!"u8.ToArray();

        var range = TagsContext.LineRange(source, 0, new Point(0, 0), 4);

        Assert.Equal(new TextRange(0, 3), range);
    }

    [Fact]
    public void LongLinesAreCutAt180Bytes()
    {
        var source = "function f() {}" + new string(' ', 10) + new string('x', 300);

        var tag = Assert.Single(Generate(Calls, "", source).Tags);

        Assert.Equal(new TextRange(0, 180), tag.LineRange);
    }

    [Fact]
    public void Utf16ColumnsCountCodeUnitsAndContinueOnOneLine()
    {
        const string query = "(call_expression function: (identifier) @name) @reference.call";

        var tags = Generate(query, "", "s('😀'); éf(); g();").Tags;

        Assert.Equal(
            [new TextRange(0, 1), new TextRange(9, 11), new TextRange(15, 16)],
            tags.Select(t => t.Utf16ColumnRange)
        );
        Assert.All(tags, t => Assert.Equal(new TextRange(0, 22), t.LineRange));
    }

    [Fact]
    public void RangeSpansTheTaggedNodeAndTheName()
    {
        var tag = Assert.Single(Generate(Calls, "", "x;\nfunction f() {}").Tags);

        Assert.Equal(new TextRange(3, 18), tag.Range);
        Assert.Equal(new TextRange(12, 13), tag.NameRange);
        Assert.Equal(new Point(1, 9), tag.SpanStart);
        Assert.True(tag.IsDefinition);
    }

    [Fact]
    public void NamesInsideErrorsAreSkippedAndTheErrorIsReported()
    {
        const string source = "function f() {} function (";
        var config = new TagsConfiguration(Language.JavaScript, Calls, "");
        using var context = new TagsContext();
        var result = context.GenerateTags(
            config,
            Encoding.UTF8.GetBytes(source),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.HasError);
        Assert.Equal(["def function f"], Render(config, Encoding.UTF8.GetBytes(source), result));
    }

    [Fact]
    public void IgnoredNamesClaimTheirNode()
    {
        const string query = """
            ((identifier) @ignore (#eq? @ignore "skip"))
            (call_expression function: (identifier) @name) @reference.call
            """;

        Assert.Equal(["ref call f"], Tags(query, "", "skip(); f(); skip();"));
    }

    [Fact]
    public void UnknownCapturesAreRejected()
    {
        var error = Assert.Throws<QueryException>(() =>
            new TagsConfiguration(Language.JavaScript, "(identifier) @bogus", "")
        );

        Assert.Equal(QueryErrorKind.Capture, error.Kind);
    }

    [Fact]
    public void CancellationStopsTheRun()
    {
        using var context = new TagsContext();
        var config = new TagsConfiguration(Language.JavaScript, Calls, "");

        Assert.Throws<OperationCanceledException>(() =>
            context.GenerateTags(config, "f();"u8.ToArray(), new CancellationToken(canceled: true))
        );
    }

    private static List<string> Tags(string tags, string locals, string source)
    {
        var config = new TagsConfiguration(Language.JavaScript, tags, locals);
        var bytes = Encoding.UTF8.GetBytes(source);
        using var context = new TagsContext();
        return Render(config, bytes, context.GenerateTags(config, bytes));
    }

    private static List<string> Render(
        TagsConfiguration config,
        byte[] source,
        TagsResult result
    ) =>
        [
            .. result.Tags.Select(t =>
                (t.IsDefinition ? "def " : "ref ")
                + config.SyntaxTypeName(t.SyntaxTypeId)
                + " "
                + Encoding.UTF8.GetString(source, t.NameRange.Start, t.NameRange.Length)
            ),
        ];

    private static TagsResult Generate(string tags, string locals, string source)
    {
        using var context = new TagsContext();
        return context.GenerateTags(
            new TagsConfiguration(Language.JavaScript, tags, locals),
            Encoding.UTF8.GetBytes(source)
        );
    }
}
