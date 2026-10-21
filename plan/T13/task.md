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

Tests:
- `Rtok.Hooks.Tests` unit: the canonical hook-event table gives each host a distinct name per event and no event maps to two names on one host.
- `Rtok.Hooks.Tests` unit: every `plugins/*/hooks.json` equals the manifest generated from the event table (drift test).
- `Rtok.Hooks.Tests` Verify: the canonical input read and the reply written for Claude, Codex, Copilot, Cursor, Gemini, Devin and Grok envelopes on PreToolUse, PostToolUse, UserPromptSubmit and SessionStart.
- `Rtok.Hooks.Tests` CsCheck: any stdin bytes (empty, garbage, truncated JSON, non-UTF-8) give exit 0 and the host's no-op reply, never an `updatedInput` or deny.
- `Rtok.Hooks.Tests` unit: a stdin of 8 MiB plus one byte answers `{}` with one stderr line before any JSON parse, and `core.hook_max_input_bytes` moves the cap.
- `Rtok.Hooks.Tests` integration: the same `<event>:<tool_use_id>` sent twice runs the plugins once and writes one measurements row.
- `Rtok.Hooks.Tests` unit: an event slower than `[hook] max_ms` logs one warn line, and `max_ms = 0` logs none.
- `Rtok.Hooks.Tests` integration: with the SQLite store write-locked by another connection, a hook answers `{}` after one 5 ms attempt and writes nothing.
- `Rtok.Hooks.Tests` integration: a SessionEnd blocked by the lock hands stdin to a detached retry that writes `ended_at` once the lock is released.
- `Rtok.Hooks.Tests` integration: SessionStart, SubagentStart and PreCompact with 40 notes in the store open one write transaction and read each note body once.
- `Rtok.Hooks.Tests` integration: on a temp git repo WorktreeCreate prints the new worktree path, and WorktreeRemove of a dirty worktree refuses without forcing.
- `Rtok.Latency` bench: the SessionStart, SubagentStart and PreCompact fixtures with 40 notes stay under the 10 ms median.
- `Rtok.Smoke` smoke: the published `rtok hook <event> --host <id>` exits 0 with the no-op reply for every `tests/fixtures/hooks` event while the store is locked.

Spec: catalog A2, catalog B1 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T0.6, T2.1, T2.2, T10.11, T46.3, T83.15, T87, T93, T98, T101, T118.1, T129, T130.1, T130.2, T130.3, T170, T178, T200, T201, T204, T237, T245, T262.1, T262.3, T262.4, T262.5, T291, T295, T309, T390, T390.1, T459, T461, T645; open T428, T525 ([open work](../../docs/port/open-work.md)).

Fuzz: `hook-io` in `tests/Rtok.Fuzz`: stdin bytes for each host adapter and event; the reply is the host's no-op or valid JSON, and no exception escapes.

Check: The fail-open matrix passes on the AOT binary: 5 hosts × 8 events × {empty, garbage, truncated, non-UTF-8, 1 MiB} stdin, every case exits 0 with the host's no-op reply.
