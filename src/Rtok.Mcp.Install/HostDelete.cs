using Rtok.Agent.Sdk;

namespace Rtok.Mcp.Install;

/// <summary>
/// Deletes a host file under an <see cref="Apply"/>, as <see cref="HostWrite"/> writes one.
/// <see cref="HostFile.Delete"/> is the real one (backed up first).
/// </summary>
public delegate void HostDelete(Apply apply, string path, string report);
