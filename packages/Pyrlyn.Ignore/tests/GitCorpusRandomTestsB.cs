namespace Pyrlyn.Ignore.Tests;

/// <summary>Seeds 5-8 of <see cref="GitCorpusRandomTests"/>.</summary>
public sealed class GitCorpusRandomTestsB : GitCorpusRandomTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void RandomRulesAndTreesB(int seed) => RandomRulesAndTrees(seed);
}
