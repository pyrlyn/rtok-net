---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T23. Checkpoint, handoff and the spawn brief

Compaction must not lose the working state, and a sub-agent should start with the context it needs.

Done when:
- PreCompact stores the checkpoint from the transcript, streamed with bounded state, as notes.
- SessionStart(compact) and PostCompact inject it within `checkpoint_tokens`.
- SessionEnd and startup restore work.
- MCP `handoff` returns its digest.
- The SubagentStart spawn brief carries symbol line ranges. Its cost is recorded as a negative Measurement, and `rtok stats` splits the sub-agent re-read share by brief or no brief (rtok T131).

Tests:
- `Rtok.Plugins.Checkpoint.Tests` Verify: PreCompact on transcript fixtures stores a kind=checkpoint note with the last 20 prompts (300 chars each), touched paths with per-file actions, the last 8 error lines, loaded skills and live archive ids newest first.
- `Rtok.Plugins.Checkpoint.Tests` unit: a generated transcript of 100k lines is streamed with state bounded to 20 prompts and 8 errors.
- `Rtok.Plugins.Checkpoint.Tests` unit: the prefilter tolerates spaced JSON and mixed blocks and skips meta, compact-summary, non-human and wrapper prompts (`<task-notification>`, `<ci-monitor-event>`, `<local-command-*>`, `[Request interrupted by user`).
- `Rtok.Plugins.Checkpoint.Tests` unit: `<private>…</private>` text is stripped from the checkpoint before store and before restore.
- `Rtok.Plugins.Checkpoint.Tests` unit: a missing, unreadable or garbage transcript stores nothing and the hook returns the no-op reply.
- `Rtok.Plugins.Checkpoint.Tests` unit: SessionStart(source=compact) and PostCompact inject the checkpoint within `checkpoint_tokens` (400), byte-identical on a repeat and with the same bytes for Codex, Cursor, Gemini and Copilot as for Claude.
- `Rtok.Plugins.Checkpoint.Tests` integration: SessionEnd writes `session:<id>` to a temp store and SessionStart(source=startup) restores the newest one.
- `Rtok.Plugins.Checkpoint.Tests` Verify: MCP `handoff(budget_tokens)` returns note titles, live archive ids with tool and bytes once each in decision order, touched paths and the last N prompts within the budget, and its description is at most 40 tokens.
- `Rtok.Plugins.Checkpoint.Tests` unit: with the spawn brief off or an empty ledger SubagentStart is a passthrough, and other events are untouched.
- `Rtok.Plugins.Checkpoint.Tests` Verify: the SubagentStart brief carries pointers with symbol line ranges and an expand id under its budget, with the already-read state scoped per context window and agent_id, including Copilot reads.
- `Rtok.Plugins.Checkpoint.Tests` unit: a fired brief writes exactly one negative Measurement of its cost, and no brief writes none.
- `Rtok.Measure.Tests` unit: `rtok stats` splits the sub-agent re-read share into brief and no-brief rows by the spawn-brief marker.
- `Rtok.Smoke` smoke: the published `rtok hook` for PreCompact, PostCompact and SubagentStart exits 0 with the no-op reply on empty, garbage and truncated stdin.
- `Rtok.Latency` bench: new `pre_compact.json` and `subagent_start.json` hook fixtures stay under the 10 ms median gate.

Spec: catalog A10, catalog B14 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T2.5, T58.2, T59.6, T62.2, T71.2, T306, T375; open T131 ([open work](../../docs/port/open-work.md)).

Check: Transcript fixtures give the expected checkpoint, handoff and brief texts, and the hook stays inside the T13 matrix and latency gate.
