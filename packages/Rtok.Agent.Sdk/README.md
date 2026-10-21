# Rtok.Agent.Sdk

The contract every `rtok agents install|update|uninstall <host>` installer shares, whatever
file format the host uses:

- **Reversible.** Before the first write, a file is copied to
  `_backup/<name>.bak-<unix-seconds>[-<n>]` beside it. A copy is taken once per distinct content
  and never overwritten. Generations are capped per file (`[setup] backup_files`, default 5,
  0 keeps all), the newest is never pruned, and a run that changed nothing deletes its own copies.
- **Idempotent.** A second apply reports `no changes` and writes nothing.
- **Dry-runnable.** `--dry-run` produces the same report and touches nothing.
- **Offered, never forced.** Questions go to a prompt the surface supplies; with no terminal
  every question is a no, and `--yes` is the only way to say yes without one.
- **Ownership-checked removal.** rtok's exact entry is removed, an entry the user edited asks
  first, a foreign entry is left alone.

The report is the write gate: `Apply.Writes(report)` is false for `no changes` and for reports
of only `leave …` / `? …` lines, so "nothing changed" and "nothing was written" cannot drift
apart. Writes are atomic and go through symlink chains to the real file
([Pyrlyn.FsKit](../Pyrlyn.FsKit)).

## Install

```bash
dotnet add package Rtok.Agent.Sdk
```

## Use

```csharp
using Rtok.Agent.Sdk;

var run = new BackupRun(keep: 5);
var apply = new Apply { Backup = true, Run = run, Ask = TerminalPrompt };

// A host that reads `mcpServers`: Claude Code's ~/.claude.json, Cursor's ~/.cursor/mcp.json.
var report = ServerEntries.RegisterMcp(apply, path, "rtok", "rtok", ["mcp"]);
// "mcpServers.rtok: rtok mcp" the first time, "no changes" after that.

var removed = ServerEntries.Unregister(
    apply, path, "mcpServers", "rtok", ServerEntries.McpEntry("rtok", ["mcp"]), IsRtokBinary);
// "- mcpServers.rtok", "leave … (not rtok's; remove by hand)",
// or "leave … (changed by you; remove by hand)" when the user edited it and said no.

run.Finish(changed: apply.Writes(report) || apply.Writes(removed));

static bool TerminalPrompt(string question, bool defaultYes)
{
    if (Console.IsInputRedirected) return false;
    Console.Write(Prompts.Line(question, defaultYes));
    return Prompts.Answer(Console.ReadLine(), defaultYes);
}
```

Hosts in other formats (JSONC, TOML) run the same check on a value they parsed themselves with
`Ownership.Judge`, and write through `HostFile.Write`.

Strict-JSON hosts only: `HostFile.ReadJson` refuses a file with comments or trailing commas
with a `file:line:column` message instead of rewriting it, since a rewrite would delete them.
