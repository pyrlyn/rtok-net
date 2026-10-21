---
status: todo
priority: P0
complexity: 4
readiness: 0%
agent:
---

# T13. Hook surface: `rtok hook <event>` for every host

Hooks are the agent-facing surface with the hardest rules: fail open, exit 0, at most 10 ms (rtok D1, D31, D32).

Done when:
- One canonical hook-event table holds the per-host name maps. Host manifests are generated from it, with a drift test.
- Per-host envelope adapters read and write each host's shape: Claude, Codex, Copilot, Cursor, Gemini, Devin, Grok and the rest in the catalog.
- Stdin is capped at 8 MiB before any parse.
- `once_key` processes each tool call once.
- A slow event is logged.
- Store-lock policy: 5 ms, one attempt; a locked store answers `{}` immediately.
- A SessionEnd blocked by the lock still lands, through a detached retry.
- SessionStart, SubagentStart and PreCompact keep their work lean: one write transaction per hook, note bodies read once (rtok T428, T525).
- WorktreeCreate/WorktreeRemove follow D31.

Spec: catalog A2, catalog B1 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T0.6, T2.1, T2.2, T10.11, T46.3, T83.15, T87, T93, T98, T101, T118.1, T129, T130.1, T130.2, T130.3, T170, T178, T200, T201, T204, T237, T245, T262.1, T262.3, T262.4, T262.5, T291, T295, T309, T390, T390.1, T459, T461, T645; open T428, T525 ([open work](../../docs/port/open-work.md)).

Check: The fail-open matrix passes on the AOT binary: 5 hosts × 8 events × {empty, garbage, truncated, non-UTF-8, 1 MiB} stdin, every case exits 0 with the host's no-op reply.
