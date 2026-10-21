---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T41. Tasks: `rtok task` with disk, GitHub and GitLab adapters

rtok tracks tasks for agents with claims and a ready queue.

Done when:
- TaskId rules and the counter allocator work.
- `rtok task create|list|show|status|next|ready|init|sync|conflict|parallel|export` and the MCP `task_*` tools return the same JSON.
- Adapters: disk (front-matter files, an OS claim lock), GitHub (labels, sub-issues, pacing, Projects v2) and GitLab.
- `worktree add` writes the task list id.

Spec: catalog A22, catalog B7 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T405, T441, T441.1, T441.2, T441.3, T441.4, T441.5, T441.6, T441.7, T441.8, T441.10, T441.11, T441.12, T441.13.1, T441.13.2, T441.13.3, T441.13.4, T441.13.5, T441.13.6, T441.13.7, T442, T541, T586, T601, T615, T904.

Check: Adapter tests run against a temp dir and WireMock fakes of GitHub and GitLab.
