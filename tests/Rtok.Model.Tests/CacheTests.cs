using Rtok.Agents.Hosts;

namespace Rtok.Model.Tests;

public sealed class CacheTests
{
    [Fact]
    public async Task The_worktrees_page_is_reused_within_its_ttl()
    {
        using var fx = new ModelFixture();
        var inner = new CountingSource("worktrees");
        var source = new BackgroundSource<TextPage>(
            inner,
            new TextPage("reading worktrees…\n", Ready: false),
            ModelSources.WorktreesTtl
        );
        var context = fx.Context();

        Assert.Equal("reading worktrees…\n", source.Read(context).Text);
        await source.Reading;
        Assert.Equal("worktrees 1\n", source.Read(context).Text);

        fx.Clock.Advance(TimeSpan.FromSeconds(299));
        Assert.Equal("worktrees 1\n", source.Read(context).Text);
        await source.Reading;
        Assert.Equal(1, inner.Reads);

        fx.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal("worktrees 1\n", source.Read(context).Text); // stale, refreshing behind
        await source.Reading;
        Assert.Equal(2, inner.Reads);
        Assert.Equal("worktrees 2\n", source.Read(context).Text);
    }

    [Fact]
    public async Task Hosts_shows_probing_hosts_until_the_probe_lands()
    {
        using var fx = new ModelFixture();
        var probe = new CountingSource("hosts ok");
        probe.Gate.Reset();
        var sources = fx.Sources();
        var hosts = new BackgroundSource<TextPage>(
            probe,
            sources.Hosts.Read(fx.Context()),
            ModelSources.HostsTtl
        );
        var model = fx.Model(sources with { Hosts = hosts });

        Assert.Equal("probing hosts…\n", model.Build().Hosts.Text);
        Assert.Equal("probing hosts…\n", model.Build().Hosts.Text);
        probe.Gate.Set();
        await hosts.Reading;

        Assert.Equal("hosts ok 1\n", model.Build().Hosts.Text);
        fx.Clock.Advance(ModelSources.HostsTtl);
        Assert.Equal("hosts ok 1\n", model.Build().Hosts.Text);
        await hosts.Reading;
        Assert.Equal(2, probe.Reads);
    }

    [Fact]
    public async Task The_usage_page_is_the_agents_usage_screen_read_in_the_background()
    {
        using var fx = new ModelFixture();
        var usage = (BackgroundSource<TextPage>)fx.Sources().Usage;
        var context = fx.Context();

        Assert.Equal(new TextPage("reading usage…\n", Ready: false), usage.Read(context));
        await usage.Reading;

        var page = usage.Read(context);
        Assert.True(page.Ready);
        Assert.Equal(new UsageSource(fx.Store).Read(context), page);
    }

    [Fact]
    public async Task A_failed_background_read_keeps_the_placeholder_and_retries()
    {
        using var fx = new ModelFixture();
        var fail = true;
        var source = new BackgroundSource<TextPage>(
            new FuncSource(() => fail ? throw new IOException("gone") : new TextPage("ok\n", true)),
            new TextPage("probing hosts…\n", Ready: false),
            ModelSources.HostsTtl
        );
        var context = fx.Context();

        source.Read(context);
        await source.Reading;
        Assert.Equal("gone", source.Error);
        fail = false;
        Assert.Equal("probing hosts…\n", source.Read(context).Text); // starts the retry
        await source.Reading;

        Assert.Equal("ok\n", source.Read(context).Text);
        Assert.Null(source.Error);
    }

    private sealed class FuncSource(Func<TextPage> read) : IPageSource<TextPage>
    {
        public TextPage Read(ModelContext context) => read();
    }

    [Fact]
    public async Task The_hosts_page_is_the_agents_list_screen_read_in_the_background()
    {
        using var fx = new ModelFixture();
        var hosts = (BackgroundSource<TextPage>)fx.Sources().Hosts;
        var context = fx.Context();

        Assert.Equal("probing hosts…\n", hosts.Read(context).Text);
        await hosts.Reading;

        var page = hosts.Read(context);
        Assert.True(page.Ready);
        Assert.Equal(new HostRun(ModelFixture.Hosts()).List(AgentHosts.All), page.Text);
        Assert.Contains("not found", page.Text, StringComparison.Ordinal);
    }
}
