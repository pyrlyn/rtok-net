using System.Text.Json;

namespace Rtok.Plugin.Sdk;

/// <summary>A tool call that has run. Its result cannot be changed; a plugin may only add
/// context beside it (rtok D2).</summary>
/// <param name="ToolName">Host tool name.</param>
/// <param name="ToolInput">Tool arguments as the host sent them.</param>
/// <param name="ToolResponse">What the tool returned.</param>
public sealed record PostToolUse(string ToolName, JsonElement ToolInput, JsonElement ToolResponse);
