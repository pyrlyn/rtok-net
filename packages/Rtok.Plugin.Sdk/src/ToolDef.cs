using System.Text.Json;

namespace Rtok.Plugin.Sdk;

/// <summary>An MCP tool exposed by <c>rtok mcp</c>.</summary>
/// <param name="Name">Tool name as the host sees it.</param>
/// <param name="Description">One short sentence: every listed tool costs tokens in every
/// request, so the host tests it against a token cap.</param>
/// <param name="InputSchema">JSON Schema of the tool arguments.</param>
public sealed record ToolDef(string Name, string Description, JsonElement InputSchema);
