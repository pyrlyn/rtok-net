using System.Text.Json;

namespace Rtok.Plugin.Sdk;

/// <summary>A tool call about to run. A plugin may deny or rewrite it.</summary>
/// <param name="ToolName">Host tool name, e.g. <c>Bash</c>, <c>Read</c>.</param>
/// <param name="ToolInput">Tool arguments as the host sent them.</param>
public sealed record PreToolUse(string ToolName, JsonElement ToolInput);
