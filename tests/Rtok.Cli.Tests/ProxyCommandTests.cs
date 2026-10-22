using System.Net;
using System.Net.Sockets;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary><c>rtok proxy</c> and <c>rtok wrap</c> end to end: the effective settings, the
/// proxy wrap starts, the base URLs the child sees, and the child's exit code.</summary>
public sealed class ProxyCommandTests
{
    [Fact]
    public async Task Wrap_starts_the_proxy_and_points_the_child_at_it()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "a POSIX shell body");
        using var dir = new TempDir("rtok-wrap-");
        var port = FreePort();
        var config = dir.Write("config.toml", $"[proxy]\nport = {port}\n");
        var release = dir.Combine("release");
        var root = $"http://127.0.0.1:{port}";

        var wrap = Task.Run(
            () =>
                RtokBinary.Exec(
                    [
                        "wrap",
                        "--config",
                        config,
                        "--",
                        "sh",
                        "-c",
                        $"echo \"$ANTHROPIC_BASE_URL $OPENAI_BASE_URL\"; while [ ! -f '{release}' ]; do sleep 0.1; done; exit 4",
                    ],
                    Unset()
                ),
            TestContext.Current.CancellationToken
        );
        var health = await HealthAsync(root);
        await File.WriteAllTextAsync(release, "", TestContext.Current.CancellationToken);
        var (code, stdout, stderr) = await wrap;

        Assert.Contains("\"recording\":true", health, StringComparison.Ordinal);
        Assert.Equal(4, code);
        Assert.Equal($"{root} {root}/v1", stdout.TrimEnd());
        Assert.DoesNotContain("running without it", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrap_runs_the_command_without_the_proxy_when_it_cannot_start()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "a POSIX shell body");
        using var dir = new TempDir("rtok-wrap-");
        using var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        var port = ((IPEndPoint)taken.LocalEndpoint).Port;
        var config = dir.Write("config.toml", $"[proxy]\nport = {port}\n");

        var (code, stdout, stderr) = RtokBinary.Exec(
            ["wrap", "--config", config, "--", "sh", "-c", "echo \"[$ANTHROPIC_BASE_URL]\""],
            Unset()
        );

        Assert.Equal(0, code);
        Assert.Equal("[]", stdout.TrimEnd());
        Assert.Contains("running without it", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrap_without_a_command_is_a_usage_error()
    {
        var (code, _, stderr) = RtokBinary.Exec(["wrap"]);

        Assert.Equal(2, code);
        Assert.Contains("missing command", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Proxy_dry_run_prints_the_settings_with_the_flags_applied()
    {
        var (code, stdout, stderr) = RtokBinary.Exec([
            "proxy",
            "--port",
            "9911",
            "--upstream",
            "https://gw.example/anthropic",
            "--mode",
            "compress",
            "--dry-run",
        ]);

        Assert.True(code == 0, stderr);
        Assert.Equal(
            """
            bind = 127.0.0.1
            port = 9911
            mode = compress
            upstream = https://gw.example/anthropic
            openai_upstream = https://api.openai.com
            timeout_s = 600
            include_usage = true
            dry_run = true

            """,
            stdout.ReplaceLineEndings("\n")
        );
    }

    private static Dictionary<string, string?> Unset() =>
        new(StringComparer.Ordinal) { ["ANTHROPIC_BASE_URL"] = null, ["OPENAI_BASE_URL"] = null };

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static async Task<string> HealthAsync(string root)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                return await client.GetStringAsync(
                    new Uri(root + "/health"),
                    TestContext.Current.CancellationToken
                );
            }
            catch (HttpRequestException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
        }
    }
}
