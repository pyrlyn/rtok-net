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

Spec: catalog A10, catalog B14 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T2.5, T58.2, T59.6, T62.2, T71.2, T306, T375; open T131 ([open work](../../docs/port/open-work.md)).

Check: Transcript fixtures give the expected checkpoint, handoff and brief texts, and the hook stays inside the T13 matrix and latency gate.
