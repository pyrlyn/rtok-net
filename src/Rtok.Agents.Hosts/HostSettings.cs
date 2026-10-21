using Rtok.Agent.Sdk;

namespace Rtok.Agents.Hosts;

/// <summary>The <c>[setup]</c> switches one run follows, flags already applied.</summary>
/// <param name="DryRun">Print what would change; write nothing.</param>
/// <param name="Yes">Answer yes to every question (remove an entry the user changed).</param>
/// <param name="Backup">Copy each file before the first write to it.</param>
/// <param name="BackupFiles">Backup generations kept per file; 0 keeps all.</param>
/// <param name="Mcp">Install and update register rtok's MCP server.</param>
/// <param name="Ask">Asks the user; null where nobody can answer, which declines.</param>
public sealed record HostSettings(
    bool DryRun = false,
    bool Yes = false,
    bool Backup = true,
    int BackupFiles = Backups.DefaultKeep,
    bool Mcp = true,
    Prompt? Ask = null
);
