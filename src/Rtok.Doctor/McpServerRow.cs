using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>One MCP server Claude Code starts.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Cmd">The program and arguments; empty for a URL server.</param>
/// <param name="Tools">How many tools it lists; 0 when it did not answer.</param>
/// <param name="DescTokens">The estimated tokens of its tool descriptions.</param>
public sealed record McpServerRow(string Name, string Cmd, int Tools, int DescTokens);
