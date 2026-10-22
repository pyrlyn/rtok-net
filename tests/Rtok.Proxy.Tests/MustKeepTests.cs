namespace Rtok.Proxy.Tests;

public sealed class MustKeepTests
{
    private const string Log = """
        Compiling a
        error[E0308]: mismatched types
          --> crates/x/src/lib.rs:42:7
        connect failed: Connection refused (os error 61) addr=10.0.0.1:5432
        ran `RetryBudget` in 248 ms; run `acme settle --retry bat_01HZX4M8Q2`
        id 9c1e5ab07f2d44e8 settled=2 MAX_BACKOFF_MS
        $ cargo test --lib
        41 passed; 2 failed
        """;

    private static List<string> Of(string kind) =>
        [
            .. MustKeep
                .Spans(Log)
                .Where(s => string.Equals(s.Kind, kind, StringComparison.Ordinal))
                .Select(s => s.Text),
        ];

    [Fact]
    public void Every_kind_is_found_once_in_order()
    {
        Assert.Equal("error[E0308]: mismatched types", Of("error")[0]);
        Assert.Contains("E0308", Of("error"), StringComparer.Ordinal);
        Assert.Contains("crates/x/src/lib.rs:42:7", Of("path"), StringComparer.Ordinal);
        Assert.Contains("`RetryBudget`", Of("identifier"), StringComparer.Ordinal);
        Assert.Contains("MAX_BACKOFF_MS", Of("identifier"), StringComparer.Ordinal);
        Assert.Contains("248 ms", Of("number"), StringComparer.Ordinal);
        Assert.Contains("settled=2", Of("number"), StringComparer.Ordinal);
        Assert.Contains("2 failed", Of("number"), StringComparer.Ordinal);
        Assert.Contains(
            "`acme settle --retry bat_01HZX4M8Q2`",
            Of("command"),
            StringComparer.Ordinal
        );
        Assert.Contains("cargo test --lib", Of("command"), StringComparer.Ordinal);
        Assert.Contains("9c1e5ab07f2d44e8", Of("id"), StringComparer.Ordinal);
        Assert.Contains("10.0.0.1:5432", Of("id"), StringComparer.Ordinal);
        var all = MustKeep.Spans(Log);
        Assert.Equal(all.Count, all.Select(s => s.Text).Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, s => Assert.Contains(s.Kind, MustKeep.Kinds, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("identifier", "MAX_BACKOFF_MS")]
    [InlineData("path", "crates/x/src/lib.rs:42:7")]
    [InlineData("number", "248 ms")]
    [InlineData("error", "error[E0308]: mismatched types")]
    public void The_guard_rejects_a_summary_that_drops_a_span(string kind, string span)
    {
        var all = MustKeep.Spans(Log);
        Assert.Empty(MustKeep.Dropped(all, Log));

        var gone = MustKeep.Dropped(all, Log.Replace(span, "somewhere", StringComparison.Ordinal));

        Assert.Contains(
            gone,
            s =>
                string.Equals(s.Kind, kind, StringComparison.Ordinal)
                && s.Text.Contains(span, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void A_dropped_location_names_it_and_the_file_inside_it()
    {
        var all = MustKeep.Spans(Log);

        var gone = MustKeep.Dropped(
            all,
            Log.Replace("crates/x/src/lib.rs:42:7", "somewhere", StringComparison.Ordinal)
        );

        Assert.Equal(2, gone.Count);
        Assert.All(
            gone,
            s => Assert.Contains("crates/x/src/lib.rs", s.Text, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void A_long_line_is_cut_on_a_character_boundary()
    {
        var spans = MustKeep.Spans("error: " + string.Concat(Enumerable.Repeat("😀", 400)));

        Assert.Equal(MustKeep.SpanChars, spans[0].Text.EnumerateRunes().Count());
    }
}
