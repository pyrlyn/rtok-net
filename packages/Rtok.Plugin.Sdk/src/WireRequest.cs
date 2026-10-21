using System.Text.Json.Nodes;

namespace Rtok.Plugin.Sdk;

/// <summary>
/// A provider request, seen through the wire that owns it. Deliberately narrow: a plugin
/// reaches the tool results and nothing else, so a rewrite cannot reorder messages, change the
/// system prompt or touch the model choice.
/// </summary>
/// <param name="wire">The dialect that understands <paramref name="body"/>.</param>
/// <param name="body">The parsed request body.</param>
public sealed class WireRequest(IToolResults wire, JsonNode body)
{
    /// <summary>Every tool result in the request.</summary>
    public IReadOnlyList<ToolResultRef> ToolResults() => wire.ToolResults(body);

    /// <summary>Skill bodies in the request.</summary>
    public IReadOnlyList<SkillRef> Skills() => wire.Skills(body);

    /// <summary>Every shrinkable non-result payload in the request.</summary>
    public IReadOnlyList<BlobRef> LiveBlobs() => wire.LiveBlobs(body);

    /// <summary>Every injected skill body in the request.</summary>
    public IReadOnlyList<SkillRef> SkillRefs() => wire.SkillRefs(body);
}
