using System.Net;
using CliWrap;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

public sealed class WebCommandTests
{
    [Fact]
    public async Task Rtok_web_binds_the_configured_host_and_prints_its_url_with_the_token()
    {
        using var dir = new TempDir();
        var config = dir.Write("config.toml", "[web]\nhost = \"localhost\"\nport = 0\n");

        var (url, stderr) = await ServeAsync(
            ["web", "--config", config],
            async url =>
            {
                using var http = new HttpClient();
                using var health = await http.GetAsync(
                    new Uri(url, "/health"),
                    TestContext.Current.CancellationToken
                );
                Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            }
        );

        Assert.Equal("127.0.0.1", url.Host);
        Assert.NotEqual(0, url.Port);
        Assert.Matches("^\\?token=[A-Za-z0-9_-]{43}$", url.Query);
        Assert.DoesNotContain("dashboard", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_is_a_hidden_alias_with_a_notice()
    {
        var (_, stderr) = await ServeAsync(["dashboard", "--port", "0"], _ => Task.CompletedTask);

        Assert.StartsWith("rtok dashboard is now rtok web\n", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain(
            RtokBinary.Model.Commands,
            c => string.Equals(c.Path, "dashboard", StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData("--host", "example.com", "must be an IP address or localhost")]
    [InlineData("--host", "0.0.0.0", "without the token")]
    public void A_refused_start_exits_1_with_the_reason(string option, string value, string reason)
    {
        string[] args = ["web", option, value, "--port", "0"];
        if (string.Equals(value, "0.0.0.0", StringComparison.Ordinal))
        {
            args = [.. args, "--no-token"];
        }

        var (code, _, stderr) = RtokBinary.Exec(args);

        Assert.Equal(1, code);
        Assert.Contains(reason, stderr, StringComparison.Ordinal);
    }

    // Starts rtok, waits for its "rtok web <url>" line, runs the check against the url, then
    // stops rtok with Ctrl-C and returns the url and all it printed to stderr.
    private static async Task<(Uri Url, string Stderr)> ServeAsync(
        string[] args,
        Func<Uri, Task> check
    )
    {
        var printed = new TaskCompletionSource<Uri>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var stderr = new System.Text.StringBuilder();
        using var interrupt = new CancellationTokenSource();
        using var kill = new CancellationTokenSource();
        var run = RtokBinary
            .Command(args)
            .WithStandardErrorPipe(
                PipeTarget.ToDelegate(line =>
                {
                    lock (stderr)
                    {
                        stderr.Append(line).Append('\n');
                    }
                    if (line.StartsWith("rtok web http", StringComparison.Ordinal))
                    {
                        printed.TrySetResult(new Uri(line["rtok web ".Length..]));
                    }
                })
            )
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(kill.Token, interrupt.Token);
        try
        {
            var url = await printed.Task.WaitAsync(
                TimeSpan.FromSeconds(30),
                TestContext.Current.CancellationToken
            );
            await check(url);
            await interrupt.CancelAsync();
            kill.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await run;
            }
            catch (OperationCanceledException) { }
            lock (stderr)
            {
                return (url, stderr.ToString());
            }
        }
        finally
        {
            await kill.CancelAsync();
        }
    }
}
