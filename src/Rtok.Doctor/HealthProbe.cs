using Rtok.Agents.Hosts;
using Rtok.Config;

namespace Rtok.Doctor;

/// <summary>The body of <c>GET /health</c> at <paramref name="hostport"/>, or null when nothing answers.</summary>
/// <param name="hostport">The host and port: <c>127.0.0.1:8790</c>.</param>
/// <param name="limit">How long it may take.</param>
public delegate string? HealthProbe(string hostport, TimeSpan limit);
