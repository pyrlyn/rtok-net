using Rtok.Agent.Sdk;

namespace Rtok.Mcp.Install;

/// <summary>
/// Writes a host file under an <see cref="Apply"/>: nothing on a dry run or a report that
/// changed nothing. <see cref="HostFile.Write"/> is the real one (atomic, backed up).
/// </summary>
public delegate void HostWrite(Apply apply, string path, string body, string report);
