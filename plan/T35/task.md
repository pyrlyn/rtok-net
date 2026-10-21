---
status: todo
priority: P1
complexity: 5
readiness: 0%
agent:
---

# T35. Agent hosts: install, update, remove, list, info, outdated

rtok installs into about 45 agent hosts; each host is mostly data plus a small adapter.

Done when:
- `rtok agents install|update|uninstall|list|info|outdated` behave as in the catalog for every host listed there, as data-driven descriptors on Rtok.Agent.Sdk and the MCP core. Covered: hooks, MCP, proxy base URL, host plugin link, skills, desktop restart, `--dry-run`, `--yes`.
- There is one plugin version scheme for every install source: a version file and a receipt, a SemVer compare, and `agents outdated --exit-code 10` (rtok T279).
- A support-matrix doc table is generated from the descriptors.
- `agents info` and `agents list` report the real MCP state from Rtok.Mcp.Install's `McpStatus` rows, the same in `--json` and the web model (moved from T34; T43 consumes the web-model part).

Spec: catalog A19, catalog B3 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T2.3, T4.7, T9.3, T10.1, T10.3, T10.5, T10.7, T10.8, T10.9, T10.10, T11.5, T21.1, T27.0, T34.5, T36.19, T37.0, T44.1, T44.2, T44.3, T44.4, T44.5, T46.1, T46.2, T46.4, T47.1, T48.3, T48.5, T48.6, T48.7, T48.8, T54.1, T71.3, T72, T76, T77, T78, T79, T81, T85, T86, T89, T90, T91.1, T91.2, T94, T96, T97, T99, T100, T112, T114, T115, T116, T117, T118.2, T121, T138, T139, T140, T141, T164, T169, T185, T186, T196, T242.1, T242.2, T242.3, T242.4, T242.5, T243, T244, T246.1, T246.2, T246.3, T246.4, T246.5, T246.6, T249, T271, T275.2, T279.1, T307, T332, T333, T380, T382, T399, T413.1, T413.2, T413.3, T413.4, T413.5, T413.6, T413.7, T413.8, T413.9, T413.10, T413.11, T413.12, T413.14, T413.15, T426, T434, T463, T520, T521, T560, T560.1, T560.2, T569, T570, T611, T625, T626, T627, T628, T629, T636; open T279 ([open work](../../docs/port/open-work.md)).

Check: An install-matrix test against fake homes runs install twice, update and uninstall for every host, with at most one rtok entry per surface.
