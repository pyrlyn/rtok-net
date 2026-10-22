using System.Net;
using System.Net.Sockets;
using CliWrap;
using Rtok.Testing;

namespace Rtok.Smoke;

/// <summary>The published <c>rtok proxy</c>: Kestrel slim starts under NativeAOT and answers
/// <c>/health</c> on loopback, then stops on Ctrl+C.</summary>
public sealed class ProxyTests
{
    [Fact]
    public async Task Proxy_starts_and_answers_health_on_loopback()
    {
        var ct = TestContext.Current.CancellationToken;
        var rtok = await PublishedApps.PathAsync("rtok", ct);
        using var home = new TempDir("proxy-");
        var port = FreePort();
        using var forceful = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var graceful = new CancellationTokenSource();
        var run = Cli.Wrap(rtok)
            .WithArguments([
                "proxy",
                "--port",
                port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ])
            .WithEnvironmentVariables(env =>
                env.Set("HOME", home.Path).Set("USERPROFILE", home.Path).Set("RTOK_HOME", null)
            )
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(forceful.Token, graceful.Token);

        try
        {
            var health = await HealthAsync($"http://127.0.0.1:{port}/health", ct);

            Assert.Equal(
                """{"ok":true,"mode":"passthrough","enabled":true,"recording":true}""",
                health
            );
        }
        finally
        {
            graceful.Cancel();
            forceful.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await run;
            }
            catch (OperationCanceledException) { }
        }
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static async Task<string> HealthAsync(string url, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                return await client.GetStringAsync(new Uri(url), ct);
            }
            catch (HttpRequestException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(100, ct);
            }
        }
    }
}
