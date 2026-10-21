---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T22. memory plugin: notes, recall, observations, sync

Notes carry knowledge across sessions without re-reading files.

Done when:
- MCP `mem_save`, `mem_search` (FTS5 bm25), `mem_get`, `mem_update` and `mem_pack` behave as in the catalog, with the note lifecycle and version history.
- SessionStart and prompt recall run within their budgets, with RRF over FTS5 and file-linked notes.
- `remember:` prompts save a note.
- These commands work: `rtok memory import/export/status/retire/pin/unpin/revise/history/bench` and `memory sync` with its managed block and hand-edit guard.
- Observations are deduped.
- `<private>` text is stripped.
- Optional embeddings (an OpenAI-compatible HTTP provider) sit behind a flag.

Spec: catalog A9, catalog B14 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T6.1, T6.2, T6.3, T66.1, T66.2, T69.1, T69.2, T69.3, T69.4, T69.5, T69.6, T202, T203, T292, T293, T294, T304, T308, T361, T373, T374, T417, T419, T452, T460, T472, T501, T538, T539, T548, T549, T550, T551, T552, T553, T562, T563, T564, T565, T566, T617, T618, T666.1, T667, T668, T669, T671.

Check: The memory recall bench (planted, drifted and superseded facts) passes at rtok's level, and the sync guard test refuses a hand-edited block.
