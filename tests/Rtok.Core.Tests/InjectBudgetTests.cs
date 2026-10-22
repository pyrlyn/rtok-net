using System.Text;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Core.Tests;

// Ported from rtok's inject::apply and hooks::cap_budget tests. MemoryHost prices prose at
// 4 characters a token, so Blob(n) is exactly n tokens.
public sealed class InjectBudgetTests
{
    [Fact]
    public void Three_500s_in_800_keep_two_drop_one_and_are_byte_stable()
    {
        var host = new MemoryHost();
        var text = Blob(500);
        Injection[] offered = [new("a", text, 3), new("b", text, 2), new("c", text, 1)];
        var once = InjectBudget.Apply(host, offered, 800);
        var twice = InjectBudget.Apply(host, offered, 800);
        Assert.Equal(once, twice);
        Assert.Equal($"{text}\n{text}\ndropped:c:500", once);
        Assert.Equal(2, host.Recorded().Count);
        Assert.Equal("dropped:c:500", host.Recorded()[0].RefId);
    }

    [Fact]
    public void A_candidate_ten_times_the_budget_keeps_a_prefix_and_its_marker()
    {
        var host = new MemoryHost();
        var once = InjectBudget.Apply(host, [new("huge", Blob(8000), 5)], 800);
        Assert.Equal(once, InjectBudget.Apply(host, [new("huge", Blob(8000), 5)], 800));
        Assert.EndsWith("\ndropped:huge:8000", once, StringComparison.Ordinal);
        Assert.True(host.Estimate(once, TextClass.Prose) <= 800, once);
    }

    [Fact]
    public void The_budget_is_read_from_the_inject_section()
    {
        Assert.Equal(800, InjectBudget.Tokens(new MemoryHost()));
        Assert.Equal(
            40,
            InjectBudget.Tokens(
                new MemoryHost("""{"plugins": {"inject": {"budget_tokens": 40}}}""")
            )
        );
    }

    [Fact]
    public void Cap_keeps_a_prefix_when_the_first_line_alone_is_over()
    {
        var host = new MemoryHost();
        var want = string.Join(' ', Enumerable.Repeat("word", 400));
        var capped = InjectBudget.Cap(host, want, 40);
        Assert.NotEmpty(capped);
        Assert.Equal(want, Expand(host, capped));
        Assert.True(host.Estimate(capped, TextClass.Prose) <= 40, capped);
    }

    [Fact]
    public void Cap_gives_back_lines_so_the_marker_fits()
    {
        var host = new MemoryHost();
        var text = string.Join('\n', Enumerable.Range(0, 200).Select(i => $"l{i}"));
        var capped = InjectBudget.Cap(host, text, 40);
        Assert.True(host.Estimate(capped, TextClass.Prose) <= 40, capped);
        var cut = capped.LastIndexOf('\n');
        var marker = capped[(cut + 1)..];
        Assert.StartsWith("dropped:post_tool:", marker, StringComparison.Ordinal);
        Assert.Equal(text, capped[..cut] + "\n" + Expand(host, capped));
    }

    [Fact]
    public void Cap_keeps_fitting_lines_and_names_the_rest()
    {
        var host = new MemoryHost();
        var want = string.Join(' ', Enumerable.Repeat("word", 400));
        var capped = InjectBudget.Cap(host, "ok\n" + want, 30);
        Assert.StartsWith("ok\n", capped, StringComparison.Ordinal);
        Assert.Equal(want, Expand(host, capped));
        Assert.True(host.Estimate(capped, TextClass.Prose) <= 30, capped);
    }

    [Fact]
    public void Cap_leaves_text_under_budget_alone() =>
        Assert.Equal("a\r\nb\n", InjectBudget.Cap(new MemoryHost(), "a\r\nb\n", 40));

    [Fact]
    public void Fit_cuts_on_whole_unicode_scalars() =>
        Assert.Equal("😀", InjectBudget.Fit(new MemoryHost(), "😀😀😀", TextClass.Cjk, 1));

    private static string Blob(int tokens) => new('x', tokens * 4);

    // The 8-hex id after "rtok expand " in the last line, resolved to the one archived id it
    // starts, read back from the archive.
    private static string Expand(MemoryHost host, string capped)
    {
        var id = capped[(capped.LastIndexOf(' ') + 1)..];
        Assert.Equal(ArchiveIds.ShortLength, id.Length);
        var full = Assert.Single(host.Archived(), a => a.StartsWith(id, StringComparison.Ordinal));
        return Encoding.UTF8.GetString(host.GetArchive(full) ?? []);
    }
}
