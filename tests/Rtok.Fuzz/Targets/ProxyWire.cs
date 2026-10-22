using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Rtok.Config;
using Rtok.Proxy;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any bytes as a provider's response, JSON or SSE, through each wire's usage parser, and as a
/// request body through the wire's request shaping and the session and model reads: usage or
/// none, never an exception. The same bytes also go through the handler in plain mode, under a
/// Host the input picks, and must come back unchanged unless the Host check refused them.
/// </summary>
internal static class ProxyWire
{
    private static readonly string[] Paths =
    [
        "/v1/messages",
        "/v1/chat/completions",
        "/v1/responses",
        "/v1beta/models/gemini-2.5-pro:streamGenerateContent",
    ];

    private static readonly ProxyHandler Plain = PlainHandler();

    public static void Run(ReadOnlySpan<byte> data)
    {
        var pick = data.IsEmpty ? (byte)0 : data[0];
        var body = data.IsEmpty ? [] : data[1..].ToArray();
        var path = Paths[pick & 3];
        var wire = Wire.For(path)!;
        var usage = UsageReader.FromResponse(
            wire,
            (pick & 4) != 0 ? "text/event-stream" : "application/json",
            body
        );
        _ = usage?.Total;
        Request(wire, path, body);
        Relay(path, body, (pick & 8) != 0 ? Encoding.UTF8.GetString(body) : "127.0.0.1:8790");
    }

    private static void Request(Wire wire, string path, byte[] body)
    {
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException) { }
        using (doc)
        {
            _ = ProxyRecorder.Session(wire, doc?.RootElement, _ => null, body);
            _ = ProxyRecorder.ModelOf(wire, path, doc?.RootElement);
        }
        var prepared = ProxyHandler.Prepare(wire, body, includeUsage: true);
        FuzzInput.Check(
            ReferenceEquals(prepared, body) || JsonNode.Parse(prepared) is JsonObject,
            "request shaping wrote something that is not a JSON object"
        );
    }

    private static void Relay(string path, byte[] body, string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.Headers.Host = host;
        context.Request.Body = new MemoryStream(body);
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        using var response = new MemoryStream();
        context.Response.Body = response;

        Plain.HandleAsync(context).GetAwaiter().GetResult();

        if (host.Contains(',', StringComparison.Ordinal))
        {
            FuzzInput.Check(context.Response.StatusCode == 400, "a Host list was not refused");
            return;
        }
        FuzzInput.Check(
            response.ToArray().AsSpan().SequenceEqual(body),
            "plain mode changed the bytes"
        );
    }

    private static ProxyHandler PlainHandler()
    {
        var config = new RtokConfig();
        config.Proxy.Enabled = false;
        config.Proxy.Upstream = "http://upstream.invalid";
        config.Proxy.OpenaiUpstream = "http://upstream.invalid";
        config.Proxy.GeminiUpstream = "http://upstream.invalid";
        return new ProxyHandler(
            new ProxyOptions(config) { CertFile = null },
            new HttpClient(new Echo()),
            new LiveCalls()
        );
    }

    /// <summary>An upstream that answers with the request body, byte for byte.</summary>
    private sealed class Echo : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var bytes = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes),
            };
        }
    }
}
