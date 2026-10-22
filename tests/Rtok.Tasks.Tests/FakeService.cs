using System.Net;
using System.Net.Sockets;
using Rtok.Tasks.Remote;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace Rtok.Tasks.Tests;

/// <summary>A WireMock stand-in for GitHub or GitLab, with a context that finds a test token,
/// sends writes without a gap and keeps the warnings.</summary>
internal sealed class FakeService : IDisposable
{
    public WireMockServer Server { get; } = StartOnLoopback();

    public HttpClient Http { get; } = new();

    public List<string> Warnings { get; } = [];

    public Dictionary<string, string> Env { get; } =
        new(StringComparer.Ordinal)
        {
            ["GH_TOKEN"] = "test-token",
            ["GITLAB_TOKEN"] = "test-token",
        };

    public FixedClock Clock { get; } = new(1_800_000_000);

    public string Url => Server.Url!;

    public RemoteContext Context =>
        new(Http, n => Env.GetValueOrDefault(n), null, Clock, Warnings.Add)
        {
            WriteGap = TimeSpan.Zero,
            UserAgent = "rtok/test",
        };

    /// <summary>Answers <paramref name="method"/> <paramref name="path"/> (and the query, when
    /// given) with <paramref name="body"/>.</summary>
    public void On(
        string method,
        string path,
        string body,
        int status = 200,
        IReadOnlyDictionary<string, string>? query = null,
        string? bodyContains = null,
        IReadOnlyDictionary<string, string>? headers = null
    )
    {
        var request = Request.Create().WithPath(path).UsingMethod(method);
        foreach (var (k, v) in query ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            request = request.WithParam(k, v);
        }
        if (bodyContains is not null)
        {
            request = request.WithBody(b =>
                b?.Contains(bodyContains, StringComparison.Ordinal) == true
            );
        }
        var response = Response
            .Create()
            .WithStatusCode(status)
            .WithBody(body)
            .WithHeader("Content-Type", "application/json");
        foreach (var (k, v) in headers ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            response = response.WithHeader(k, v);
        }
        Server.Given(request).RespondWith(response);
    }

    /// <summary>Every request, in order.</summary>
    public IReadOnlyList<(string Method, string Path, string? Body)> Calls =>
        [
            .. Server.LogEntries.Select(e =>
                (e.RequestMessage!.Method, e.RequestMessage!.Path, e.RequestMessage!.Body)
            ),
        ];

    /// <summary>The bodies of every request that was not a GET, in order.</summary>
    public IReadOnlyList<string?> Writes =>
        [
            .. Calls
                .Where(c => !string.Equals(c.Method, "GET", StringComparison.Ordinal))
                .Select(c => c.Body),
        ];

    public void Dispose()
    {
        Http.Dispose();
        Server.Dispose();
    }

    /// <summary>
    /// Starts on 127.0.0.1 only. A free port is one bound and released; on <c>localhost</c>
    /// WireMock's Kestrel shrugs off a half-failed bind, so a port another test process took in
    /// between answered our requests with a dropped connection. On one address a taken port
    /// fails the start instead, and the next port is tried.
    /// </summary>
    private static WireMockServer StartOnLoopback()
    {
        for (var attempt = 1; ; attempt++)
        {
            var settings = new WireMockServerSettings { Urls = [$"http://127.0.0.1:{FreePort()}"] };
            try
            {
                return WireMockServer.Start(settings);
            }
            catch (Exception) when (attempt < 5)
            {
                // Taken between the probe and the bind: try another port.
            }
        }
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
