---
status: in progress
priority: P1
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T34. MCP install core: per-host McpSpec data, D33

MCP registration differs per host only in data, so one core does it for all of them (rtok T275, T277, T278).

Done when:
- One McpSpec table per host gives the paths per surface, the format (JSON, JSONC, TOML), key path, name, command form, entry shape and duplicate-name behaviour.
- One core reads, writes and reports status (present, missing or stale with a diff) through an FS seam.
- install and update always write rtok's entry; only remove takes it out (D33).
- `agents info` and `agents list` report the real MCP state, the same in `--json` and the web model.
- An unreadable host config is refused, not replaced.

Spec: catalog A19, catalog B3 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T277.1, T652; open T275, T277, T278 ([open work](../../docs/port/open-work.md)).

Check: A table-driven virtual-FS test runs install, update, status and remove for every host's spec.

## Plan

1. `src/Rtok.Mcp.Install` (library, AOT-clean): `McpSpec` (host, surface, client, path, format, key path, server, launch, entry shape, duplicate-name behaviour, plugin serves) and `McpHosts.For(env)`, one table row per host surface.
2. `McpConfig`: read, write and remove rtok's entry through `IFileSystem` (rtok D29). Strict JSON through `HostFile`, JSONC through Pyrlyn.Jsonc, TOML through Tomlyn's syntax tree. A file that cannot be read or parsed is refused.
3. `McpInstaller.Apply(spec, install|update|remove, Apply)`: install and update always write, remove goes through `Ownership.Judge` (D33).
4. `McpStatus`: present, missing or stale with a diff, as an `McpRow` with a source-generated JSON context, for `agents info/list --json` and the web model.
5. `tests/Rtok.Mcp.Install.Tests`: the table-driven MemFs test over every host's spec, plus the rtok-mcp cases.
