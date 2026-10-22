---
status: todo
priority: P2
complexity: 2
readiness: 0%
agent:
---

# T41.1. MCP `task_*` tools over the `rtok task` JSON

T41 landed `rtok task` with the disk, GitHub and GitLab adapters, but the MCP server did not exist yet. It depends on T15 (the MCP server): once it has a tool registry, the `task_*` tools call the same `TaskCli` code as the CLI, so an agent sees one JSON shape from either surface.

Done when:
- `task_create`, `task_list`, `task_get`, `task_status`, `task_next`, `task_ready`, `task_claim`, `task_release`, `task_dep`, `task_conflict`, `task_parallel` and `task_priority` are MCP tools that print what `rtok task … --json` prints.

Tests:
- `Rtok.Mcp.Tests` unit: `task_create`, `task_list`, `task_get`, `task_status` and `task_next` return the same JSON as the CLI `--json` for the same store.

Spec: catalog A22 in [docs/port/catalog.md](../../docs/port/catalog.md); `plan/T41/done.md` for what T41 left here.

Ports: rtok's MCP task tools (T41's list).

Fuzz: none new; `task-file` already drives the parser both surfaces share.

Check: each `task_*` tool and its `rtok task … --json` command give byte-equal JSON over one temp store.
