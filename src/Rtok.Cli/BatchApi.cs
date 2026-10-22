using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Rtok.Cli;

/// <summary>A thin client for the providers' own Batch APIs that sends every call through
/// <c>rtok proxy</c>, so its batch lane sees them (rtok T385.12.1).</summary>
/// <remarks>It never turns a sync request into a batch job and never rewrites a JSONL line: the
/// Anthropic body is the caller's lines joined into <c>requests</c>, the OpenAI file is uploaded
/// as it is. Wire shapes: platform.claude.com batch-processing and developers.openai.com batch
/// guides, as rtok checked them on 2026-10-08.</remarks>
internal sealed class BatchApi : IDisposable
{
    /// <summary>The OpenAI upload's multipart boundary; a file that holds it is refused.</summary>
    internal const string Boundary = "rtok-batch-7f3a9c1e";

    /// <summary>How much of an error body is echoed: a provider error is short, a proxy HTML
    /// page is not.</summary>
    private const int ErrorSnippet = 300;

    private readonly HttpClient _client;
    private readonly string _base;
    private readonly BatchProvider _provider;
    private readonly string _key;

    public BatchApi(
        string baseUrl,
        BatchProvider provider,
        string? key,
        HttpMessageHandler? handler = null
    )
    {
        if (
            !baseUrl.StartsWith("http://", StringComparison.Ordinal)
            && !baseUrl.StartsWith("https://", StringComparison.Ordinal)
        )
        {
            throw new BatchException($"proxy url must start with http:// or https://: {baseUrl}");
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BatchException($"{KeyEnv(provider)} is not set");
        }
        _client = handler is null ? new HttpClient() : new HttpClient(handler);
        _base = baseUrl.TrimEnd('/');
        _provider = provider;
        _key = key.Trim();
    }

    /// <summary>The environment variable that holds <paramref name="provider"/>'s API key.</summary>
    public static string KeyEnv(BatchProvider provider) =>
        provider == BatchProvider.Anthropic ? "ANTHROPIC_API_KEY" : "OPENAI_API_KEY";

    /// <summary>The provider that issued <paramref name="id"/>: Message Batch ids start with
    /// <c>msgbatch_</c>, OpenAI's with <c>batch_</c>.</summary>
    public static BatchProvider OfId(string id) =>
        id.StartsWith("msgbatch_", StringComparison.Ordinal)
            ? BatchProvider.Anthropic
            : BatchProvider.Openai;

    /// <summary>Creates a batch from the JSONL at <paramref name="file"/>; returns the provider's
    /// batch object.</summary>
    public async Task<string> SubmitAsync(string file, CancellationToken ct)
    {
        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new BatchException($"read {file}: {e.Message}");
        }
        var (lines, url) = Validate(raw, _provider);
        if (_provider == BatchProvider.Anthropic)
        {
            var body = $"{{\"requests\":[{string.Join(',', lines)}]}}";
            return await TextAsync(JsonPost("/v1/messages/batches", body), ct)
                .ConfigureAwait(false);
        }
        if (raw.Contains(Boundary, StringComparison.Ordinal))
        {
            throw new BatchException($"{file} contains the multipart boundary {Boundary}");
        }
        var form =
            $"--{Boundary}\r\nContent-Disposition: form-data; name=\"purpose\"\r\n\r\nbatch\r\n"
            + $"--{Boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"batch.jsonl\"\r\n"
            + $"Content-Type: application/jsonl\r\n\r\n{raw}\r\n--{Boundary}--\r\n";
        var upload = Request(HttpMethod.Post, "/v1/files");
        upload.Content = new StringContent(form, Encoding.UTF8);
        upload.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
            $"multipart/form-data; boundary={Boundary}"
        );
        var fileId =
            Field(await TextAsync(upload, ct).ConfigureAwait(false), "id")
            ?? throw new BatchException("the upload answered without a file id");
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("input_file_id", fileId);
            json.WriteString("endpoint", url);
            json.WriteString("completion_window", "24h");
            json.WriteEndObject();
        }
        return await TextAsync(
                JsonPost("/v1/batches", Encoding.UTF8.GetString(buffer.ToArray())),
                ct
            )
            .ConfigureAwait(false);
    }

    /// <summary>The provider's current state of batch <paramref name="id"/>.</summary>
    public Task<string> StatusAsync(string id, CancellationToken ct)
    {
        CheckId(id);
        return TextAsync(Request(HttpMethod.Get, BatchPath(id)), ct);
    }

    /// <summary>Writes the results of a finished batch to <paramref name="output"/>, which must
    /// not exist yet; returns the bytes written.</summary>
    public async Task<long> FetchAsync(string id, string output, CancellationToken ct)
    {
        CheckId(id);
        // Before the first request: a refused target should not cost a download.
        if (File.Exists(output) || Directory.Exists(output))
        {
            throw new BatchException($"{output} exists; results are never overwritten");
        }
        string path;
        if (_provider == BatchProvider.Anthropic)
        {
            path = BatchPath(id) + "/results";
        }
        else
        {
            var batch = await StatusAsync(id, ct).ConfigureAwait(false);
            var file =
                Field(batch, "output_file_id")
                ?? throw new BatchException(
                    $"batch {id} has no output file yet (status: {Field(batch, "status") ?? "unknown"})"
                );
            CheckId(file);
            path = $"/v1/files/{file}/content";
        }
        using var response = await SendAsync(Request(HttpMethod.Get, path), ct)
            .ConfigureAwait(false);
        FileStream sink;
        try
        {
            sink = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
        }
        catch (IOException e)
        {
            throw new BatchException($"create {output} (it must not exist): {e.Message}");
        }
        try
        {
            await using (sink.ConfigureAwait(false))
            {
                var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await stream.CopyToAsync(sink, ct).ConfigureAwait(false);
                return sink.Length;
            }
        }
        catch
        {
            // The file is ours (CreateNew), so a torn download is not left behind.
            File.Delete(output);
            throw;
        }
    }

    public void Dispose() => _client.Dispose();

    /// <summary>An id goes into a URL path: keep it to the characters providers issue.</summary>
    internal static void CheckId(string id)
    {
        if (id.Length == 0 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            throw new BatchException($"not a batch or file id: \"{id}\"");
        }
    }

    /// <summary>The non-empty lines of <paramref name="raw"/> once each is checked to be a
    /// request of <paramref name="provider"/>'s shape, plus the endpoint every OpenAI line
    /// targets (empty for Anthropic).</summary>
    internal static (List<string> Lines, string Url) Validate(string raw, BatchProvider provider)
    {
        string[] required =
            provider == BatchProvider.Anthropic
                ? ["custom_id", "params"]
                : ["custom_id", "method", "url", "body"];
        var lines = new List<string>();
        var url = "";
        var n = 0;
        foreach (var line in raw.Split('\n'))
        {
            n++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            using var doc = ParseLine(line, n);
            var root = doc.RootElement;
            if (
                required.FirstOrDefault(k =>
                    root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(k, out _)
                ) is
                { } missing
            )
            {
                throw new BatchException($"line {n}: missing `{missing}`");
            }
            if (provider == BatchProvider.Openai)
            {
                var target = root.GetProperty("url") is { ValueKind: JsonValueKind.String } u
                    ? u.GetString()!
                    : "";
                if (url.Length == 0)
                {
                    url = target;
                }
                else if (!string.Equals(url, target, StringComparison.Ordinal))
                {
                    throw new BatchException(
                        $"line {n}: url `{target}` differs from `{url}`; a batch targets one endpoint"
                    );
                }
            }
            lines.Add(line.Trim());
        }
        return lines.Count == 0
            ? throw new BatchException("no requests in the file")
            : (lines, url);
    }

    private static JsonDocument ParseLine(string line, int n)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            throw new BatchException($"line {n}: not JSON");
        }
    }

    /// <summary>A string field of a JSON object answer, or null.</summary>
    private static string? Field(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return
                doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            throw new BatchException($"the provider answered something that is not JSON: {json}");
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(_base + path));
        if (_provider == BatchProvider.Anthropic)
        {
            request.Headers.TryAddWithoutValidation("x-api-key", _key);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        }
        return request;
    }

    private HttpRequestMessage JsonPost(string path, string body)
    {
        var request = Request(HttpMethod.Post, path);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    )
    {
        HttpResponseMessage response;
        using (request)
        {
            try
            {
                response = await _client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                throw new BatchException(
                    $"cannot reach the proxy (is `rtok proxy` running?): {e.Message}"
                );
            }
        }
        if (response.IsSuccessStatusCode)
        {
            return response;
        }
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var snippet = body.Length > ErrorSnippet ? body[..ErrorSnippet] : body;
            throw new BatchException(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {snippet}"
            );
        }
    }

    private async Task<string> TextAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await SendAsync(request, ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private string BatchPath(string id) =>
        _provider == BatchProvider.Anthropic ? $"/v1/messages/batches/{id}" : $"/v1/batches/{id}";
}
