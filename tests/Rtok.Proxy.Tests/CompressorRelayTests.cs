using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Rtok.Proxy.Tests;

public sealed class CompressorRelayTests
{
    private const string AgentBody = """{"model":"m-1","max_tokens":1,"messages":[]}""";
    private const string Answer = """{"usage":{"input_tokens":1,"output_tokens":1}}""";

    /// <summary>A build log: a few must-keep spans and enough prose that a summary is shorter.</summary>
    private static readonly string Original =
        "error[E0308]: mismatched types\n  --> src/lib.rs:42:7\n"
        + string.Concat(Enumerable.Repeat("   Compiling one more crate of the workspace\n", 40))
        + "41 passed; 2 failed\n";

    private static readonly string Kept = string.Join(
        "; ",
        MustKeep.Spans(Original).Select(s => s.Text)
    );

    private static readonly CompressAuth Auth = CompressAuth.Of([new("x-api-key", "k-test")])!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_summary_runs_on_the_internal_lane_off_the_agent_path_and_the_first_one_wins()
    {
        await using var h = await StartAsync();
        h.Config.Plugins.Compress.Llm.Url = h.Server.Address.ToString();
        using var queue = new SummaryQueue(Create(h), 2);
        var version = CompressPrompt.Version(h.Config.Plugins.Compress.Llm.Model);
        var id = h.Store!.PutArchive("s1", Encoding.UTF8.GetBytes(Original));
        Summarises(h, Kept, delay: TimeSpan.FromSeconds(2));
        h.Upstream.Given(Request.Create().UsingPost())
            .AtPriority(10)
            .RespondWith(Response.Create().WithBody(Answer));

        var clock = Stopwatch.StartNew();
        queue.Submit(Auth, [id]);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), "submit returns at once");
        using var agent = await PostAsync(h);

        Assert.Equal(HttpStatusCode.OK, agent.StatusCode);
        Assert.Null(h.Store.GetArchiveSummary(id, version));
        var stored = await SummaryAsync(h, id, version);
        Assert.Equal(Kept, stored);
        var call = h
            .Upstream.LogEntries.Select(e => e.RequestMessage!)
            .Single(r => r.Body!.Contains("<tool_result>", StringComparison.Ordinal));
        Assert.Equal("k-test", call.Headers!["x-api-key"].Single());
        Assert.Equal("2023-06-01", call.Headers!["anthropic-version"].Single());
        Assert.False(call.Headers!.ContainsKey(Lanes.Header));
        var body = JsonNode.Parse(call.Body!)!;
        Assert.Equal(h.Config.Plugins.Compress.Llm.Model, (string?)body["model"]);
        Assert.Equal(CompressPrompt.System, (string?)body["system"]);
        var kinds = await h.RowsAsync(
            db => db.Calls.AsNoTracking().Select(c => c.Kind).ToList(),
            k => k.Count >= 2
        );
        Assert.Contains(
            kinds,
            k => string.Equals(k, "api_request:internal", StringComparison.Ordinal)
        );

        using var again = Create(h);
        Assert.Equal(SummaryOutcome.Cached, (await again.RunAsync(Auth, id, Ct)).Outcome);
        Assert.False(h.Store.PutArchiveSummary(id, version, "a later summary"));
        Assert.Equal(Kept, h.Store.GetArchiveSummary(id, version));
    }

    [Fact]
    public async Task A_summary_that_drops_a_span_is_warned_and_not_stored()
    {
        var log = new LogCapture();
        await using var h = await StartAsync();
        h.Config.Plugins.Compress.Llm.Url = h.Upstream.Url!;
        using var compressor = Compressor.Create(h.Config, h.Store!, log.Logger)!;
        var id = h.Store!.PutArchive("s1", Encoding.UTF8.GetBytes(Original));
        Summarises(h, Kept.Replace("2 failed", "some failed", StringComparison.Ordinal));

        var result = await compressor.RunAsync(Auth, id, Ct);

        Assert.Equal(SummaryOutcome.Rejected, result.Outcome);
        Assert.Contains("2 failed", result.Detail, StringComparison.Ordinal);
        Assert.Null(h.Store.GetArchiveSummary(id, compressor.Version));
        Assert.Contains(
            log.Events,
            e =>
                e.Level == LogEventLevel.Warning
                && e.RenderMessage().Contains("summary dropped", StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData(500, "upstream status 500")]
    [InlineData(200, "max_output_tokens")]
    public async Task A_failed_call_stores_nothing(int status, string detail)
    {
        await using var h = await StartAsync();
        h.Config.Plugins.Compress.Llm.Url = h.Upstream.Url!;
        using var compressor = Create(h);
        var id = h.Store!.PutArchive("s1", Encoding.UTF8.GetBytes(Original));
        h.Upstream.Given(Request.Create().UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(status)
                    .WithBody(Reply(Kept, stopReason: "max_tokens"))
            );

        var result = await compressor.RunAsync(Auth, id, Ct);

        Assert.Equal(SummaryOutcome.Failed, result.Outcome);
        Assert.Contains(detail, result.Detail, StringComparison.Ordinal);
        Assert.Null(h.Store.GetArchiveSummary(id, compressor.Version));
    }

    [Theory]
    [InlineData("missing", "archive payload missing", 0)]
    [InlineData("binary", "not text", 0)]
    [InlineData("large", "too large", 0)]
    [InlineData("code", "diff or source", 0)]
    [InlineData("spans", "too many must-keep spans", 0)]
    [InlineData("longer", "summary not shorter than the original", 1)]
    public async Task What_is_not_worth_a_summary_keeps_the_extractive_view(
        string input,
        string reason,
        int calls
    )
    {
        await using var h = await StartAsync(c =>
        {
            c.Plugins.Compress.Llm.MaxInputBytes = string.Equals(
                input,
                "large",
                StringComparison.Ordinal
            )
                ? 16UL
                : 65536UL;
            c.Plugins.Compress.Llm.MaxSpans = string.Equals(
                input,
                "spans",
                StringComparison.Ordinal
            )
                ? 2U
                : 64U;
        });
        h.Config.Plugins.Compress.Llm.Url = h.Upstream.Url!;
        using var compressor = Create(h);
        byte[] bytes = input switch
        {
            "binary" => [0xff, 0xfe, 0x00],
            "code" => Encoding.UTF8.GetBytes("fn a() {\n    let x = 1;\n    x;\n}\n"),
            _ => Encoding.UTF8.GetBytes(Original),
        };
        var id = string.Equals(input, "missing", StringComparison.Ordinal)
            ? new string('a', 64)
            : h.Store!.PutArchive("s1", bytes);
        Summarises(h, Original + " and more");

        var result = await compressor.RunAsync(Auth, id, Ct);

        Assert.Equal(new SummaryResult(SummaryOutcome.Skipped, reason), result);
        Assert.Equal(calls, h.Upstream.LogEntries.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_compressor_flag_never_changes_the_agent_request(bool enabled)
    {
        await using var h = await StartAsync(c =>
        {
            c.Proxy.Mode = "compress";
            c.Plugins.Compress.Llm.Enabled = enabled;
        });
        h.Upstream.Given(Request.Create().UsingPost())
            .RespondWith(Response.Create().WithBody(Answer));

        using var response = await PostAsync(h);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AgentBody, Assert.Single(h.Upstream.LogEntries).RequestMessage!.Body);
    }

    private static Task<ProxyHarness> StartAsync(
        Action<Rtok.Config.RtokConfig>? configure = null
    ) =>
        ProxyHarness.StartAsync(c =>
        {
            c.Plugins.Compress.Llm.Enabled = true;
            configure?.Invoke(c);
        });

    private static Compressor Create(ProxyHarness h) =>
        Compressor.Create(h.Config, h.Store!, Serilog.Core.Logger.None)!;

    private static void Summarises(ProxyHarness h, string summary, TimeSpan? delay = null)
    {
        var response = Response.Create().WithBody(Reply(summary));
        h.Upstream.Given(
                Request.Create().UsingPost().WithBody(new WildcardMatcher("*<tool_result>*"))
            )
            .AtPriority(1)
            .RespondWith(delay is { } d ? response.WithDelay(d) : response);
    }

    private static string Reply(string summary, string stopReason = "end_turn") =>
        new JsonObject
        {
            ["content"] = new JsonArray(
                new JsonObject { ["type"] = "text", ["text"] = $" {summary} " }
            ),
            ["stop_reason"] = stopReason,
            ["usage"] = new JsonObject { ["input_tokens"] = 1, ["output_tokens"] = 1 },
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

    private static async Task<HttpResponseMessage> PostAsync(ProxyHarness h)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, h.Url("/v1/messages"))
        {
            Content = new StringContent(AgentBody, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        return await h.Client.SendAsync(request, Ct);
    }

    private static async Task<string?> SummaryAsync(ProxyHarness h, string id, string version)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (h.Store!.GetArchiveSummary(id, version) is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, Ct);
        }
        return h.Store.GetArchiveSummary(id, version);
    }
}
