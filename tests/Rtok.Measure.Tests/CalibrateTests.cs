using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Rtok.Config;
using Rtok.Plugin.Sdk;
using Rtok.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using static Rtok.Measure.Tests.Lines;

namespace Rtok.Measure.Tests;

public sealed class CalibrateTests : IDisposable
{
    private readonly TempDir _logs = new();
    private readonly TempDir _home = new();
    private readonly HttpClient _http = new();

    public CalibrateTests()
    {
        var prose = string.Concat(
            Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 10)
        );
        var code = string.Concat(
            Enumerable.Repeat("fn main() { let x = vec![1, 2]; println!(\"{x:?}\"); }\n", 6)
        );
        var json =
            "[" + string.Join(',', Enumerable.Range(0, 40).Select(i => $$"""{"id":{{i}}}""")) + "]";
        Write(
            _logs,
            "s.jsonl",
            [
                Use("t1", "Read", Obj(("file_path", "README.md"))),
                Result("t1", prose),
                Use("t2", "Read", Obj(("file_path", "main.rs"))),
                Result("t2", code),
                Use("t3", "Bash", Obj(("command", "jq . ids.json"))),
                Result("t3", json),
                Use("t4", "Bash", Obj(("command", "echo hi"))),
                Result("t4", "hi"),
            ]
        );
    }

    public void Dispose()
    {
        _http.Dispose();
        _logs.Dispose();
        _home.Dispose();
    }

    private RtokConfig Config()
    {
        var c = new RtokConfig();
        c.Stats.TranscriptsDir = _logs.Path;
        return c;
    }

    private string UserFile => _home.Combine("config.toml");

    private Task<string> RunAsync(string? key, Uri endpoint) =>
        Calibrate.RunAsync(
            Config(),
            UserFile,
            key,
            _http,
            endpoint,
            DateTime.UtcNow,
            TestContext.Current.CancellationToken
        );

    /// <summary>A fake count_tokens: 7 tokens of envelope, JSON at 3 chars a token, the rest at 4.</summary>
    [Fact]
    public async Task Fits_chars_per_token_per_class_from_the_api()
    {
        using var api = WireMockServer.Start();
        api.Given(
                Request
                    .Create()
                    .WithPath("/v1/messages/count_tokens")
                    .UsingPost()
                    .WithHeader("x-api-key", "k")
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithBody(req =>
                    {
                        using var doc = JsonDocument.Parse(req.Body!);
                        var text = doc
                            .RootElement.GetProperty("messages")[0]
                            .GetProperty("content")
                            .GetString()!;
                        var per = text.StartsWith('[') ? 3 : 4;
                        return $$"""{"input_tokens":{{7 + (text.Length + per - 1) / per}}}""";
                    })
            );

        var printed = await RunAsync("k", new Uri(api.Url + "/v1/messages/count_tokens"));

        Assert.Contains("estimator.prose = 3.9", printed, StringComparison.Ordinal);
        Assert.Contains("estimator.json = 2.9", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("estimator.cjk", printed, StringComparison.Ordinal);
        var rates = ConfigLoader
            .Load(new ConfigLoadOptions { ConfigFile = UserFile })
            .Config.Estimator;
        Assert.Equal(4.0, rates.Prose, 1);
        Assert.Equal(4.0, rates.Code, 1);
        Assert.Equal(3.0, rates.Json, 1);
        Assert.Equal(1.0, rates.Cjk); // no CJK sample: left alone
    }

    [Fact]
    public async Task An_unreachable_api_leaves_the_rates_unchanged()
    {
        File.WriteAllText(UserFile, "[estimator]\ncode = 3.3\n");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var printed = await RunAsync(
            "k",
            new Uri($"http://127.0.0.1:{port}/v1/messages/count_tokens")
        );

        Assert.StartsWith(
            "calibrate failed, [estimator] unchanged",
            printed,
            StringComparison.Ordinal
        );
        Assert.Equal("[estimator]\ncode = 3.3\n", File.ReadAllText(UserFile));
    }

    [Fact]
    public async Task No_key_skips_without_a_request()
    {
        Assert.Equal("skipped: no ANTHROPIC_API_KEY\n", await RunAsync(null, Calibrate.Endpoint));
        Assert.False(File.Exists(UserFile));
    }

    [Theory]
    [InlineData("""{"a": [1, 2, 3], "b": "text"}""", TextClass.Json)]
    [InlineData("東京都渋谷区の天気は晴れです。明日も晴れでしょう。", TextClass.Cjk)]
    [InlineData("The quick brown fox jumps over the lazy dog, twice in a row.", TextClass.Prose)]
    [InlineData("if (x > 0) { y = f(x); } else { y = g[x]; }", TextClass.Code)]
    [InlineData("{ not json at all; x = 1 }", TextClass.Code)]
    public void Classifies_text(string text, TextClass cls) =>
        Assert.Equal(cls, Calibrate.Classify(text));
}
