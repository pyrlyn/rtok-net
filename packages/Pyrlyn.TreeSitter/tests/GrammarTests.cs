namespace Pyrlyn.TreeSitter.Tests;

public sealed class GrammarTests
{
    private static readonly string FixtureDir = Path.Join(AppContext.BaseDirectory, "fixtures");

    public static TheoryData<string> Fixtures() =>
        [.. TagsGolden.Fixtures(FixtureDir).Select(f => Path.GetFileName(f))];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TagsOfEachFixtureMatchItsGolden(string fixture)
    {
        var (expected, actual) = TagsGolden.Check(Path.Join(FixtureDir, fixture));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EveryGrammarHasAFixture()
    {
        var fixtures = TagsGolden
            .Fixtures(FixtureDir)
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Grammar.All, g => Assert.Contains(g.Id, fixtures));
    }

    [Fact]
    public void EveryBundledTagsQueryCompiles()
    {
        Assert.All(Grammar.All, g => Assert.NotEmpty(g.Tags.SyntaxTypeNames));
    }

    [Theory]
    [InlineData("rs", "rust")]
    [InlineData("rust", "rust")]
    [InlineData("ts", "typescript")]
    [InlineData("tsx", "tsx")]
    [InlineData("mjs", "javascript")]
    [InlineData("cjs", "javascript")]
    [InlineData("py", "python")]
    [InlineData("h", "c")]
    [InlineData("kts", "kotlin")]
    [InlineData("cs", "c_sharp")]
    [InlineData("rb", "ruby")]
    public void FindResolvesExtensionsAndAliases(string name, string id)
    {
        Assert.Equal(id, Grammar.Find(name)?.Id);
    }

    [Fact]
    public void FindIsOrdinalAndReturnsNullForUnknownNames()
    {
        Assert.Null(Grammar.Find("RS"));
        Assert.Null(Grammar.Find("txt"));
    }

    [Fact]
    public void TagsQueryIsUpstreamThenRtokExtras()
    {
        var rust = Grammar.Find("rust")!;

        Assert.StartsWith(Grammar.Read("rust/tags.scm"), rust.TagsQuery, StringComparison.Ordinal);
        Assert.EndsWith(Grammar.Read("rust/extra.scm"), rust.TagsQuery, StringComparison.Ordinal);
        Assert.Equal("", rust.LocalsQuery);
    }
}
