using CsCheck;
using Rtok.Config;

namespace Rtok.Proxy.Tests;

public sealed class LaneTests
{
    [Theory]
    [InlineData("/v1/messages", Lane.Agent)]
    [InlineData("/v1/chat/completions", Lane.Agent)]
    [InlineData("/v1/responses", Lane.Agent)]
    [InlineData("/v1beta/models/gemini-2.0-flash:generateContent", Lane.Agent)]
    [InlineData("/v1/something/new", Lane.Agent)]
    [InlineData("/v1/messages/batches", Lane.Batch)]
    [InlineData("/v1/messages/batches/msgbatch_1/results", Lane.Batch)]
    [InlineData("/v1/batches/batch_1/cancel", Lane.Batch)]
    [InlineData("/v1beta/batches/b1", Lane.Batch)]
    [InlineData("/v1beta/models/gemini-2.0-flash:batchGenerateContent", Lane.Batch)]
    [InlineData("/v1beta/models/text-embedding-004:asyncBatchEmbedContent", Lane.Batch)]
    [InlineData("/v1/files", Lane.Files)]
    [InlineData("/v1/files/file_1/content", Lane.Files)]
    [InlineData("/v1beta/files/f", Lane.Files)]
    [InlineData("/upload/v1beta/files", Lane.Files)]
    [InlineData("/v1/embeddings", Lane.Embeddings)]
    [InlineData("/v1beta/models/text-embedding-004:embedContent", Lane.Embeddings)]
    [InlineData("/v1beta/models/text-embedding-004:batchEmbedContents", Lane.Embeddings)]
    [InlineData("/v1/models", Lane.Meta)]
    [InlineData("/v1/models/gpt-5", Lane.Meta)]
    [InlineData("/v1beta/models", Lane.Meta)]
    [InlineData("/v1/messages/count_tokens", Lane.Meta)]
    [InlineData("/v1beta/models/gemini-2.0-flash:countTokens", Lane.Meta)]
    [InlineData("/v1/filesystem", Lane.Agent)]
    public void Paths_name_their_lane(string path, Lane lane) =>
        Assert.Equal((lane, path), Lanes.Classify(path, null));

    [Fact]
    public void The_header_picks_among_what_a_path_cannot_say()
    {
        Assert.Equal(Lane.Bulk, Lanes.Classify("/v1/messages", "Bulk").Lane);
        Assert.Equal(Lane.Internal, Lanes.Classify("/v1/responses", " internal ").Lane);
        Assert.Equal(Lane.Batch, Lanes.Classify("/v1/messages/batches", "bulk").Lane);
        Assert.Equal(Lane.Agent, Lanes.Classify("/v1/messages", "turbo").Lane);
    }

    [Fact]
    public void The_prefix_is_stripped_and_beats_the_header()
    {
        Assert.Equal(
            (Lane.Bulk, "/v1/messages"),
            Lanes.Classify("/lane/bulk/v1/messages", "internal")
        );
        Assert.Equal((Lane.Files, "/v1/files/f1"), Lanes.Classify("/lane/BULK/v1/files/f1", null));
    }

    [Theory]
    [InlineData("/lane/turbo/v1/messages")]
    [InlineData("/lane/bulk")]
    [InlineData("/lane/")]
    [InlineData("/lanes/bulk/v1/messages")]
    public void Foreign_prefixes_are_forwarded_untouched(string path) =>
        Assert.Equal((Lane.Agent, path), Lanes.Classify(path, null));

    [Fact]
    public void Kinds_name_the_lane_and_stay_api_requests()
    {
        foreach (var lane in Lanes.All)
        {
            Assert.True(Lanes.IsApiRequest(lane.Kind()), lane.Name());
            Assert.Equal(lane.Name(), Lanes.NameOfKind(lane.Kind()));
            Assert.Equal(lane, Lanes.Parse(lane.Name()));
        }
        Assert.Equal("api_request", Lane.Agent.Kind());
        Assert.Equal("api_request:bulk", Lane.Bulk.Kind());
        Assert.False(Lanes.IsApiRequest("hook"));
        Assert.False(Lanes.IsApiRequest("api_requests"));
    }

    [Fact]
    public void Only_the_agent_lane_rewrites_by_default()
    {
        var config = Everything();
        foreach (var lane in Lanes.All)
        {
            var rules = LaneRules.For(lane, config);
            var all =
                rules.Compress
                && rules.Toon
                && rules.ToolsRewrite
                && rules.ContextManagement
                && rules.SemanticCache;
            Assert.True(all == (lane == Lane.Agent), lane.Name());
            Assert.False(rules.Flex);
            Assert.Equal(config.Proxy.TimeoutS, rules.TimeoutS);
        }
    }

    [Fact]
    public void Batch_and_files_ignore_the_config_and_agent_keeps_its_upstream_and_no_cap()
    {
        var open = new LanePolicy
        {
            Compress = true,
            ToolsRewrite = true,
            Upstream = "http://elsewhere",
            MaxInFlight = 1,
        };
        var lanes = new LanesSection
        {
            Bulk = open,
            Embeddings = open,
            Meta = open,
            Internal = open,
        };
        foreach (var lane in new[] { Lane.Agent, Lane.Batch, Lane.Files })
        {
            var policy = lane.Policy(lanes);
            Assert.Equal(("", 0u), (policy.Upstream, policy.MaxInFlight));
        }
        Assert.True(Lane.Batch.PassesThrough() && Lane.Files.PassesThrough());
        Assert.False(Lane.Bulk.PassesThrough());
        Assert.Same(open, Lane.Bulk.Policy(lanes));
    }

    [Fact]
    public void Only_bulk_and_internal_route_and_only_when_enabled()
    {
        var routing = new RoutingPolicy();
        Assert.All(Lanes.All, lane => Assert.Null(lane.Route(routing)));
        routing.Enabled = true;
        foreach (var lane in Lanes.All)
        {
            Assert.Equal(lane is Lane.Bulk or Lane.Internal, lane.Route(routing) is not null);
        }
    }

    [Fact]
    public void A_lane_policy_never_widens_the_global_one() =>
        Gen.Select(Gen.Bool.Array[6], Gen.Bool.Array[5], Gen.ULong[0, 2000], Gen.ULong[0, 2000])
            .Sample(t =>
            {
                var (global, lane, globalTimeout, laneTimeout) = t;
                var config = new RtokConfig();
                config.Proxy.Mode = global[0] ? "compress" : "passthrough";
                config.Plugins.Toon.Enabled = global[1];
                config.Proxy.ToolsRewrite.Enabled = global[2];
                config.Proxy.ContextManagement = global[3];
                config.Plugins.Proxy.SemanticCache.Enabled = global[4];
                config.Proxy.TimeoutS = globalTimeout;
                config.Proxy.Lanes.Bulk = new LanePolicy
                {
                    Compress = lane[0],
                    Toon = lane[1],
                    ToolsRewrite = lane[2],
                    ContextManagement = lane[3],
                    SemanticCache = lane[4],
                    TimeoutS = laneTimeout,
                };
                var rules = LaneRules.For(Lane.Bulk, config);
                return (!rules.Compress || global[0])
                    && (!rules.Toon || (global[0] && global[1]))
                    && (!rules.ToolsRewrite || global[2])
                    && (!rules.ContextManagement || global[3])
                    && (!rules.SemanticCache || global[4])
                    && rules.TimeoutS <= globalTimeout
                    && (laneTimeout == 0 || rules.TimeoutS <= laneTimeout);
            });

    [Theory]
    [InlineData(false, 0, 600, 600)]
    [InlineData(false, 30, 600, 30)]
    [InlineData(false, 900, 600, 600)]
    [InlineData(true, 0, 600, 900)]
    [InlineData(true, 0, 1200, 1200)]
    [InlineData(true, 1500, 600, 1500)]
    public void Timeouts(bool flex, ulong lane, ulong global, ulong expected) =>
        Assert.Equal(expected, LaneRules.Timeout(flex, lane, global));

    [Fact]
    public async Task The_gate_lets_in_then_queues_then_turns_away()
    {
        using var gate = LaneGate.For(new LanePolicy { MaxInFlight = 1, MaxQueued = 1 })!;
        Assert.Null(LaneGate.For(new LanePolicy { MaxInFlight = 0 }));
        var first = await gate.EnterAsync(CancellationToken.None);
        Assert.NotNull(first);
        var queued = gate.EnterAsync(CancellationToken.None);
        Assert.Equal(1, gate.Waiting);
        Assert.Null(await gate.EnterAsync(CancellationToken.None));
        first.Dispose();
        first.Dispose();
        using var second = await queued;
        Assert.NotNull(second);
        Assert.Equal(0, gate.Waiting);
    }

    [Fact]
    public async Task A_waiter_that_leaves_frees_its_place()
    {
        using var gate = LaneGate.For(new LanePolicy { MaxInFlight = 1, MaxQueued = 1 })!;
        using var held = await gate.EnterAsync(CancellationToken.None);
        using var gone = new CancellationTokenSource();
        var waiter = gate.EnterAsync(gone.Token);
        await gone.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        Assert.Equal(0, gate.Waiting);
    }

    private static RtokConfig Everything()
    {
        var config = new RtokConfig();
        config.Proxy.Mode = "compress";
        config.Proxy.ToolsRewrite.Enabled = true;
        config.Proxy.ContextManagement = true;
        config.Plugins.Proxy.SemanticCache.Enabled = true;
        return config;
    }
}
