using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Rtok.Core;
using Rtok.Log;
using Serilog;

namespace Rtok.Proxy;

/// <summary>Every request the proxy gets: the Host check, <c>/health</c> and <c>/live</c>, and
/// the forward with its tee. An <see cref="HttpClient"/> forwarder rather than a reverse-proxy
/// library: the tee and the recording need the body anyway.</summary>
internal sealed class ProxyHandler
{
    /// <summary>Headers that describe one hop, not the message; the body bytes stay untouched.</summary>
    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "connection",
        "keep-alive",
        "proxy-authenticate",
        "proxy-authorization",
        "te",
        "trailer",
        "transfer-encoding",
        "upgrade",
        "host",
        Hops.Header,
    };

    private const int Chunk = 64 * 1024;

    private readonly ProxyOptions _options;
    private readonly HttpClient _client;
    private readonly ProxyRecorder? _recorder;
    private readonly ILogger _log;
    private readonly TimeSpan _readTimeout;

    public ProxyHandler(ProxyOptions options, HttpClient client, LiveCalls live)
    {
        _options = options;
        _client = client;
        Live = live;
        _log = options.Logger.ForSource("module").Named("proxy");
        _recorder =
            options.Store is { } store && !options.Plain
                ? new ProxyRecorder(store, options.Logger, options.Config.Hook.Host)
                : null;
        _readTimeout = TimeSpan.FromSeconds(
            Math.Clamp(options.Config.Proxy.TimeoutS, 1, int.MaxValue)
        );
    }

    public LiveCalls Live { get; }

    public async Task HandleAsync(HttpContext context)
    {
        // S6: one Host, one upstream identity; a list is refused before anything is forwarded.
        if (context.Request.Headers.Host.ToString().Contains(',', StringComparison.Ordinal))
        {
            await ErrorAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Host holds more than one host"
                )
                .ConfigureAwait(false);
            return;
        }
        var (path, query) = Target(context);
        if (path is "/health" or "/live")
        {
            await LocalAsync(context, path).ConfigureAwait(false);
            return;
        }
        await ForwardAsync(context, path, query).ConfigureAwait(false);
    }

    /// <summary>Whether a client may read <c>/health</c> and <c>/live</c> (rtok S5): loopback, or
    /// one presenting this run's token. Without a known peer (an in-process host) it is local.</summary>
    internal static bool Allowed(IPAddress? peer, string? presented, string? token) =>
        peer is null
        || IPAddress.IsLoopback(peer)
        || (peer.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(peer.MapToIPv4()))
        || (token is not null && AccessToken.Matches(token, presented));

    /// <summary>The path and query exactly as the client sent them, still percent-encoded.</summary>
    private static (string Path, string? Query) Target(HttpContext context)
    {
        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        if (string.IsNullOrEmpty(raw) || raw[0] != '/')
        {
            var request = context.Request;
            raw = (request.PathBase + request.Path).ToUriComponent() + request.QueryString;
        }
        var mark = raw.IndexOf('?', StringComparison.Ordinal);
        return mark < 0 ? (raw, null) : (raw[..mark], raw[(mark + 1)..]);
    }

    private async Task LocalAsync(HttpContext context, string path)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }
        var presented = AccessToken.Presented(
            request.Headers.Authorization.ToString(),
            request.Query["token"].ToString()
        );
        if (!Allowed(context.Connection.RemoteIpAddress, presented, _options.AccessToken))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        context.Response.ContentType = "application/json";
        var body = string.Equals(path, "/health", StringComparison.Ordinal)
            ? Health()
            : JsonSerializer.SerializeToUtf8Bytes(
                Live.Snapshot(),
                ProxyJson.Default.IReadOnlyListLiveCall
            );
        await context.Response.Body.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
    }

    private byte[] Health()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteBoolean("ok", true);
            if (_options.Plain)
            {
                json.WriteString("mode", "passthrough");
                json.WriteBoolean("enabled", false);
                json.WriteBoolean("recording", false);
                json.WriteNumber("live", Live.Count);
            }
            else
            {
                json.WriteString("mode", _options.Config.Proxy.Mode);
                json.WriteBoolean("enabled", true);
                json.WriteBoolean("recording", true);
            }
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private async Task ForwardAsync(HttpContext context, string path, string? query)
    {
        var started = Stopwatch.GetTimestamp();
        var request = context.Request;
        var hopValues = request.Headers[Hops.Header];
        var hops = Hops.Count(hopValues.Count > 0 ? hopValues[0] : null);
        if (hops >= Hops.Max)
        {
            var loop =
                $"{request.Method} {path}: {hops} rtok proxy hops, refusing a loop (check proxy.upstream)";
            _log.Error("{Message}", loop);
            await ErrorAsync(context, StatusCodes.Status508LoopDetected, loop)
                .ConfigureAwait(false);
            return;
        }
        if (hops > 0)
        {
            _log.Warning(
                "{Method} {Path}: already through {Hops} rtok proxy hop(s), forwarding as is",
                request.Method,
                path,
                hops
            );
        }
        byte[] body;
        try
        {
            body = await ReadBodyAsync(request, context.RequestAborted).ConfigureAwait(false);
        }
        catch (BadHttpRequestException e)
        {
            await ErrorAsync(context, e.StatusCode, e.Message).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        var wire = Wire.For(path);
        // Plain: a kill switch is off, or an earlier rtok already recorded this request.
        var plain = _options.Plain || hops > 0;
        var recorded = plain
            ? null
            : _recorder?.Record(wire, path, body, h => request.Headers[h].ToString());
        if (!plain && wire is not null)
        {
            body = Prepare(wire, body, _options.Config.Proxy.IncludeUsage);
        }
        string target;
        try
        {
            target = UpstreamUrl.Join(Upstream(wire, path, request.Headers), path, query);
        }
        catch (FormatException e)
        {
            await ErrorAsync(context, StatusCodes.Status400BadRequest, e.Message)
                .ConfigureAwait(false);
            return;
        }
        var exchange = new Exchange(request.Method, path, wire, body, plain, recorded, started);
        await RelayAsync(context, exchange, target, hops).ConfigureAwait(false);
    }

    /// <summary>Sends the request upstream and streams the answer back; the bookkeeping runs
    /// once the client has all of it.</summary>
    private async Task RelayAsync(HttpContext context, Exchange exchange, string target, int hops)
    {
        using var outgoing = Outgoing(context.Request, exchange, target, hops);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(_readTimeout);
        HttpResponseMessage upstream;
        try
        {
            upstream = await _client
                .SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            await FailedAsync(context, exchange, e).ConfigureAwait(false);
            return;
        }
        using (upstream)
        {
            var response = context.Response;
            response.StatusCode = (int)upstream.StatusCode;
            CopyHeaders(upstream.Headers, response.Headers);
            CopyHeaders(upstream.Content.Headers, response.Headers);
            var contentType = upstream.Content.Headers.ContentType?.ToString();
            var (kept, total) = await TeeAsync(context, upstream, timeout).ConfigureAwait(false);
            await response.CompleteAsync().ConfigureAwait(false);
            if (exchange.Plain)
            {
                Live.Push(LiveCallOf(exchange, response.StatusCode, total));
            }
            else if (exchange.Recorded is { } recorded)
            {
                _recorder!.Finish(
                    recorded,
                    exchange.Started,
                    exchange.Wire,
                    contentType,
                    exchange.Body,
                    kept,
                    total
                );
            }
        }
    }

    /// <summary>Upstream never answered: the request still owes its live entry or its usage row,
    /// and the client gets rtok's 502.</summary>
    private async Task FailedAsync(HttpContext context, Exchange exchange, Exception error)
    {
        var gone = context.RequestAborted.IsCancellationRequested;
        var reason =
            gone ? "client closed the request"
            : error is OperationCanceledException
                ? $"no response within {_readTimeout.TotalSeconds:0}s"
            : error.Message;
        var failure = $"{exchange.Method} {exchange.Path}: {reason}";
        if (exchange.Plain)
        {
            Live.Push(LiveCallOf(exchange, StatusCodes.Status502BadGateway, 0));
        }
        else if (exchange.Recorded is { } recorded)
        {
            _recorder!.UpstreamFailed(recorded, exchange.Started, exchange.Wire, failure);
        }
        if (!gone)
        {
            await ErrorAsync(
                    context,
                    StatusCodes.Status502BadGateway,
                    $"rtok proxy: upstream error: {failure}"
                )
                .ConfigureAwait(false);
        }
    }

    /// <summary>Streams the upstream body to the client chunk by chunk while keeping up to
    /// <see cref="ProxyOptions.TeeBytes"/> of it. An upstream that stops mid-body aborts the
    /// client connection, so the client sees a cut, not a short success.</summary>
    private async Task<(byte[] Kept, long Total)> TeeAsync(
        HttpContext context,
        HttpResponseMessage upstream,
        CancellationTokenSource timeout
    )
    {
        using var kept = new MemoryStream();
        long total = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(Chunk);
        try
        {
            var stream = await upstream
                .Content.ReadAsStreamAsync(timeout.Token)
                .ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                int read;
                while ((read = await ReadAsync(stream, buffer, timeout).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (kept.Length < _options.TeeBytes)
                    {
                        kept.Write(buffer, 0, (int)Math.Min(read, _options.TeeBytes - kept.Length));
                    }
                    if (
                        !await ToClientAsync(context, buffer.AsMemory(0, read))
                            .ConfigureAwait(false)
                    )
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception e)
            when (e is IOException or HttpRequestException or OperationCanceledException)
        {
            context.Abort();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return (kept.ToArray(), total);
    }

    /// <summary>One upstream read; the read budget starts again for each.</summary>
    private async Task<int> ReadAsync(Stream stream, byte[] buffer, CancellationTokenSource timeout)
    {
        timeout.CancelAfter(_readTimeout);
        return await stream
            .ReadAsync(buffer.AsMemory(0, Chunk), timeout.Token)
            .ConfigureAwait(false);
    }

    /// <summary>One chunk to the client, flushed so a stream arrives as it is made. False when
    /// the client went away; what arrived so far is still recorded.</summary>
    private static async Task<bool> ToClientAsync(HttpContext context, ReadOnlyMemory<byte> chunk)
    {
        try
        {
            await context
                .Response.Body.WriteAsync(chunk, context.RequestAborted)
                .ConfigureAwait(false);
            await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (e is IOException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>The upstream request: every end-to-end header, the hop count plus one, and when
    /// recording <c>Accept-Encoding: identity</c> so the tee can read the body. Plain mode keeps
    /// the client's encoding and passes the bytes through as they come.</summary>
    private static HttpRequestMessage Outgoing(
        HttpRequest request,
        Exchange exchange,
        string target,
        int hops
    )
    {
        var (body, plain) = (exchange.Body, exchange.Plain);
        var outgoing = new HttpRequestMessage(new HttpMethod(exchange.Method), target);
        if (
            body.Length > 0
            || request.ContentLength is not null
            || request.Headers.ContentType.Count > 0
        )
        {
            outgoing.Content = new ByteArrayContent(body);
        }
        foreach (var (name, values) in request.Headers)
        {
            if (
                HopByHop.Contains(name)
                || name.Equals("content-length", StringComparison.OrdinalIgnoreCase)
                || (!plain && name.Equals("accept-encoding", StringComparison.OrdinalIgnoreCase))
            )
            {
                continue;
            }
            if (!outgoing.Headers.TryAddWithoutValidation(name, (IEnumerable<string?>)values))
            {
                outgoing.Content?.Headers.TryAddWithoutValidation(
                    name,
                    (IEnumerable<string?>)values
                );
            }
        }
        if (!plain)
        {
            outgoing.Headers.TryAddWithoutValidation("accept-encoding", "identity");
        }
        outgoing.Headers.TryAddWithoutValidation(
            Hops.Header,
            (hops + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
        );
        return outgoing;
    }

    private static void CopyHeaders(System.Net.Http.Headers.HttpHeaders from, IHeaderDictionary to)
    {
        foreach (var (name, values) in from.NonValidated)
        {
            if (!HopByHop.Contains(name))
            {
                to[name] = values.ToArray();
            }
        }
    }

    /// <summary>The wire's own request shaping. Fail open: a body that is not a JSON object, or
    /// that the wire leaves alone, goes on exactly as it came.</summary>
    internal static byte[] Prepare(Wire wire, byte[] body, bool includeUsage)
    {
        try
        {
            if (
                JsonNode.Parse(body) is not JsonObject json
                || !wire.PrepareRequest(json, includeUsage)
            )
            {
                return body;
            }
            using var buffer = new MemoryStream();
            using (
                var writer = new Utf8JsonWriter(
                    buffer,
                    new JsonWriterOptions
                    {
                        Encoder = System
                            .Text
                            .Encodings
                            .Web
                            .JavaScriptEncoder
                            .UnsafeRelaxedJsonEscaping,
                    }
                )
            )
            {
                json.WriteTo(writer);
            }
            return buffer.ToArray();
        }
        catch (Exception e)
            when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return body;
        }
    }

    /// <summary>The OpenAI wires go to <c>openai_upstream</c>, Gemini to <c>gemini_upstream</c>,
    /// the rest to <c>upstream</c>, except OpenAI's batch and files APIs: Anthropic's Files API
    /// shares the path but always carries <c>anthropic-version</c>.</summary>
    private string Upstream(Wire? wire, string path, IHeaderDictionary headers)
    {
        var proxy = _options.Config.Proxy;
        return (wire?.Provider) switch
        {
            Wire.OpenAiProvider => proxy.OpenaiUpstream,
            Wire.GeminiProvider => proxy.GeminiUpstream,
            null when IsOpenAiBatch(path) && !headers.ContainsKey("anthropic-version") =>
                proxy.OpenaiUpstream,
            _ => proxy.Upstream,
        };
    }

    private static bool IsOpenAiBatch(string path) =>
        Under(path, "/v1/batches") || Under(path, "/v1/files");

    private static bool Under(string path, string root) =>
        path.StartsWith(root, StringComparison.Ordinal)
        && (path.Length == root.Length || path[root.Length] == '/');

    private static LiveCall LiveCallOf(Exchange exchange, int status, long responseBytes)
    {
        using var doc = Json.Parse(exchange.Body);
        return new LiveCall(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exchange.Method,
            exchange.Path,
            exchange.Wire?.Provider,
            ProxyRecorder.ModelOf(exchange.Wire, exchange.Path, doc?.RootElement),
            status,
            exchange.Body.Length,
            responseBytes,
            ProxyRecorder.Ms(exchange.Started)
        );
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancel)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancel).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>rtok's error body: <c>{"type":"error","error":{"type":…,"message":…}}</c>.</summary>
    private static async Task ErrorAsync(HttpContext context, int status, string message)
    {
        var response = context.Response;
        if (response.HasStarted)
        {
            context.Abort();
            return;
        }
        response.StatusCode = status;
        response.ContentType = "application/json";
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("type", "error");
            json.WriteStartObject("error");
            json.WriteString(
                "type",
                status switch
                {
                    StatusCodes.Status502BadGateway => "upstream_error",
                    StatusCodes.Status429TooManyRequests => "rate_limit_error",
                    _ => "invalid_request_error",
                }
            );
            json.WriteString("message", message);
            json.WriteEndObject();
            json.WriteEndObject();
        }
        await response
            .Body.WriteAsync(buffer.ToArray(), context.RequestAborted)
            .ConfigureAwait(false);
    }

    /// <summary>One forwarded request as the relay sees it.</summary>
    private sealed record Exchange(
        string Method,
        string Path,
        Wire? Wire,
        byte[] Body,
        bool Plain,
        Recorded? Recorded,
        long Started
    );
}
