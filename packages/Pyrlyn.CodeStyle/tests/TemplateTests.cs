using System.Xml.Linq;

namespace Pyrlyn.CodeStyle.Tests;

// rtok-net is the package's first consumer: its root files must stay the package's templates.
public sealed class TemplateTests
{
    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, relative));

    [Fact]
    public void RepoCsharpierConfigIsTheTemplate()
    {
        Assert.Equal(Read("package/.csharpierrc.json"), Read("repo/.csharpierrc.json"));
    }

    [Fact]
    public void RepoEditorconfigStartsWithTheTemplate()
    {
        Assert.StartsWith(
            Read("package/.editorconfig"),
            Read("repo/.editorconfig"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void RepoMarkdownlintConfigExtendsTheTemplate()
    {
        Assert.Contains(
            "\"extends\": \"packages/Pyrlyn.CodeStyle/src/templates/.markdownlint.jsonc\"",
            Read("repo/.markdownlint-cli2.jsonc"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void GlobalconfigIsGlobal()
    {
        var lines = Read("package/Pyrlyn.CodeStyle.globalconfig")
            .Split('\n')
            .Where(l => l.Length > 0 && !l.StartsWith('#'));
        Assert.Equal("is_global = true", lines.First());
    }

    [Fact]
    public void PropsFeedTheGlobalconfigToTheCompiler()
    {
        var include = XDocument
            .Parse(Read("package/Pyrlyn.CodeStyle.props"))
            .Descendants("GlobalAnalyzerConfigFiles")
            .Single()
            .Attribute("Include")
            ?.Value;
        Assert.Equal("$(MSBuildThisFileDirectory)Pyrlyn.CodeStyle.globalconfig", include);
    }
}
