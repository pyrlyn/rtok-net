using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>One module of rtok in a host.</summary>
/// <param name="Name"><c>hooks</c>, <c>mcp</c>, <c>proxy</c>, <c>plugin</c>.</param>
/// <param name="State"><c>installed</c>, <c>not_installed</c> or <c>not_supported</c>.</param>
/// <param name="Note">The flag or reason after the state.</param>
public sealed record AgentModule(string Name, string State, string Note);
