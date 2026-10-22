using System.IO.Compression;
using System.Text;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;
using Rtok.Store;
using Rtok.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using ZstdSharp;

namespace Rtok.Plugins.Docs.Tests;

/// <summary>A workspace, a store, a host and a fake docs.rs / nuget.org.</summary>
internal sealed class DocsHarness : IDisposable
{
    public const string CargoLockText = """
        version = 3

        [[package]]
        name = "serde"
        version = "1.0.210"
        source = "registry+https://github.com/rust-lang/crates.io-index"
        checksum = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        """;

    public const string NuGetLockText = """
        {
          "version": 1,
          "dependencies": {
            "net10.0": {
              "Newtonsoft.Json": {
                "type": "Direct",
                "requested": "[13.0.3, )",
                "resolved": "13.0.3",
                "contentHash": "x"
              }
            }
          }
        }
        """;

    // A docs.rs rustdoc JSON in miniature: two documented items, one without docs.
    public const string Rustdoc = """
        {
          "format_version": 39,
          "paths": {
            "1": { "path": ["serde", "Serialize"], "kind": "trait" },
            "2": { "path": ["serde", "Deserialize"], "kind": "trait" },
            "3": { "path": ["serde", "de", "Visitor"], "kind": { "trait": {} } },
            "4": { "path": ["serde", "Hidden"], "kind": "struct" }
          },
          "index": {
            "1": { "docs": "A data structure that can be serialized into any data format supported by Serde." },
            "2": { "docs": "A data structure that can be deserialized from any data format supported by Serde. Deserialize is the entry point." },
            "3": { "docs": "Walks a Deserialize input one value at a time." },
            "4": { "docs": "" }
          }
        }
        """;

    public const string XmlDocs = """
        <?xml version="1.0"?>
        <doc>
          <assembly><name>Newtonsoft.Json</name></assembly>
          <members>
            <member name="T:Newtonsoft.Json.JsonConvert">
              <summary>Provides methods for converting between .NET types and JSON types.</summary>
            </member>
            <member name="M:Newtonsoft.Json.JsonConvert.SerializeObject(System.Object)">
              <summary>Serializes the specified object to a JSON string.</summary>
              <param name="value">The object to serialize.</param>
              <returns>A JSON string representation of the <see cref="T:System.Object"/>.</returns>
            </member>
          </members>
        </doc>
        """;

    private readonly TempDir _dir = new("rtok-docs-");

    public DocsHarness(string configJson = "{}")
    {
        Workspace = _dir.Combine("ws");
        Directory.CreateDirectory(Workspace);
        Store = RtokStore.Open(new StoreOptions(_dir.Combine("home", "rtok.db")));
        Home = _dir.Combine("home");
        Host = new MemoryHost(configJson);
        Cx = new Ctx(Host);
        Service = new DocsService(Store, DocsPlugin.Config(Cx), Home);
        Upstream = WireMockServer.Start();
        Http = new HttpClient();
    }

    public string Workspace { get; }

    public string Home { get; }

    public RtokStore Store { get; }

    public MemoryHost Host { get; }

    public Ctx Cx { get; }

    public DocsService Service { get; }

    public WireMockServer Upstream { get; }

    public HttpClient Http { get; }

    public DocsUpstream Fake(TimeSpan? timeout = null) =>
        new()
        {
            DocsRs = new Uri(Upstream.Url + "/rs/"),
            NuGet = new Uri(Upstream.Url + "/nuget/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(10),
        };

    public string Write(string relative, string text) =>
        _dir.Write(Path.Join("ws", relative), text);

    public void Serve(string path, byte[] body, int status = 200, TimeSpan? delay = null)
    {
        var response = Response.Create().WithStatusCode(status).WithBody(body);
        if (delay is { } d)
        {
            response = response.WithDelay(d);
        }
        Upstream.Given(Request.Create().WithPath(path).UsingGet()).RespondWith(response);
    }

    public Task<string> FetchAsync(string name, DocsUpstream? upstream = null) =>
        Service.FetchAsync(
            name,
            Workspace,
            Http,
            upstream ?? Fake(),
            TestContext.Current.CancellationToken
        );

    public string Query(string name, string query, string? version = null) =>
        Service.Query(name, version, query, Workspace, Cx);

    public static byte[] Zstd(string text)
    {
        using var compressor = new Compressor();
        return compressor.Wrap(Encoding.UTF8.GetBytes(text)).ToArray();
    }

    public static byte[] Nupkg(string xml)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Entry(zip, "Newtonsoft.Json.nuspec", "<package/>");
            Entry(zip, "lib/netstandard1.0/Newtonsoft.Json.xml", "<doc><members/></doc>");
            Entry(zip, "lib/net6.0/Newtonsoft.Json.xml", xml);
        }
        return buffer.ToArray();
    }

    private static void Entry(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(text);
    }

    public void Dispose()
    {
        Http.Dispose();
        Upstream.Stop();
        Upstream.Dispose();
        Store.Dispose();
        _dir.Dispose();
    }
}
