using Pyrlyn.TreeSitter;
using Pyrlyn.TreeSitter.Tests;

namespace Rtok.AotTests;

// Pyrlyn.TreeSitter linked statically into the native executable: every fixture's tags match
// the goldens its JIT tests check.
public sealed class TreeSitterTests
{
    [Fact]
    public void Tags_of_every_fixture_match_the_goldens()
    {
        var fixtures = TagsGolden
            .Fixtures(Path.Join(AppContext.BaseDirectory, "tree-sitter-fixtures"))
            .ToList();

        var mismatched = fixtures
            .Where(f =>
                TagsGolden.Check(f) is var (expected, actual)
                && !string.Equals(expected, actual, StringComparison.Ordinal)
            )
            .Select(Path.GetFileName);

        Assert.Equal(Grammar.All.Count, fixtures.Count);
        Assert.Empty(mismatched);
    }
}
