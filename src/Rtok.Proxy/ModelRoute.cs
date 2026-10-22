using System.Text.Json.Nodes;
using Rtok.Config;

namespace Rtok.Proxy;

/// <summary>Model routing on the bulk and internal lanes (rtok T385.10): a rule swaps the
/// request's <c>model</c> for a cheaper one before the call is recorded, so the ledger names the
/// model that served it. Nothing else in the body changes.</summary>
/// <remarks>Whether the cheaper model answers well enough is a bench result a proxy cannot see,
/// so a rule acts only once the operator names that bench (<c>gate</c>) and every other field;
/// the agent lane has no rule at all (<see cref="Lanes.Route"/>). Gemini names its model in the
/// path, which is never rewritten, so a Gemini call never matches.</remarks>
internal static class ModelRoute
{
    /// <summary>Whether a rule may act: <c>from</c>, <c>to</c>, <c>provider</c> and
    /// <c>gate</c> all set.</summary>
    public static bool Active(RouteRule rule) =>
        rule.From.Count > 0
        && !string.IsNullOrWhiteSpace(rule.To)
        && !string.IsNullOrWhiteSpace(rule.Provider)
        && !string.IsNullOrWhiteSpace(rule.Gate);

    /// <summary>An exact model id, or a prefix when the pattern ends in <c>*</c>.</summary>
    public static bool Matches(string pattern, string model) =>
        pattern.EndsWith('*')
            ? model.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : string.Equals(pattern, model, StringComparison.Ordinal);

    /// <summary>The body pointed at the rule's model, or null to forward it as it is: the rule
    /// is not active, the body is not a JSON object naming one of <c>from</c>, or it already
    /// names <c>to</c>.</summary>
    public static Routed? Apply(RouteRule rule, byte[] body)
    {
        if (!Active(rule))
        {
            return null;
        }
        try
        {
            if (
                Json.ParseObject(body) is not { } json
                || json["model"] is not JsonValue value
                || !value.TryGetValue<string>(out var from)
            )
            {
                return null;
            }
            var to = rule.To.Trim();
            if (
                string.Equals(from, to, StringComparison.Ordinal)
                || !rule.From.Any(p => Matches(p, from))
            )
            {
                return null;
            }
            json["model"] = to;
            return new Routed(Json.Write(json), from, to, rule.Provider.Trim());
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
