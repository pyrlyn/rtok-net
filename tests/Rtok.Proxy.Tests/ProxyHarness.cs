using Microsoft.EntityFrameworkCore;
using Rtok.Config;
using Rtok.Store;
using Rtok.Store.Model;
using Rtok.Testing;
using WireMock.Server;

namespace Rtok.Proxy.Tests;

/// <summary>A proxy on a free loopback port in front of a WireMock upstream, with its own store.</summary>
internal sealed class ProxyHarness : IAsyncDisposable
{
    private readonly TempDir _dir = new("rtok-proxy-");

    private ProxyHarness(RtokConfig config)
    {
        Config = config;
        StoreOptions = new StoreOptions(_dir.Combine("data", "rtok.db"))
        {
            ArchiveDir = _dir.Combine("data", "archive"),
        };
    }

    public WireMockServer Upstream { get; } = WireMockServer.Start();

    public RtokConfig Config { get; }

    public StoreOptions StoreOptions { get; }

    public RtokStore? Store { get; private set; }

    public ProxyServer Server { get; private set; } = null!;

    public HttpClient Client { get; } = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Starts a recording proxy; <paramref name="configure"/> edits the config first.</summary>
    public static async Task<ProxyHarness> StartAsync(
        Action<RtokConfig>? configure = null,
        Func<ProxyOptions, ProxyOptions>? options = null
    )
    {
        var config = new RtokConfig();
        config.Proxy.Port = 0;
        var harness = new ProxyHarness(config);
        config.Proxy.Upstream = harness.Upstream.Url!;
        config.Proxy.OpenaiUpstream = harness.Upstream.Url!;
        config.Proxy.GeminiUpstream = harness.Upstream.Url!;
        configure?.Invoke(config);
        harness.Store = RtokStore.Open(harness.StoreOptions);
        var start = new ProxyOptions(config) { Store = harness.Store, CertFile = null };
        harness.Server = await ProxyServer.StartAsync(options?.Invoke(start) ?? start);
        return harness;
    }

    public Uri Url(string pathAndQuery) => new(Server.Address, pathAndQuery);

    /// <summary>The store's rows, read once <paramref name="ready"/> holds: the proxy records
    /// after the client already has the response.</summary>
    public async Task<T> RowsAsync<T>(Func<RtokDbContext, T> read, Func<T, bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            await using var db = new RtokDbContext(
                new DbContextOptionsBuilder<RtokDbContext>()
                    .UseSqlite($"Data Source={StoreOptions.DbPath};Pooling=False")
                    .Options
            );
            var rows = read(db);
            if (ready(rows) || DateTime.UtcNow > deadline)
            {
                return rows;
            }
            await Task.Delay(20);
        }
    }

    public Task<List<Rtok.Store.Model.Usage>> UsageAsync(int expected) =>
        RowsAsync(
            db => db.Usage.AsNoTracking().OrderBy(u => u.Id).ToList(),
            r => r.Count >= expected
        );

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Server.DisposeAsync();
        Store?.Dispose();
        Upstream.Stop();
        Upstream.Dispose();
        _dir.Dispose();
    }
}
