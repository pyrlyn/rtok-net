using System.Text.Json.Nodes;

namespace Rtok.Plugin.Sdk;

/// <summary>The one thing <see cref="WireRequest"/> needs from a provider dialect. The host
/// implements it; a plugin never names it.</summary>
public interface IToolResults
{
    /// <summary>Every tool-result payload in <paramref name="request"/>, with its id and turn
    /// distance.</summary>
    IReadOnlyList<ToolResultRef> ToolResults(JsonNode request);

    /// <summary>Skill bodies the archive plugin may shrink.</summary>
    IReadOnlyList<SkillRef> Skills(JsonNode request) => [];

    /// <summary>Large non-result payloads a proxy pass may shrink losslessly; wires with user
    /// content blocks override it.</summary>
    IReadOnlyList<BlobRef> LiveBlobs(JsonNode request) => [];

    /// <summary>Injected skill bodies in <paramref name="request"/>.</summary>
    IReadOnlyList<SkillRef> SkillRefs(JsonNode request) => [];
}
