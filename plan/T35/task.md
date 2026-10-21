---
status: in progress
priority: P1
complexity: 5
readiness: 95%
agent: Claude Code / claude-opus-5-5
---

# T35. Agent hosts: descriptors, MCP module, install, update, remove, list, info

rtok installs into about 45 agent hosts; each host is mostly data plus a small adapter.

Done when:
- Every host in the catalog has a data-driven descriptor on Rtok.Agent.Sdk and the MCP core: variants (cli/desktop, binaries, app paths), presence and `--version` probes, and per-module support with the reason when a module cannot be installed.
- `rtok agents install|update|uninstall|list|info` behave as in the catalog for the MCP module on every host, goose, continue and reasonix included, with `--cli`, `--desktop`, `--all`, `--dry-run` and `--yes`. The other modules come with T35.3 (hooks), T35.4 (proxy base URL), T35.5 (plugin link, skills, the plugin version scheme and `agents outdated`) and T35.6 (desktop restart).
- A support-matrix doc table is generated from the descriptors.
- `agents info` and `agents list` report the real MCP state from Rtok.Mcp.Install's `McpStatus` rows, the same in `--json` and the web model (moved from T34; T43 consumes the web-model part).

The split into T35.3–T35.6 follows the creator's go-ahead: those modules need T13, T27, T36 and T11 from other lanes; each took its tests along.

Tests:
- `Rtok.Agents.Hosts.Tests` integration: for every host descriptor on fake Linux, macOS and Windows homes (`MemFs`), install, install again, update and uninstall leave at most one rtok MCP entry per surface, also across hosts that cross-load each other's files.
- `Rtok.Agents.Hosts.Tests` CsCheck: install then uninstall on generated JSONC, TOML and YAML host files keeps every byte outside rtok's entry.
- `Rtok.Agents.Hosts.Tests` unit: a current MCP entry prints NO_CHANGES and is not rewritten; Goose, Continue and Reasonix files keep their foreign lines and CRLF.
- `Rtok.Agents.Hosts.Tests` unit: an unparseable TOML, YAML or broken strict-JSON host file is refused with a message naming the file and left byte for byte.
- `Rtok.Agents.Hosts.Tests` unit: `--dry-run` writes nothing; an edited rtok entry with no TTY prints `leave … (changed by you; remove by hand)` and `--yes` removes it.
- `Rtok.Agents.Hosts.Tests` unit: when every requested host is absent, install fails with `error: <host> is not installed`.
- `Rtok.Agents.Hosts.Tests` unit: the support-matrix table generated from the descriptors equals the committed table in `docs/agents.md`.
- `Rtok.Agents.Hosts.Tests` unit: with `rtok` not on PATH a warning is printed, and on `MemFs.Windows()` MCP entries carry the absolute `rtok.exe`.
- `Rtok.Agents.Hosts.Tests` Verify: `agents list` and `agents info <host>` text and `--json` show each `McpStatus` row (present, missing, stale with its diff), the same rows the web model reads.

Spec: catalog A19, catalog B3 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T2.3, T4.7, T9.3, T10.1, T10.3, T10.5, T10.7, T10.8, T10.9, T10.10, T11.5, T21.1, T27.0, T34.5, T36.19, T37.0, T44.1, T44.2, T44.3, T44.4, T44.5, T46.1, T46.2, T46.4, T47.1, T48.3, T48.5, T48.6, T48.7, T48.8, T54.1, T71.3, T72, T76, T77, T78, T79, T81, T85, T86, T89, T90, T91.1, T91.2, T94, T96, T97, T99, T100, T112, T114, T115, T116, T117, T118.2, T121, T138, T139, T140, T141, T164, T169, T185, T186, T196, T242.1, T242.2, T242.3, T242.4, T242.5, T243, T244, T246.1, T246.2, T246.3, T246.4, T246.5, T246.6, T249, T271, T275.2, T279.1, T307, T332, T333, T380, T382, T399, T413.1, T413.2, T413.3, T413.4, T413.5, T413.6, T413.7, T413.8, T413.9, T413.10, T413.11, T413.12, T413.14, T413.15, T426, T434, T463, T520, T521, T560, T560.1, T560.2, T569, T570, T611, T625, T626, T627, T628, T629, T636; open T279 ([open work](../../docs/port/open-work.md)).

Fuzz: `agents-install` in `tests/Rtok.Fuzz`: any existing host config file is refused with a clear error or changed only in rtok's own entry.

Check: An install-matrix test against fake homes runs install twice, update and uninstall for every host, with at most one rtok entry per surface.

## Plan

1. `src/Rtok.Agents`: host descriptors (variants, support per module), probes (PATH with PATHEXT, app paths, `RTOK_HOST_SANDBOX`, `--version` with a 3 s limit), modules (MCP now).
2. MCP adapters the core left out: goose and continue (YAML, text edit validated by a parser), reasonix (TOML array of tables).
3. `rtok agents install|update|uninstall|list|info` in Rtok.Cli, text and `--json`.
4. `docs/agents.md` support matrix generated from the descriptors, with a drift test.
5. Install-matrix test over fake homes for every host, variant and OS.
