---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T35.2. More agent hosts

Popular agents rtok does not install into yet each get a sub-task (rtok T413).

Done when each new host has:
- a research row with sources;
- a descriptor;
- a docs row;
- an install-matrix test against a fake home.

Ports: rtok open T413 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Tests` unit: every descriptor has a research row with at least one source, and every research row has a descriptor or a "not supported" reason.
- `Rtok.Agents.Tests` integration: each new host joins the T35 install matrix: install, install again, update and uninstall on fake Linux, macOS and Windows homes leave at most one rtok entry per surface.
- `Rtok.Agents.Tests` CsCheck: install and uninstall on the new host's config format keep every byte outside rtok's entry.
- `Rtok.Agents.Tests` unit: a surface the host does not document reports "not supported" with a reason and writes nothing.
- `Rtok.Agents.Tests` unit: with the host's binary or bundle absent, install fails with `error: <host> is not installed`.
- `Rtok.Agents.Tests` unit: the generated support-matrix table has a row per new host and matches the committed docs table.
- `Rtok.Mcp.Install.Tests` unit: each new host's `McpSpec` rows pass the `McpHostsTests` cycle (install, again, update, status, remove).

Fuzz: each new host joins T35's `agents-install` target.

Check: Each added host passes the T35 install-matrix test.
