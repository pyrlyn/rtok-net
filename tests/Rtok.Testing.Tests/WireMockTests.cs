using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rtok.Testing.Tests;

// WireMock.Net sample: a fake upstream on a free local port, the shape proxy tests use.
public sealed class WireMockTests
{
    [Fact]
    public async Task A_stubbed_upstream_answers()
    {
        using var upstream = WireMockServer.Start();
        upstream
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"ok":true}"""));
        using var http = new HttpClient();

        using var response = await http.PostAsync(
            new Uri($"{upstream.Url}/v1/messages"),
            new StringContent("{}"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"ok":true}""",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
    }
}
