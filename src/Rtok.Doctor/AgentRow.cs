using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>One host app and rtok's modules in it.</summary>
/// <param name="Host">The host id.</param>
/// <param name="Kind"><c>cli</c> or <c>desktop</c>.</param>
/// <param name="Modules">Each module's state.</param>
public sealed record AgentRow(string Host, string Kind, IReadOnlyList<AgentModule> Modules);
