using Rtok.Agents.Junk;

namespace Rtok.Agents.Tests;

public sealed class JunkViewTests
{
    [Theory]
    [InlineData("4096", 4096L)]
    [InlineData("512K", 524_288L)]
    [InlineData("10M", 10_485_760L)]
    [InlineData("1GB", 1_073_741_824L)]
    [InlineData("1kb", 1024L)]
    [InlineData(" 2m ", 2_097_152L)]
    [InlineData("0", 0L)]
    [InlineData("", null)]
    [InlineData("M", null)]
    [InlineData("-1", null)]
    [InlineData("1.5M", null)]
    [InlineData("10T", null)]
    [InlineData("9223372036854775807G", null)]
    public void Min_size_takes_bytes_or_a_binary_unit(string text, long? bytes) =>
        Assert.Equal(bytes, JunkView.ParseSize(text));

    [Theory]
    [InlineData("all", int.MaxValue)]
    [InlineData("0", 0)]
    [InlineData("12", 12)]
    [InlineData("-1", null)]
    [InlineData("ten", null)]
    public void Items_takes_a_count_or_all(string text, int? shown) =>
        Assert.Equal(shown, JunkView.ParseShown(text));

    [Fact]
    public void A_bad_kind_or_agent_names_the_choices()
    {
        Assert.Null(JunkFilter.KindError("cache"));
        Assert.Null(JunkFilter.KindError("archive"));
        Assert.StartsWith(
            "not a junk kind: bogus (one of log, archive, cache,",
            JunkFilter.KindError("bogus"),
            StringComparison.Ordinal
        );
        Assert.Null(JunkFilter.AgentError("rtok", []));
        Assert.Null(JunkFilter.AgentError("claude", ["claude"]));
        Assert.Equal(
            "not an agent: nobody (rtok or a host id; see `rtok agents list`)",
            JunkFilter.AgentError("nobody", ["claude"])
        );
    }

    [Fact]
    public void Only_rtok_alone_is_the_default_selection()
    {
        Assert.True(new JunkFilter().OwnOnly);
        Assert.True(new JunkFilter { Agents = ["rtok"], Trash = true }.OwnOnly);
        Assert.False(new JunkFilter { Agents = ["claude"] }.OwnOnly);
        Assert.False(new JunkFilter { IncludeReview = true }.OwnOnly);
        Assert.False(new JunkFilter { MinSize = 1 }.OwnOnly);
    }

    [Theory]
    [InlineData("cache", "safe", false, true)]
    [InlineData("logs", "review", false, false)]
    [InlineData("logs", "review", true, true)]
    [InlineData("sessions", "explicit", true, false)]
    public void Without_kinds_the_class_decides(
        string kind,
        string cls,
        bool review,
        bool wanted
    ) => Assert.Equal(wanted, new JunkFilter { IncludeReview = review }.WantsKind(kind, cls));

    [Fact]
    public void A_named_kind_takes_review_and_explicit_classes()
    {
        var filter = new JunkFilter { Kinds = ["logs", "sessions"] };

        Assert.True(filter.WantsKind("logs", "review"));
        Assert.True(filter.WantsKind("sessions", "explicit"));
        Assert.False(filter.WantsKind("cache", "safe"));
        Assert.False(filter.WantsKind("logs", "never"));
    }

    [Fact]
    public void A_path_under_home_prints_with_a_tilde()
    {
        var home = Path.Combine(Path.GetTempPath(), "h");
        var view = new JunkView { Home = home };

        Assert.Equal("~/.claude/debug", view.Tilde(Path.Combine(home, ".claude", "debug")));
        Assert.Equal("~", view.Tilde(home));
        Assert.Equal("/elsewhere", view.Tilde("/elsewhere"));
    }

    [Fact]
    public void A_time_reads_relative_then_absolute()
    {
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        var view = new JunkView { Now = now, Zone = TimeZoneInfo.Utc };

        Assert.Equal("3 weeks ago (2026-10-01 12:00)", view.When(now.AddDays(-21)));
        Assert.Equal("1 hour ago (2026-10-22 11:00)", view.When(now.AddHours(-1)));
        Assert.Equal("unknown", view.When(null));
    }

    [Fact]
    public void A_terminal_gets_links_and_binary_units()
    {
        var path = Path.Combine(Path.GetTempPath(), "a b");

        Assert.Equal(
            $"\u001b]8;;{new Uri(path).AbsoluteUri}\u001b\\x\u001b]8;;\u001b\\",
            JunkView.Link(path, "x")
        );
        Assert.Equal("rel", JunkView.Link("rel", "rel"));
        Assert.Equal("1.5 KB", new JunkView().Size(1536));
        Assert.Equal("1536", new JunkView { Exact = true }.Size(1536));
    }
}
