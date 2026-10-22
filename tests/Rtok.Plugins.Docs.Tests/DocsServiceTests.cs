using System.Text;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Docs.Tests;

public sealed class DocsServiceTests : IDisposable
{
    private const string SerdeZst = "/rs/crate/serde/1.0.210/json.zst";
    private readonly DocsHarness _h = new();

    public DocsServiceTests() => _h.Write("Cargo.lock", DocsHarness.CargoLockText);

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Fetch_ingests_the_pinned_version_and_resolve_reports_it_cached()
    {
        Assert.Equal(
            """{"name":"serde","version":"1.0.210","cached":false}""",
            _h.Service.Resolve("serde", _h.Workspace)
        );
        var zst = DocsHarness.Zstd(DocsHarness.Rustdoc);
        _h.Serve(SerdeZst, zst);

        Assert.Equal("cached serde 1.0.210 (3 items)", await _h.FetchAsync("serde"));
        Assert.Equal(
            """{"name":"serde","version":"1.0.210","cached":true}""",
            _h.Service.Resolve("serde", _h.Workspace)
        );
        var kept = Path.Join(_h.Home, "docs", "serde", "1.0.210", "rustdoc.json.zst");
        Assert.Equal(
            zst,
            await File.ReadAllBytesAsync(kept, TestContext.Current.CancellationToken)
        );
        Assert.Matches(
            "^[0-9a-f]{64}\n$",
            await File.ReadAllTextAsync(kept + ".sha256", TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Query_and_get_answer_from_the_ingested_docs()
    {
        await IngestAsync();
        var query = _h.Query("serde", "Deserialize");
        var get = _h.Service.Get(LocalDocsTests.Ids(query)[0]);

        await Verify($"{query}\n{get}");
    }

    [Fact]
    public async Task A_query_records_its_saving_against_the_full_bodies()
    {
        using var h = new DocsHarness("""{"plugins":{"docs":{"snippet_chars":12}}}""");
        h.Write("Cargo.lock", DocsHarness.CargoLockText);
        h.Serve(SerdeZst, DocsHarness.Zstd(DocsHarness.Rustdoc));
        await h.FetchAsync("serde");

        var reply = h.Query("serde", "Deserialize");

        var bodies = LocalDocsTests.Ids(reply).Select(id => h.Store.GetDocItem(id)!.Docs).ToList();
        Assert.Equal(2, bodies.Count);
        var row = Assert.Single(h.Host.Recorded());
        Assert.Equal(
            new Measurement
            {
                Plugin = "docs",
                Kind = "query",
                BeforeBytes = bodies.Sum(b => Encoding.UTF8.GetByteCount(b)),
                AfterBytes = Encoding.UTF8.GetByteCount(reply),
                EstBefore = h.Host.Estimate(string.Join('\n', bodies), TextClass.Prose),
                EstAfter = h.Host.Estimate(reply, TextClass.Prose),
                RefId = "serde@1.0.210",
            },
            row
        );
        Assert.True(row.EstAfter < row.EstBefore);
    }

    [Fact]
    public async Task The_reply_keeps_hits_only_while_it_fits_the_token_budget()
    {
        using var h = new DocsHarness("""{"plugins":{"docs":{"max_tokens":55}}}""");
        h.Write("Cargo.lock", DocsHarness.CargoLockText);
        h.Serve(SerdeZst, DocsHarness.Zstd(DocsHarness.Rustdoc));
        await h.FetchAsync("serde");

        var reply = h.Query("serde", "Deserialize");

        Assert.Single(LocalDocsTests.Ids(reply));
        Assert.True(h.Host.Estimate(reply, TextClass.Prose) <= 55);
    }

    [Fact]
    public async Task A_refetch_of_the_same_bytes_keeps_every_id()
    {
        await IngestAsync();
        var before = LocalDocsTests.Ids(_h.Query("serde", "data"));

        await _h.FetchAsync("serde");

        Assert.Equal(before, LocalDocsTests.Ids(_h.Query("serde", "data")));
    }

    [Fact]
    public async Task A_404_leaves_the_cached_docs_untouched()
    {
        await IngestAsync();
        var before = _h.Query("serde", "data");
        _h.Upstream.Reset();
        _h.Serve(SerdeZst, "nope"u8.ToArray(), status: 404);

        Assert.Equal("docs.rs returned 404", await _h.FetchAsync("serde"));
        Assert.Equal(before, _h.Query("serde", "data"));
    }

    [Fact]
    public async Task A_timeout_fails_and_leaves_the_cached_docs_untouched()
    {
        await IngestAsync();
        var before = _h.Query("serde", "data");
        _h.Upstream.Reset();
        _h.Serve(SerdeZst, DocsHarness.Zstd("{}"), delay: TimeSpan.FromSeconds(5));

        var e = await Assert.ThrowsAsync<DocsException>(() =>
            _h.FetchAsync("serde", _h.Fake(TimeSpan.FromMilliseconds(200)))
        );
        Assert.StartsWith("docs.rs did not answer within", e.Message, StringComparison.Ordinal);
        Assert.Equal(before, _h.Query("serde", "data"));
    }

    [Fact]
    public async Task An_archive_over_either_cap_leaves_the_cached_docs_untouched()
    {
        await IngestAsync();
        var before = _h.Query("serde", "data");
        var zst = DocsHarness.Zstd(new string(' ', 4096) + "{}");
        _h.Upstream.Reset();
        _h.Serve(SerdeZst, zst);

        Assert.Equal(
            "docs.rs sent more than 16 bytes",
            await _h.FetchAsync("serde", _h.Fake() with { MaxDownloadBytes = 16 })
        );
        var e = await Assert.ThrowsAsync<DocsException>(() =>
            _h.FetchAsync("serde", _h.Fake() with { MaxUnpackedBytes = 1024 })
        );
        Assert.Equal("docs.rs rustdoc JSON unpacks to more than 1024 bytes", e.Message);
        Assert.Equal(before, _h.Query("serde", "data"));
    }

    [Fact]
    public async Task A_nuget_package_is_fetched_from_its_nupkg_xml_docs()
    {
        _h.Write("src/App/packages.lock.json", DocsHarness.NuGetLockText);
        _h.Serve(
            "/nuget/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg",
            DocsHarness.Nupkg(DocsHarness.XmlDocs)
        );

        Assert.Equal(
            "cached Newtonsoft.Json 13.0.3 (2 items)",
            await _h.FetchAsync("newtonsoft.json")
        );
        var reply = _h.Query("Newtonsoft.Json", "serializes");
        Assert.Equal(
            ["Newtonsoft.Json.JsonConvert.SerializeObject(System.Object)"],
            LocalDocsTests.Paths(reply)
        );
        Assert.Equal(
            """{"id":2,"path":"Newtonsoft.Json.JsonConvert.SerializeObject(System.Object)","kind":"method","docs":"Serializes the specified object to a JSON string.\nparam value: The object to serialize.\nreturns: A JSON string representation of the System.Object."}""",
            _h.Service.Get(2)
        );
        Assert.Equal(reply, _h.Query("newtonsoft.json", "serializes", "13.0.3"));
    }

    [Fact]
    public async Task Misses_say_what_to_do()
    {
        Assert.Equal("not in a lockfile: tokio", _h.Service.Resolve("tokio", _h.Workspace));
        Assert.Equal("not in a lockfile: tokio", await _h.FetchAsync("tokio"));
        Assert.Equal("not cached: rtok docs fetch serde", _h.Query("serde", "data"));
        Assert.Equal("not cached: rtok docs fetch serde", _h.Query("serde", "data", "9.9.9"));
        Assert.Equal(
            "unknown doc id: 99",
            Assert.Throws<DocsException>(() => _h.Service.Get(99)).Message
        );
        Assert.Empty(_h.Host.Recorded());
    }

    private async Task IngestAsync()
    {
        _h.Serve(SerdeZst, DocsHarness.Zstd(DocsHarness.Rustdoc));
        Assert.Equal("cached serde 1.0.210 (3 items)", await _h.FetchAsync("serde"));
    }
}
