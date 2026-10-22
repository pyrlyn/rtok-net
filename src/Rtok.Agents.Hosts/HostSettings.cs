using Rtok.Agent.Sdk;

namespace Rtok.Agents.Hosts;

/// <summary>The <c>[setup]</c> switches one run follows, flags already applied.</summary>
/// <param name="DryRun">Print what would change; write nothing.</param>
/// <param name="Yes">Answer yes to every question (remove an entry the user changed).</param>
/// <param name="Backup">Copy each file before the first write to it.</param>
/// <param name="BackupFiles">Backup generations kept per file; 0 keeps all.</param>
/// <param name="Mcp">Install and update register rtok's MCP server.</param>
/// <param name="Ask">Asks the user; null where nobody can answer, which declines.</param>
/// <param name="HookTimeoutS">The timeout written into each hook entry, in seconds (<c>setup.hook_timeout_s</c>).</param>
/// <param name="Proxy">Install also points the host's model base URL at rtok's proxy (<c>--proxy</c>).</param>
/// <param name="ProxyUrl">rtok's proxy as clients dial it (<c>[proxy] bind</c> and <c>port</c>).</param>
/// <param name="Warn">Takes a warning line as it happens (stderr); null drops it.</param>
public sealed record HostSettings(
    bool DryRun = false,
    bool Yes = false,
    bool Backup = true,
    int BackupFiles = Backups.DefaultKeep,
    bool Mcp = true,
    Prompt? Ask = null,
    long HookTimeoutS = 5,
    bool Proxy = false,
    string ProxyUrl = "http://127.0.0.1:8790",
    Action<string>? Warn = null
);
