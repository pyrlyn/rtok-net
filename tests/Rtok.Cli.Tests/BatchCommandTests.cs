using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Rtok.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Rtok.Cli.Tests;

public sealed class BatchCommandTests : IDisposable
{
    private const string AnthropicLine = """
        {"custom_id":"a","params":{"model":"m","max_tokens":1,"messages":[]}}
        """;

    private const string OpenaiLine = """
        {"custom_id":"a","method":"POST","url":"/v1/chat/completions","body":{}}
        """;

    private readonly TempDir _dir = new("rtok-batch-");
    private readonly WireMockServer _proxy = WireMockServer.Start();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _proxy.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public async Task Anthropic_submit_status_and_fetch()
    {
        _proxy
            .Given(Request.Create().WithPath("/v1/messages/batches").UsingPost())
            .RespondWith(Response.Create().WithBody("""{"id":"msgbatch_1"}"""));
        _proxy
            .Given(Request.Create().WithPath("/v1/messages/batches/msgbatch_1").UsingGet())
            .RespondWith(Response.Create().WithBody("""{"processing_status":"ended"}"""));
        _proxy
            .Given(Request.Create().WithPath("/v1/messages/batches/msgbatch_1/results").UsingGet())
            .RespondWith(Response.Create().WithBody("line1\nline2\n"));
        using var api = Api(BatchProvider.Anthropic);
        var file = _dir.Write("in.jsonl", $"{AnthropicLine}\n\n{AnthropicLine}\n");
        var output = _dir.Combine("out.jsonl");

        Assert.Equal("""{"id":"msgbatch_1"}""", await api.SubmitAsync(file, Ct));
        Assert.Equal("""{"processing_status":"ended"}""", await api.StatusAsync("msgbatch_1", Ct));
        Assert.Equal(12, await api.FetchAsync("msgbatch_1", output, Ct));

        Assert.Equal("line1\nline2\n", await File.ReadAllTextAsync(output, Ct));
        var submit = _proxy.LogEntries[0].RequestMessage!;
        Assert.Equal($$"""{"requests":[{{AnthropicLine}},{{AnthropicLine}}]}""", submit.Body);
        Assert.Equal("k", submit.Headers!["x-api-key"].Single());
        Assert.Equal("2023-06-01", submit.Headers!["anthropic-version"].Single());
    }

    [Fact]
    public async Task Openai_uploads_the_file_then_creates_the_batch_and_fetches_its_output()
    {
        _proxy
            .Given(Request.Create().WithPath("/v1/files").UsingPost())
            .RespondWith(Response.Create().WithBody("""{"id":"file-in"}"""));
        _proxy
            .Given(Request.Create().WithPath("/v1/batches").UsingPost())
            .RespondWith(Response.Create().WithBody("""{"id":"batch_1"}"""));
        _proxy
            .Given(Request.Create().WithPath("/v1/batches/batch_1").UsingGet())
            .RespondWith(
                Response.Create().WithBody("""{"status":"completed","output_file_id":"file-out"}""")
            );
        _proxy
            .Given(Request.Create().WithPath("/v1/files/file-out/content").UsingGet())
            .RespondWith(Response.Create().WithBody("results"));
        using var api = Api(BatchProvider.Openai);
        var file = _dir.Write("in.jsonl", OpenaiLine);

        Assert.Equal("""{"id":"batch_1"}""", await api.SubmitAsync(file, Ct));
        Assert.Equal(7, await api.FetchAsync("batch_1", _dir.Combine("out.jsonl"), Ct));

        var requests = _proxy.LogEntries.Select(e => e.RequestMessage!).ToList();
        Assert.Equal(
            ["/v1/files", "/v1/batches", "/v1/batches/batch_1", "/v1/files/file-out/content"],
            requests.Select(r => r.Path),
            StringComparer.Ordinal
        );
        Assert.All(requests, r => Assert.Equal("Bearer k", r.Headers!["Authorization"].Single()));
        Assert.StartsWith(
            $"multipart/form-data; boundary={BatchApi.Boundary}",
            requests[0].Headers!["Content-Type"].Single(),
            StringComparison.Ordinal
        );
        Assert.Contains(OpenaiLine, requests[0].Body, StringComparison.Ordinal);
        using var create = JsonDocument.Parse(requests[1].Body!);
        Assert.Equal("file-in", create.RootElement.GetProperty("input_file_id").GetString());
        Assert.Equal(
            "/v1/chat/completions",
            create.RootElement.GetProperty("endpoint").GetString()
        );
        Assert.Equal("24h", create.RootElement.GetProperty("completion_window").GetString());
    }

    [Fact]
    public async Task Openai_fetch_before_the_output_exists_names_the_state()
    {
        _proxy
            .Given(Request.Create().WithPath("/v1/batches/batch_1").UsingGet())
            .RespondWith(Response.Create().WithBody("""{"status":"in_progress"}"""));
        using var api = Api(BatchProvider.Openai);
        var output = _dir.Combine("out.jsonl");

        var e = await Assert.ThrowsAsync<BatchException>(() =>
            api.FetchAsync("batch_1", output, Ct)
        );

        Assert.Equal("batch batch_1 has no output file yet (status: in_progress)", e.Message);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task An_existing_output_is_refused_before_any_request()
    {
        using var api = Api(BatchProvider.Anthropic);
        var output = _dir.Write("out.jsonl", "keep");

        await Assert.ThrowsAsync<BatchException>(() => api.FetchAsync("msgbatch_1", output, Ct));

        Assert.Equal("keep", await File.ReadAllTextAsync(output, Ct));
        Assert.Empty(_proxy.LogEntries);
    }

    [Fact]
    public async Task A_provider_error_is_reported_with_its_status_and_a_short_body()
    {
        _proxy
            .Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(404).WithBody(new string('x', 1000)));
        using var api = Api(BatchProvider.Anthropic);

        var e = await Assert.ThrowsAsync<BatchException>(() => api.StatusAsync("msgbatch_1", Ct));

        Assert.StartsWith("404 Not Found: xxx", e.Message, StringComparison.Ordinal);
        Assert.True(e.Message.Length < 330);
    }

    [Fact]
    public void Validation_names_the_line_and_the_missing_key()
    {
        Assert.Equal(
            2,
            BatchApi
                .Validate($"{AnthropicLine}\n\n{AnthropicLine}\n", BatchProvider.Anthropic)
                .Lines.Count
        );
        Assert.Equal(
            "line 2: missing `params`",
            Assert
                .Throws<BatchException>(() =>
                    BatchApi.Validate(
                        $"{AnthropicLine}\n{{\"custom_id\":\"b\"}}",
                        BatchProvider.Anthropic
                    )
                )
                .Message
        );
        Assert.Contains(
            "line 1",
            Assert
                .Throws<BatchException>(() => BatchApi.Validate("nope", BatchProvider.Anthropic))
                .Message,
            StringComparison.Ordinal
        );
        Assert.Throws<BatchException>(() => BatchApi.Validate("\n \n", BatchProvider.Openai));
    }

    [Fact]
    public void An_openai_batch_targets_one_endpoint()
    {
        Assert.Equal(
            "/v1/chat/completions",
            BatchApi.Validate(OpenaiLine, BatchProvider.Openai).Url
        );
        var other = OpenaiLine.Replace("chat/completions", "responses", StringComparison.Ordinal);
        Assert.Throws<BatchException>(() =>
            BatchApi.Validate($"{OpenaiLine}\n{other}", BatchProvider.Openai)
        );
    }

    [Fact]
    public void Ids_cannot_escape_the_url_path_and_name_their_provider()
    {
        BatchApi.CheckId("msgbatch_01-AB");
        foreach (var bad in new[] { "", "a/b", "../x", "a?b", "a b" })
        {
            Assert.Throws<BatchException>(() => BatchApi.CheckId(bad));
        }
        Assert.Equal(BatchProvider.Anthropic, BatchCommands.ProviderOf(null, "msgbatch_1"));
        Assert.Equal(BatchProvider.Openai, BatchCommands.ProviderOf(null, "batch_1"));
        Assert.Equal(BatchProvider.Anthropic, BatchCommands.ProviderOf(null, null));
        Assert.Equal(BatchProvider.Openai, BatchCommands.ProviderOf("OpenAI", "msgbatch_1"));
        Assert.Throws<ValidationException>(() => BatchCommands.ProviderOf("gemini", null));
    }

    [Fact]
    public void A_missing_key_or_bad_url_is_refused_before_any_request()
    {
        Assert.Throws<BatchException>(() =>
            new BatchApi("http://127.0.0.1:1", BatchProvider.Openai, " ")
        );
        Assert.Throws<BatchException>(() => new BatchApi("ftp://x", BatchProvider.Openai, "k"));
    }

    private BatchApi Api(BatchProvider provider) => new(_proxy.Url! + "/", provider, " k ");
}
