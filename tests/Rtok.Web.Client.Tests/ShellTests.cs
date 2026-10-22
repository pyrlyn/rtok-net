using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Rtok.Web.Client.Layout;
using Rtok.Web.Client.Pages;
using Rtok.Web.Client.Services;
using Rtok.Web.Contracts;

namespace Rtok.Web.Client.Tests;

public sealed class ShellTests : IAsyncDisposable
{
    private readonly BunitContext _context = new();
    private readonly ScriptedTransport _transport = new();

    public ShellTests()
    {
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.Services.AddRadzenComponents();
        _context.Services.AddSingleton<IWsTransport>(_transport);
        _context.Services.AddScoped<DashboardConnection>();
        _context.Services.AddScoped<ThemeStore>();
    }

    [Fact]
    public void The_shell_shows_loading_then_the_overview_then_offline_with_reconnect()
    {
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/?token=t0k&x=1");

        var shell = RenderShell();

        shell.WaitForAssertion(() => shell.Find(".rtok-loading"));
        Assert.Equal("ws://localhost/ws?token=t0k", Assert.Single(_transport.Connects).ToString());
        Assert.Equal(
            "http://localhost/?x=1",
            _context.Services.GetRequiredService<NavigationManager>().Uri
        );
        _context.JSInterop.VerifyInvoke("sessionStorage.setItem");

        _transport.Push(new SnapshotFrame(ScriptedTransport.Snapshot(1234)));

        shell.WaitForAssertion(() =>
        {
            Assert.Empty(shell.FindAll(".rtok-loading"));
            Assert.Contains(
                "1,234",
                shell.Find(".rtok-kpis").TextContent,
                StringComparison.Ordinal
            );
            shell.Find(".rtok-no-savings");
            Assert.Equal("live", shell.Find(".rtok-state").TextContent.Trim());
        });

        _transport.Close();

        shell.WaitForAssertion(() =>
        {
            Assert.Contains(
                "the connection to rtok closed",
                shell.Find(".rtok-offline").TextContent,
                StringComparison.Ordinal
            );
            Assert.Equal("offline", shell.Find(".rtok-state").TextContent.Trim());
            // The last snapshot stays on screen while offline.
            Assert.Contains(
                "1,234",
                shell.Find(".rtok-kpis").TextContent,
                StringComparison.Ordinal
            );
        });

        shell.Find(".rtok-reconnect").Click();

        shell.WaitForAssertion(() => Assert.Equal(2, _transport.Connects.Count));
        _transport.Push(new SnapshotFrame(ScriptedTransport.Snapshot(5)));
        shell.WaitForAssertion(() =>
        {
            Assert.Empty(shell.FindAll(".rtok-offline"));
            Assert.Equal("live", shell.Find(".rtok-state").TextContent.Trim());
        });
    }

    [Fact]
    public void Without_a_token_in_the_address_the_saved_one_is_used()
    {
        _context
            .JSInterop.Setup<string?>("sessionStorage.getItem", DashboardConnection.TokenKey)
            .SetResult("saved");

        RenderShell();

        Assert.Equal(
            "ws://localhost/ws?token=saved",
            Assert.Single(_transport.Connects).ToString()
        );
    }

    [Fact]
    public async Task A_request_gets_the_frame_that_echoes_its_id()
    {
        var connection = _context.Services.GetRequiredService<DashboardConnection>();
        await connection.StartAsync();

        var answer = connection.SendAsync(
            new WsRequest { Toggle = new ToggleRequest("docs", false) }
        );
        _transport.Push(new DoneFrame("other") { Req = 99 });
        _transport.Push(new DoneFrame("plugins.docs.enabled = false") { Req = 1 });

        Assert.Equal("plugins.docs.enabled = false", Assert.IsType<DoneFrame>(await answer).Text);
        Assert.Contains("\"req\":1", Assert.Single(_transport.Sent), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_waiting_when_the_socket_drops_is_answered_offline()
    {
        var connection = _context.Services.GetRequiredService<DashboardConnection>();
        await connection.StartAsync();

        var answer = connection.SendAsync(new WsRequest { Expand = "x" });
        _transport.Close();

        Assert.Equal("rtok is offline", Assert.IsType<MessageFrame>(await answer).Text);
    }

    [Fact]
    public void The_theme_toggle_switches_the_radzen_theme_and_keeps_it_in_local_storage()
    {
        var shell = RenderShell();
        var themes = _context.Services.GetRequiredService<ThemeService>();
        Assert.Equal(ThemeStore.LightTheme, themes.Theme);

        shell.Find(".rtok-theme").Click();

        Assert.Equal(ThemeStore.DarkTheme, themes.Theme);
        var saved = _context.JSInterop.VerifyInvoke("localStorage.setItem");
        Assert.Equal([ThemeStore.Key, "dark"], saved.Arguments);
    }

    [Fact]
    public void The_saved_theme_is_applied_on_load()
    {
        _context.JSInterop.Setup<string?>("localStorage.getItem", ThemeStore.Key).SetResult("dark");

        RenderShell();

        Assert.Equal(
            ThemeStore.DarkTheme,
            _context.Services.GetRequiredService<ThemeService>().Theme
        );
    }

    [Theory]
    [InlineData("?token=a%2Bb&x=1", "token", "a+b")]
    [InlineData("?x=1", "token", null)]
    [InlineData("?token", "token", "")]
    public void Query_values(string query, string name, string? value) =>
        Assert.Equal(value, DashboardConnection.QueryValue(query, name));

    [Theory]
    [InlineData("?token=a&x=1", "?x=1")]
    [InlineData("?token=a", "")]
    [InlineData("", "")]
    public void The_token_leaves_the_address(string query, string rest) =>
        Assert.Equal(rest, DashboardConnection.RemoveQueryValue(query, "token"));

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    private IRenderedComponent<MainLayout> RenderShell() =>
        _context.Render<MainLayout>(p =>
            p.Add(
                l => l.Body,
                b =>
                {
                    b.OpenComponent<Overview>(0);
                    b.CloseComponent();
                }
            )
        );
}
