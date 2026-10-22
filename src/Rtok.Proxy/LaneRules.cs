using Rtok.Config;

namespace Rtok.Proxy;

/// <summary>What a request on one lane may get: each rewrite only when its global switch and
/// the lane's switch are both on, and a read timeout no longer than the global one (rtok T385.2).
/// Flex is the one exception: it has no global switch, and a flex lane without its own timeout
/// waits at least <see cref="FlexTimeoutS"/>, since flex answers slowly by design.</summary>
/// <param name="Compress"><c>proxy.mode = "compress"</c> and the lane's <c>compress</c>.</param>
/// <param name="Toon">The toon filter inside the compress pass.</param>
/// <param name="ToolsRewrite"><c>[proxy.tools_rewrite]</c>.</param>
/// <param name="ContextManagement"><c>[proxy] context_management</c>.</param>
/// <param name="SemanticCache"><c>[plugins.proxy.semantic_cache]</c>.</param>
/// <param name="Flex">The lane asks for OpenAI's flex tier.</param>
/// <param name="TimeoutS">The upstream read timeout in seconds.</param>
public readonly record struct LaneRules(
    bool Compress,
    bool Toon,
    bool ToolsRewrite,
    bool ContextManagement,
    bool SemanticCache,
    bool Flex,
    ulong TimeoutS
)
{
    /// <summary>The least a flex lane without its own <c>timeout_s</c> waits.</summary>
    public const ulong FlexTimeoutS = 900;

    /// <summary>The rules for <paramref name="lane"/> under <paramref name="config"/>.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="config">The loaded config.</param>
    /// <returns>The effective rules.</returns>
    public static LaneRules For(Lane lane, RtokConfig config)
    {
        var proxy = config.Proxy;
        var policy = lane.Policy(proxy.Lanes);
        var compress =
            string.Equals(proxy.Mode, "compress", StringComparison.Ordinal) && policy.Compress;
        return new LaneRules(
            compress,
            compress && config.Plugins.Toon.Enabled && policy.Toon,
            proxy.ToolsRewrite.Enabled && policy.ToolsRewrite,
            proxy.ContextManagement && policy.ContextManagement,
            config.Plugins.Proxy.SemanticCache.Enabled && policy.SemanticCache,
            policy.Flex,
            Timeout(policy.Flex, policy.TimeoutS, proxy.TimeoutS)
        );
    }

    /// <summary>The lane's read timeout: 0 keeps the global one; otherwise the shorter of the
    /// two, except that a flex lane keeps what it set and waits at least
    /// <see cref="FlexTimeoutS"/> when it set nothing.</summary>
    /// <param name="flex">The lane's <c>flex</c>.</param>
    /// <param name="lane">The lane's <c>timeout_s</c>.</param>
    /// <param name="global"><c>[proxy] timeout_s</c>.</param>
    /// <returns>Seconds.</returns>
    public static ulong Timeout(bool flex, ulong lane, ulong global) =>
        (flex, lane) switch
        {
            (true, 0) => Math.Max(global, FlexTimeoutS),
            (false, 0) => global,
            (true, _) => lane,
            _ => Math.Min(lane, global),
        };
}
