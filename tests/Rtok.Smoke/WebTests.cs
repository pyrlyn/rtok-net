using System.Net;
using System.Net.WebSockets;
using System.Text;
using CliWrap;
using Rtok.Testing;

namespace Rtok.Smoke;

public sealed class WebTests
{
    [Fact]
    public async Task Rtok_web_serves_health_the_client_and_a_snapshot_over_the_socket()
    {
        var ct = TestContext.Current.CancellationToken;
        var rtok = await PublishedApps.PathAsync("rtok", ct);
        using var home = new TempDir();
        var printed = new TaskCompletionSource<Uri>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var stop = new CancellationTokenSource();
        var run = Cli.Wrap(rtok)
            .WithArguments(["web", "--port", "0"])
            .WithEnvironmentVariables(e =>
                e.Set("HOME", home.Path)
                    .Set("USERPROFILE", home.Path)
                    .Set("RTOK_HOME", home.Combine(".rtok"))
            )
            .WithStandardErrorPipe(
                PipeTarget.ToDelegate(line =>
                {
                    if (line.StartsWith("rtok web http", StringComparison.Ordinal))
                    {
                        printed.TrySetResult(new Uri(line["rtok web ".Length..]));
                    }
                })
            )
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(stop.Token);
        try
        {
            var url = await printed.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            await ServesAsync(url, ct);
        }
        finally
        {
            await stop.CancelAsync();
            try
            {
                await run;
            }
            catch (OperationCanceledException) { }
        }
    }

    // The client, its CSP and a first snapshot frame, all from the address rtok printed.
    private static async Task ServesAsync(Uri url, CancellationToken ct)
    {
        using var http = new HttpClient { BaseAddress = url };

        using var health = await http.GetAsync(new Uri("/health", UriKind.Relative), ct);
        using var index = await http.GetAsync(new Uri("/", UriKind.Relative), ct);
        using var blazor = await http.GetAsync(
            new Uri("/_framework/blazor.webassembly.js", UriKind.Relative),
            ct
        );

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Contains(
            "'wasm-unsafe-eval'",
            string.Join(' ', index.Headers.GetValues("Content-Security-Policy")),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "_framework/blazor.webassembly.js",
            await index.Content.ReadAsStringAsync(ct),
            StringComparison.Ordinal
        );
        Assert.Equal(HttpStatusCode.OK, blazor.StatusCode);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new UriBuilder(url) { Scheme = "ws", Path = "/ws" }.Uri, ct);
        Assert.StartsWith(
            "{\"type\":\"snapshot\"",
            await FirstMessageAsync(socket, ct),
            StringComparison.Ordinal
        );
    }

    private static async Task<string> FirstMessageAsync(
        ClientWebSocket socket,
        CancellationToken ct
    )
    {
        using var message = new MemoryStream();
        var buffer = new byte[16 * 1024];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(message.ToArray());
    }
}
