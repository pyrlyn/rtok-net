using System.Text;
using BenchmarkDotNet.Attributes;
using Rtok.Config;
using Rtok.Proxy;
using Rtok.Store;
using Rtok.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rtok.Benchmarks;

// T27: one Anthropic Messages request (rtok's 40 KB fixture) answered with a short stream, sent
// straight to a WireMock upstream, through the proxy in plain mode, and through the recording
// proxy (call, call_io, usage and tokens rows in a real store).
[MemoryDiagnoser]
public class ProxyBenchmarks : IDisposable
{
    private readonly HttpClient _client = new();
    private WireMockServer _upstream = null!;
    private ProxyServer? _proxy;
    private RtokStore? _store;
    private TempDir? _dir;
    private Uri _target = null!;
    private byte[] _request = [];

    [Params("direct", "plain", "recording")]
    public string Route { get; set; } = "direct";

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _request = File.ReadAllBytes(RepoPaths.Fixture("proxy", "messages.json"));
        _upstream = WireMockServer.Start();
        _upstream
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithHeader("Content-Type", "text/event-stream")
                    .WithBody(
                        File.ReadAllText(
                            RepoPaths.Fixture("proxy", "anthropic_messages_stream.json"),
                            Encoding.UTF8
                        )
                    )
            );
        if (string.Equals(Route, "direct", StringComparison.Ordinal))
        {
            _target = new Uri(_upstream.Url + "/v1/messages");
            return;
        }
        var config = new RtokConfig();
        config.Proxy.Port = 0;
        config.Proxy.Upstream = _upstream.Url!;
        config.Proxy.Enabled = string.Equals(Route, "recording", StringComparison.Ordinal);
        _dir = new TempDir("rtok-bench-proxy-");
        _store = RtokStore.Open(
            new StoreOptions(_dir.Combine("rtok.db")) { ArchiveDir = _dir.Combine("archive") }
        );
        _proxy = await ProxyServer.StartAsync(
            new ProxyOptions(config) { Store = _store, CertFile = null }
        );
        _target = new Uri(_proxy.Address, "/v1/messages");
    }

    [Benchmark]
    public async Task<int> MessagesAsync()
    {
        using var content = new ByteArrayContent(_request);
        using var response = await _client.PostAsync(_target, content);
        return (await response.Content.ReadAsByteArrayAsync()).Length;
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (_proxy is not null)
        {
            await _proxy.DisposeAsync();
        }
        _store?.Dispose();
        _upstream.Stop();
        _upstream.Dispose();
        Dispose();
    }

    public void Dispose()
    {
        _client.Dispose();
        _dir?.Dispose();
        GC.SuppressFinalize(this);
    }
}
