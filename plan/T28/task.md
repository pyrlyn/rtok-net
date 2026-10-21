---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T28. Proxy rewriters: archive pointers, toon, json_tree, tools_rewrite

Compress mode rewrites old tool results without touching the system prompt, tools or the last two turns, and stays byte-stable for the prompt cache.

Done when:
- Old large tool results become archive pointers, keyed by (session, tool_use_id) and persisted. `expand` freezes a pointer.
- Skill bodies are archived outside the live zone.
- toon tables and json_tree folding work, each archiving the original first.
- `tools_rewrite`, `compact_schema`, `context_management`, terminal-noise stripping and `live_blobs` work. `live_blobs` never touches image or document data (rtok T55.15).

Spec: catalog A14, catalog A6 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T5.3, T5.4, T11.7, T11.8, T36.1, T36.2, T55.15, T59.5, T61.2, T127, T609; open T55.15 ([open work](../../docs/port/open-work.md)).

Check: Replaying a recorded session twice gives byte-identical requests, every pointer expands back to its original, and the image/document test passes.
