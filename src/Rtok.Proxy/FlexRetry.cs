using Rtok.Config;

namespace Rtok.Proxy;

/// <summary>How one request rtok sent as Flex is retried after a 429 (<c>[proxy.flex]
/// on_429</c>): <c>backoff</c> waits and sends it again, <c>default</c> sends it once on
/// standard processing, <c>none</c> (and any unknown value) hands the 429 to the client.</summary>
internal sealed class FlexRetry
{
    private readonly byte[] _original;
    private readonly bool _backoff;
    private readonly int _retries;
    private readonly TimeSpan _delay;

    private FlexRetry(byte[] original, bool backoff, int retries, TimeSpan delay)
    {
        _original = original;
        _backoff = backoff;
        _retries = retries;
        _delay = delay;
    }

    /// <summary>The retry for a body rtok put on Flex; null under <c>none</c>.</summary>
    /// <param name="original">The body as it was before rtok set the tier.</param>
    /// <param name="flex"><c>[proxy.flex]</c>.</param>
    public static FlexRetry? For(byte[] original, FlexPolicy flex) =>
        flex.On429 switch
        {
            "backoff" => new FlexRetry(
                original,
                true,
                (int)Math.Min(flex.Retries, Flex.MaxRetries),
                TimeSpan.FromMilliseconds(Math.Min(flex.BackoffMs, (ulong)int.MaxValue))
            ),
            "default" => new FlexRetry(original, false, 1, TimeSpan.Zero),
            _ => null,
        };

    /// <summary>The wait before retry <paramref name="n"/> (0-based) and the body to send then
    /// (null keeps the last one), or null once there is no retry left. <c>backoff</c> waits the
    /// longer of its doubling delay and the server's <c>Retry-After</c>, and gives up when the
    /// server asks for more than <see cref="Flex.MaxDelay"/>. <c>default</c> never waits: its
    /// retry goes to another tier, and <c>Retry-After</c> speaks only of Flex capacity.</summary>
    public (TimeSpan Wait, byte[]? Body)? Next(int n, TimeSpan? server)
    {
        if (n >= _retries)
        {
            return null;
        }
        if (!_backoff)
        {
            return Fallback() is { } body ? (TimeSpan.Zero, body) : null;
        }
        if (server > Flex.MaxDelay)
        {
            return null;
        }
        var ours = TimeSpan.FromTicks(
            (long)Math.Min(_delay.Ticks * Math.Pow(2, n), Flex.MaxDelay.Ticks)
        );
        return (server is { } s && s > ours ? s : ours, null);
    }

    /// <summary>The client's own tier when <c>force</c> overwrote one, <c>auto</c> when it sent
    /// none.</summary>
    private byte[]? Fallback()
    {
        var json = Json.ParseObject(_original);
        return json?["service_tier"] is not null ? _original : Flex.SetTier(_original, "auto");
    }
}
