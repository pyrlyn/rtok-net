using System.Text.Json;
using Rtok.Config;
using Rtok.Model;
using Rtok.Web;
using Rtok.Web.Contracts;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// What <c>rtok web</c> reads from outside, picked by the first byte: a <c>/ws</c> text message
/// is answered with one frame that echoes its id and serializes; the Host and Origin checks
/// answer without throwing and never pass an Origin whose Host they refuse; an asset path
/// decodes to a relative path with no dot or empty segment. No plugin is linked and no store
/// is open, so a toggle or an expand is refused before it could write.
/// </summary>
internal static class WsMessage
{
    private static readonly WsDispatcher Dispatcher = new(
        new OperatorModel(
            Unread(),
            [],
            new ConfigLoadOptions
            {
                Environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["HOME"] = "/nonexistent/rtok-fuzz",
                    ["RTOK_HOME"] = "/nonexistent/rtok-fuzz/.rtok",
                },
            }
        ),
        null
    );

    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        switch (pick % 3)
        {
            case 0:
                Message(text);
                break;
            case 1:
                Guard(text);
                break;
            default:
                Path(text);
                break;
        }
    }

    private static void Message(string text)
    {
        var answer = Dispatcher.Answer(text);
        _ = JsonSerializer.Serialize(answer, WsJsonContext.Default.ServerFrame);
        WsRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize(text, WsJsonContext.Default.WsRequest);
        }
        catch (JsonException) { }
        FuzzInput.Check(answer.Req == request?.Req, "the reply does not echo the request id");
        FuzzInput.Check(
            answer is MessageFrame { Text.Length: > 0 },
            "a message with no linked plugin got something other than a refusal"
        );
    }

    private static void Guard(string text)
    {
        var parts = text.Split('\n', 2);
        var host = parts[0];
        var origin = parts.Length > 1 ? parts[1] : null;
        var hostAllowed = RequestGuard.HostAllowed(host);
        var originAllowed = RequestGuard.OriginAllowed(origin, host);
        _ = RequestGuard.IsLoopback(host);
        FuzzInput.Check(
            !originAllowed || origin is null || hostAllowed,
            "an Origin passed for a refused Host"
        );
    }

    private static void Path(string text)
    {
        if (AssetPath.Decode("/" + text) is not { } path || path.Length == 0)
        {
            return;
        }
        FuzzInput.Check(
            path.Split('/')
                .All(s =>
                    s is not ("" or "." or "..")
                    && !s.Contains('\\', StringComparison.Ordinal)
                    && !s.Contains('\0', StringComparison.Ordinal)
                ),
            "a decoded path has an empty, dot or separator segment"
        );
    }

    private static ModelSources Unread() =>
        new(
            new Never<OverviewPage>(),
            new Never<IReadOnlyList<PluginRow>>(),
            new Never<IReadOnlyList<CallItem>>(),
            new Never<IReadOnlyList<SessionItem>>(),
            new Never<TextPage>(),
            new Never<TextPage>(),
            new Never<TextPage>(),
            new Never<TextPage>(),
            new Never<IReadOnlyList<ConfigRow>>(),
            new Never<TextPage>(),
            new Never<TextPage>(),
            new Never<TextPage>(),
            new Never<TextPage>(),
            new Never<TextPage>(),
            new Never<TextPage>()
        );

    // Answering a message never builds a snapshot.
    private sealed class Never<T> : IPageSource<T>
    {
        public T Read(ModelContext context) =>
            throw new InvalidOperationException("the fuzz target built a snapshot");
    }
}
