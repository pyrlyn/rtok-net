using System.Globalization;

namespace Rtok.Proxy;

/// <summary>How many rtok proxies a request has passed (rtok T442). A second pass must not
/// record or rewrite it again, and a proxy whose upstream is itself must not recurse.</summary>
public static class Hops
{
    /// <summary>The header every forwarded request carries.</summary>
    public const string Header = "x-rtok-proxied";

    /// <summary>A chain this long is a loop, not a deployment: answered 508.</summary>
    public const int Max = 8;

    /// <summary>The incoming count. Anything but a number still says another rtok saw the
    /// request, so it counts as one.</summary>
    /// <param name="value">The header's first value; null when absent.</param>
    /// <returns>The hop count.</returns>
    public static int Count(string? value) =>
        value is null ? 0
        : uint.TryParse(
            value.Trim(),
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var hops
        )
            ? (int)Math.Min(hops, int.MaxValue)
        : 1;
}
