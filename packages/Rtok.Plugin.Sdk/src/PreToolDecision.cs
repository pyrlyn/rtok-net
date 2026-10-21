using System.Text.Json.Nodes;

namespace Rtok.Plugin.Sdk;

/// <summary>What a plugin may do to a tool call before it runs. The first <see cref="Deny"/>
/// wins; among rewrites the last one wins.</summary>
public abstract record PreToolDecision
{
    private PreToolDecision() { }

    /// <summary>Block the call.</summary>
    /// <param name="Reason">Why; shown to the model.</param>
    public sealed record Deny(string Reason) : PreToolDecision;

    /// <summary>Replace the tool input with something cheaper but equivalent.</summary>
    /// <param name="Input">The replacement tool input.</param>
    /// <param name="Reason">Why it was rewritten.</param>
    public sealed record Rewrite(JsonNode Input, string Reason) : PreToolDecision;
}
