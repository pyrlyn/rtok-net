namespace Pyrlyn.OperationIcons.Tests;

public sealed class VerbIconsTests
{
    [Theory]
    [InlineData("uninstalling", "🧹")]
    [InlineData("removed", "🧹")]
    [InlineData("pruning", "🧹")]
    [InlineData("installed", "📦")]
    [InlineData("upgrading", "⏫")]
    [InlineData("updated", "⏫")]
    [InlineData("downloading", "⏬")]
    [InlineData("fetching", "⏬")]
    [InlineData("linked", "🔗")]
    [InlineData("rolled back", "⏪")]
    [InlineData("searching", "🔍")]
    [InlineData("doctor", "🩺")]
    public void EachVerbGetsItsIcon(string verb, string icon)
    {
        Assert.Equal(icon, VerbIcons.Icon(verb));
    }

    [Fact]
    public void UninstallWinsOverInstallAndCaseIsIgnored()
    {
        Assert.Equal("🧹", VerbIcons.Icon("Uninstalled"));
        Assert.Equal("📦", VerbIcons.Icon("ALREADY INSTALLED"));
    }

    [Fact]
    public void AnUnknownVerbHasNoIcon()
    {
        Assert.Null(VerbIcons.Icon("thinking"));
        Assert.Null(VerbIcons.Icon(""));
    }

    [Fact]
    public void CallerRowsExtendTheLookup()
    {
        VerbIcon[] rows = [new("index", "📚")];
        Assert.Equal("📚", VerbIcons.Icon(rows, "indexing"));
        Assert.Null(VerbIcons.Icon(rows, "installing"));
    }

    public static TheoryData<string> TableIcons() => [.. VerbIcons.Shared.Select(r => r.Icon)];

    [Theory]
    [MemberData(nameof(TableIcons))]
    public void EveryTableIconMeasuresTheIconWidth(string icon)
    {
        Assert.Equal(VerbIcons.IconWidth, VerbIcons.Width(icon));
        Assert.Equal(0, VerbIcons.Padding(icon));
        Assert.Equal(VerbIcons.IconWidth + 1, VerbIcons.Width(VerbIcons.Gutter(icon)));
    }

    [Theory]
    [InlineData("→", 1)]
    [InlineData("a", 1)]
    [InlineData("", 0)]
    [InlineData("⚠", 1)]
    [InlineData("⚠️", 2)]
    [InlineData("❗", 2)]
    [InlineData("界", 2)]
    [InlineData("é", 1)]
    [InlineData("\t", 0)]
    public void WidthIsMeasured(string text, int width)
    {
        Assert.Equal(width, VerbIcons.Width(text));
    }

    [Fact]
    public void ANarrowIconIsPaddedAndNoIconIsBlank()
    {
        Assert.Equal(1, VerbIcons.Padding("→"));
        Assert.Equal("→  ", VerbIcons.Gutter("→"));
        Assert.Equal("📦 ", VerbIcons.Gutter("📦"));
        Assert.Equal("   ", VerbIcons.Gutter(null));
    }

    [Fact]
    public void GuttersWithAndWithoutAnIconHaveEqualWidth()
    {
        var with = VerbIcons.Width(VerbIcons.Gutter(VerbIcons.Icon("install")));
        var without = VerbIcons.Width(VerbIcons.Gutter(null));
        Assert.Equal(without, with);
        Assert.Equal(VerbIcons.IconWidth + 1, with);
    }
}
