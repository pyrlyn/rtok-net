using System.Text.Json.Serialization;

namespace Rtok.Agents.Hosts;

/// <summary>The source-generated JSON for <see cref="AgentListRow"/>: AOT-safe, snake_case.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(IReadOnlyList<AgentListRow>))]
public sealed partial class AgentsJson : JsonSerializerContext;
