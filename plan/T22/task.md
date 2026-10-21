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

Tests:
- `Rtok.Plugins.Memory.Tests` integration: `mem_save` upserts by project, kind and title with a revision count, and a body change stores the previous title and body in `note_versions` (version = max+1) in the same transaction, except for checkpoint:* and session:* kinds.
- `Rtok.Plugins.Memory.Tests` integration: `mem_search` ranks by FTS5 bm25 with pinned notes first, returns ids, titles and 120-char snippets, never returns retired notes, and bumps uses and last_used on hits.
- `Rtok.Plugins.Memory.Tests` unit: `mem_get` returns the verbatim body and `mem_update` retires, supersedes and pins without ever deleting a note.
- `Rtok.Plugins.Memory.Tests` Verify: `mem_pack` returns the abstract, first-paragraph and body tiers, and its `used_tokens` equals the estimate of the returned text with headers.
- `Rtok.Plugins.Memory.Tests` unit: SessionStart recall stays within `recall_tokens` (200) at priority 10 with one mem_search/mem_get line, and with zero notes prints a line naming the project key.
- `Rtok.Plugins.Memory.Tests` unit: prompt recall (`prompt_recall` 5, priority 11) fuses FTS and file-linked notes through RRF k=60 with ties broken by id, takes at most 2 notes from prompt-named files, and is skipped when the top-N matches the previous turn.
- `Rtok.Plugins.Memory.Tests` unit: a `remember: <text>` prompt saves a kind=user note titled with the first 80 chars.
- `Rtok.Plugins.Memory.Tests` unit: under a fake TimeProvider `half_life_days = 0` gives byte-identical id-desc order and a positive value ranks by `ln(1+uses)*0.5^(age/N)`.
- `Rtok.Plugins.Memory.Tests` unit: an observation recorded twice within 5 s or redelivered is stored once, recall gives at most 3 per session and 2 `obs <id> <title> (<session>)` lines, and `observations_for_files` returns distinct ids.
- `Rtok.Plugins.Memory.Tests` unit: `<private>…</private>` text is stripped from each whole observation field before the 200-char cut.
- `Rtok.Plugins.Memory.Tests` WireMock: with `[plugins.memory.embed]` `provider="http"` recall POSTs to `<url>/embeddings` and fuses vectors through RRF, and with embeddings off no request reaches the fake server.
- `Rtok.Plugins.Memory.Tests` integration: the recall bench of planted, drifted and superseded facts at N = 1, 10, 30, 100 meets rtok's floors (20 of 20 FTS hits, zero superseded ids), and the p29 fixture finds its planted note in hybrid mode.
- `Rtok.Cli.Tests` integration: `rtok memory export` then `memory import` round-trips id, ts, retired, superseded_by and pinned, skips checkpoint:/session: notes, dedupes by body sha256, and a missing import file exits non-zero.
- `Rtok.Cli.Tests` Verify: `rtok memory status --json`, `memory history <id>` (oldest first) and `memory retire|pin|unpin|revise` output, with a revise keeping a global note global.
- `Rtok.Cli.Tests` integration: `rtok memory sync` writes exactly one `<!-- rtok:memory -->` block with `<!--` in titles escaped, leaves the rest of the file byte for byte, `--dry-run` writes nothing, a hand-edited block exits 1 unchanged without `--force`, and a read error other than NotFound aborts.

Spec: catalog A9, catalog B14 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T6.1, T6.2, T6.3, T66.1, T66.2, T69.1, T69.2, T69.3, T69.4, T69.5, T69.6, T202, T203, T292, T293, T294, T304, T308, T361, T373, T374, T417, T419, T452, T460, T472, T501, T538, T539, T548, T549, T550, T551, T552, T553, T562, T563, T564, T565, T566, T617, T618, T666.1, T667, T668, T669, T671.

Check: The memory recall bench (planted, drifted and superseded facts) passes at rtok's level, and the sync guard test refuses a hand-edited block.
