---
status: todo
priority: P3
complexity: 2
readiness: 0%
agent:
---

# T35.1. Host: Trae

Trae documents only a project-level `.trae/mcp.json`; rtok had no installer because there is no documented user-level path.

Done when Trae documents a user-level path and an installer exists for it, or the host table says why it is not supported.

Ports: rtok open T413.13 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Tests` unit: the host table has a dated Trae row with sources and either a user-level path or the reason it is not supported.
- `Rtok.Agents.Tests` unit: the generated support-matrix table carries the Trae row.
- `Rtok.Cli.Tests` Verify: without a documented user-level path, `rtok agents install trae` reports "not supported" with the reason and writes nothing.
- `Rtok.Agents.Tests` integration: if an installer is built, Trae joins the T35 install matrix on fake Linux, macOS and Windows homes with one rtok entry in its user-level MCP file.
- `Rtok.Agents.Tests` unit: if an installer is built, install and uninstall keep every foreign server in Trae's MCP file byte for byte.

Fuzz: an installer, if built, joins T35's `agents-install` target.

Check: The host table has a dated Trae row with sources, and an installer test passes if one was built.
