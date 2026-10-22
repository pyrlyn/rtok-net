namespace Pyrlyn.Ignore.Tests;

/// <summary>Seeds 1-4 of <see cref="GitCorpusRandomTests"/>.</summary>
public sealed class GitCorpusRandomTestsA : GitCorpusRandomTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RandomRulesAndTreesA(int seed) => RandomRulesAndTrees(seed);
}
