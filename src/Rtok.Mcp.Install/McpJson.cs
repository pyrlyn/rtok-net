using System.Text.Json.Serialization;

namespace Rtok.Mcp.Install;

/// <summary>The source-generated JSON for <see cref="McpRow"/>: <c>agents info --json</c> and the web model share it.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(McpRow))]
[JsonSerializable(typeof(IReadOnlyList<McpRow>))]
public sealed partial class McpJson : JsonSerializerContext;
