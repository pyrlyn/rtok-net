---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
---

# T19. read plugin: read, search, tree, native Read gate

Reading files is the second largest sink; MCP `read` gives cheaper views and dedups re-reads.

Done when:
- MCP `read` supports these modes: full, lines, map, signatures, stripped, diff, `density:X`. It caps output at 20K chars and archives the cut part.
- Re-reads are deduped by sha256, with a delta mode and per-range dedup.
- `search` and `tree` respect gitignore (Pyrlyn.Ignore) with rtok's caps.
- Allowed roots: the cwd, `allow_paths`, the repo's worktrees, and the client roots. The refusal text matches rtok's.
- A default secret deny list (`.env*`, `.git/config`, `*.pem`, `id_*`) is always enforced over HTTP (rtok S4). A single-file read is also root-checked on the HTTP surface (rtok S1).
- The native Read gate denies a large unranged native Read, with the per-host deny text.

Spec: catalog A5, catalog B11 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T4.2, T4.3, T4.4, T4.5, T4.6, T36.5, T36.6, T50.3, T55.13, T58.1, T59.2, T68.8, T122, T135, T136, T300, T351, T378, T383, T384, T435, T454, T529, T535, T590, T670; open S4 ([open work](../../docs/port/open-work.md)).

Check: Tests cover every mode, dedup and delta, the root and deny-list refusals over stdio and HTTP, and the native Read gate on each host shape.
