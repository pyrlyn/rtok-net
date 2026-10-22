using System.Diagnostics;
using System.Text.Json;
using Rtok.Core;
using Rtok.Log;
using Rtok.Store;
using Serilog;

namespace Rtok.Proxy;

/// <summary>The proxy's bookkeeping: the <c>calls</c> row before forwarding, then
/// <c>call_io</c>, one <c>usage</c> row and the provider <c>tokens</c> row after. Every write is
/// best-effort: a store error logs and the traffic goes on.</summary>
internal sealed class ProxyRecorder
{
    /// <summary>The <c>hosts</c> id of <c>other</c>, for a <c>[hook] host</c> the store does not know.</summary>
    private const long OtherHost = 6;

    private readonly RtokStore _store;
    private readonly ILogger _log;
    private readonly long? _hostId;

    public ProxyRecorder(RtokStore store, ILogger log, string host)
    {
        _store = store;
        _log = log;
        _hostId = Try(() => _store.HostId(host), null, "host") ?? OtherHost;
    }

    /// <summary>The session, model and <c>calls</c> row for a request about to go upstream.</summary>
    public Recorded? Record(Wire? wire, string path, byte[] body, Func<string, string?> header)
    {
        using var doc = Json.Parse(body);
        var root = doc?.RootElement;
        var session = Session(wire, root, header, body);
        var model = ModelOf(wire, path, root);
        try
        {
            _store.UpsertSession(session, _hostId, null, null, "proxy");
            long? providerId = null;
            long? modelId = null;
            if (wire is not null && model is not null)
            {
                (providerId, modelId) = _store.UpsertModel(wire.Provider, model);
            }
            var callId = _store.InsertCall(
                session,
                "proxy",
                "api_request",
                _hostId,
                providerId,
                modelId,
                null,
                path
            );
            return new Recorded(session, model, callId);
        }
#pragma warning disable CA1031 // Fail open: a store error never stops the request.
        catch (Exception e)
#pragma warning restore CA1031
        {
            Log(session, null).Error("record {Path}: {Error}", path, e.Message);
            return null;
        }
    }

    /// <summary>After the response reached the client: the time, the bodies, then usage.</summary>
    public void Finish(
        Recorded recorded,
        long started,
        Wire? wire,
        string? contentType,
        byte[] request,
        byte[] response,
        long responseBytes
    )
    {
        var log = Log(recorded.Session, recorded.CallId);
        Try(() => _store.SetCallMs(recorded.CallId, Ms(started)), "ms", recorded);
        if (responseBytes > response.Length)
        {
            log.Error(
                "response body truncated for recording: {Received} bytes received, {Kept} kept; call_io and usage see only those",
                responseBytes,
                response.Length
            );
        }
        Try(() => _store.InsertCallIo(recorded.CallId, request, response), "call_io", recorded);
        if (wire is null)
        {
            // A path no wire owns (/v1/models) never carries usage; logging it would be noise.
            return;
        }
        var usage = UsageReader.FromResponse(wire, contentType, response);
        Usage(recorded, wire, usage);
        if (usage is null)
        {
            log.Information("no usage in upstream response");
        }
    }

    /// <summary>Upstream never answered: the time and the all-zero usage row the request still owes.</summary>
    public void UpstreamFailed(Recorded recorded, long started, Wire? wire, string message)
    {
        Try(() => _store.SetCallMs(recorded.CallId, Ms(started)), "ms", recorded);
        if (wire is not null)
        {
            Usage(recorded, wire, null);
        }
        Log(recorded.Session, recorded.CallId).Error("upstream {Message}", message);
    }

    /// <summary>The request's one <c>usage</c> row; with counters also the provider <c>tokens</c>
    /// row, without them an all-zero row and no <c>tokens</c> row.</summary>
    private void Usage(Recorded recorded, Wire wire, Usage? counters)
    {
        var usage = counters ?? default;
        Try(
            () =>
                _store.InsertUsage(
                    recorded.Session,
                    recorded.Model,
                    wire.Api,
                    usage.Input,
                    usage.CacheCreate,
                    usage.CacheRead,
                    usage.Output,
                    recorded.CallId
                ),
            "usage",
            recorded
        );
        if (counters is not null)
        {
            Try(
                () =>
                    _store.InsertProviderTokens(
                        recorded.CallId,
                        usage.Total,
                        usage.Input,
                        usage.CacheCreate,
                        usage.CacheRead,
                        usage.Output
                    ),
                "tokens",
                recorded
            );
        }
    }

    /// <summary>The wire's session field, else <c>x-rtok-session</c> or <c>x-session-id</c>,
    /// else the body's SHA-256.</summary>
    internal static string Session(
        Wire? wire,
        JsonElement? body,
        Func<string, string?> header,
        byte[] raw
    ) =>
        (wire is not null && body is { } b ? wire.SessionId(b) : null)
        ?? NonEmpty(header("x-rtok-session"))
        ?? NonEmpty(header("x-session-id"))
        ?? IdenticalResult.Sha256Hex(raw);

    /// <summary>The model a request names: where its wire keeps it (Gemini: the path), else the
    /// body's <c>model</c>.</summary>
    internal static string? ModelOf(Wire? wire, string path, JsonElement? body) =>
        wire is not null ? wire.Model(path, body)
        : body is { } b ? Json.String(b, "model")
        : null;

    /// <summary>Wall time since <paramref name="started"/> (a <see cref="Stopwatch"/> timestamp).</summary>
    internal static double Ms(long started) => Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private ILogger Log(string? session, long? callId)
    {
        var log = _log.ForSource("module").Named("proxy");
        if (session is not null)
        {
            log = log.ForContext(LogProperties.Session, session);
        }
        return callId is { } id ? log.ForContext(LogProperties.CallId, id) : log;
    }

    private void Try(Action write, string what, Recorded? recorded = null)
    {
        try
        {
            write();
        }
#pragma warning disable CA1031 // Fail open: a store error never reaches the client.
        catch (Exception e)
#pragma warning restore CA1031
        {
            Log(recorded?.Session, recorded?.CallId).Error("{What}: {Error}", what, e.Message);
        }
    }

    private T Try<T>(Func<T> read, T fallback, string what)
    {
        try
        {
            return read();
        }
#pragma warning disable CA1031 // Fail open: a store error never reaches the client.
        catch (Exception e)
#pragma warning restore CA1031
        {
            Log(null, null).Error("{What}: {Error}", what, e.Message);
            return fallback;
        }
    }
}
