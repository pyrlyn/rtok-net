using System.IO.Compression;
using System.Net;
using Rtok.Testing;

namespace Rtok.Web.Tests;

public sealed class AssetTests
{
    private static readonly string Wasm = ClientFiles
        .Embedded()
        .List()
        .First(p =>
            p.StartsWith("_framework/", StringComparison.Ordinal)
            && p.EndsWith(".wasm.br", StringComparison.Ordinal)
        )[..^3];

    [Fact]
    public void The_embedded_framework_is_fingerprinted() =>
        Assert.True(AssetServer.Fingerprinted(Wasm), Wasm);

    [Theory]
    [InlineData("br", "br")]
    [InlineData("gzip", "gzip")]
    [InlineData("br;q=0, gzip", "gzip")]
    public async Task A_fingerprinted_asset_goes_out_precompressed_and_immutable(
        string accept,
        string encoding
    )
    {
        await using var web = await WebFixture.StartAsync();

        using var response = await GetAsync(web, Wasm, accept);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([encoding], response.Content.Headers.ContentEncoding);
        Assert.Equal("application/wasm", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "public, max-age=31536000, immutable",
            response.Headers.CacheControl?.ToString()
        );
        Assert.Contains("Accept-Encoding", response.Headers.Vary, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_client_that_takes_no_coding_gets_the_plain_bytes()
    {
        await using var web = await WebFixture.StartAsync();

        using var plain = await GetAsync(web, Wasm, accept: null);
        using var gzip = await GetAsync(web, Wasm, "gzip");

        Assert.Empty(plain.Content.Headers.ContentEncoding);
        var bytes = await plain.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        await using var unzipped = new GZipStream(
            await gzip.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            CompressionMode.Decompress
        );
        using var expected = new MemoryStream();
        await unzipped.CopyToAsync(expected, TestContext.Current.CancellationToken);
        Assert.Equal(expected.ToArray(), bytes);
        Assert.Equal("\0asm"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public async Task Index_html_is_never_cached_and_carries_the_csp()
    {
        await using var web = await WebFixture.StartAsync();

        using var response = await GetAsync(web, "", "br, gzip");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(AssetServer.Csp, Csp(response));
        Assert.Equal(
            ["nosniff"],
            response.Headers.GetValues("X-Content-Type-Options"),
            StringComparer.Ordinal
        );
    }

    [Theory]
    [InlineData("doctor")]
    [InlineData("graph/deep/route")]
    public async Task A_page_route_falls_back_to_index_html(string route)
    {
        await using var web = await WebFixture.StartAsync();

        using var index = await GetAsync(web, "", accept: null);
        using var response = await GetAsync(web, route, accept: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AssetServer.Csp, Csp(response));
        Assert.Equal(
            await index.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData("nope.js")]
    [InlineData("_framework/nope.wasm")]
    [InlineData("ws/deeper")]
    public async Task A_missing_file_is_404_not_the_index(string path)
    {
        await using var web = await WebFixture.StartAsync();

        using var response = await GetAsync(web, path, accept: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Other_methods_are_refused()
    {
        await using var web = await WebFixture.StartAsync();

        using var response = await web.Http.PostAsync(
            new Uri("index.html", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("/a%2Fb")]
    [InlineData("/%2e%2e/secret")]
    [InlineData("/_framework/%5Cx.js")]
    [InlineData("/%ZZ")]
    [InlineData("/%FF.js")]
    [InlineData("/a//b")]
    public async Task A_path_that_does_not_decode_to_a_file_name_gets_400(string target)
    {
        await using var web = await WebFixture.StartAsync();

        Assert.Equal(400, await web.RawStatusAsync(target));
    }

    [Fact]
    public async Task Rtok_web_dist_replaces_the_embedded_client()
    {
        using var dist = new TempDir();
        dist.Write("index.html", "<p>from dist</p>");
        dist.Write("app.js", "dist();");
        var (files, notice) = ClientFiles.Resolve(dist.Path);
        await using var web = await WebFixture.StartAsync(files: files);

        using var index = await GetAsync(web, "some/route", "br");
        using var js = await GetAsync(web, "app.js", "br");

        Assert.Null(notice);
        Assert.Equal(
            "<p>from dist</p>",
            await index.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.Empty(js.Content.Headers.ContentEncoding);
        Assert.Equal(
            "dist();",
            await js.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public void A_dist_with_no_index_html_falls_back_to_the_embedded_client_with_a_notice()
    {
        using var dist = new TempDir();

        var (files, notice) = ClientFiles.Resolve(dist.Path);

        Assert.Contains("index.html", notice, StringComparison.Ordinal);
        Assert.True(files.Exists("index.html.br"));
    }

    [Fact]
    public void Exactly_two_radzen_themes_are_embedded()
    {
        const string Css = "_content/Radzen.Blazor/css/";

        var themes = ClientFiles
            .Embedded()
            .List()
            .Where(p => p.StartsWith(Css, StringComparison.Ordinal))
            .Select(p => Path.GetFileNameWithoutExtension(p)) // drops .br and .gz
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            ["standard-base.css", "standard-dark-base.css"],
            themes,
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void The_csp_allows_no_style_or_script_source_beyond_its_own()
    {
        var directives = AssetServer
            .Csp.Split(';', StringSplitOptions.TrimEntries)
            .ToDictionary(d => d.Split(' ')[0], d => d.Split(' ')[1..], StringComparer.Ordinal);

        Assert.Equal(["'self'"], directives["style-src"]);
        Assert.Equal(["'unsafe-inline'"], directives["style-src-attr"]);
        Assert.Equal(["'self'", "'wasm-unsafe-eval'"], directives["script-src"]);
        Assert.Equal(["'self'"], directives["default-src"]);
        Assert.Equal(["'none'"], directives["frame-ancestors"]);
    }

    [Theory]
    [InlineData("gzip", "gzip", true)]
    [InlineData("br, gzip", "gzip", true)]
    [InlineData("gzip;q=0", "gzip", false)]
    [InlineData("gzip; q=0.000", "gzip", false)]
    [InlineData("GZIP;q=0.5", "gzip", true)]
    [InlineData("deflate", "gzip", false)]
    [InlineData("", "br", false)]
    public void Accept_encoding_honours_q_zero(string header, string coding, bool accepted) =>
        Assert.Equal(accepted, AssetServer.Accepts(header, coding));

    private static async Task<HttpResponseMessage> GetAsync(
        WebFixture web,
        string path,
        string? accept
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Encoding", accept);
        }
        return await web.Http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string Csp(HttpResponseMessage response) =>
        string.Join(' ', response.Headers.GetValues("Content-Security-Policy"));
}
