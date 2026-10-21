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

Tests:
- `Rtok.Plugins.Read.Tests` Verify: `read` output on one fixture file in each mode: full, lines, map, signatures, stripped, diff and `density:0.3`.
- `Rtok.Plugins.Read.Tests` unit: a read over 20K chars is cut to at most 20K chars including the marker, keeps head and tail, names an archive id and writes one `read_cap` Measurement row.
- `Rtok.Plugins.Read.Tests` CsCheck: for any file content and range, `expand` of a capped read's archive id returns the original bytes byte for byte.
- `Rtok.Plugins.Read.Tests` unit: an unchanged re-read returns `unchanged since <archive_id> (N lines)`, and a PostToolUse Edit or Write on that path invalidates the dedup.
- `Rtok.Plugins.Read.Tests` unit: after a change, delta mode returns a unified diff against the archived read when it is at most `delta_max_ratio` 0.6 of the file, and the full read otherwise.
- `Rtok.Plugins.Read.Tests` unit: a `mode=lines` re-read is deduped per range and is never answered with a whole-file pointer.
- `Rtok.Plugins.Read.Tests` integration: in a temp repo `search` and `tree` skip gitignored files, `search` caps at `max=50` hits, 120-char snippets and `search_max_bytes` 1 MiB per file, and search_cap/tree_cap rows are written only when output is cut.
- `Rtok.Plugins.Read.Tests` integration: the cwd, `allow_paths`, a worktree of the repo and a client `roots/list` root are allowed, and a scratchpad path is refused with `path outside cwd: <p> (outside this project and its worktrees; read it with the host's own file tool)`.
- `Rtok.Plugins.Read.Tests` integration: a `..` path through a symlink resolves with symlink semantics and cannot leave the allowed roots.
- `Rtok.Mcp.Tests` integration: the root refusal text is identical over stdio and HTTP for `read`, `search` and `tree`.
- `Rtok.Mcp.Tests` integration: over HTTP, `read` and `search` refuse `.env.local`, `.git/config`, `key.pem` and `id_ed25519` even with `plugins.read.deny_globs` emptied, and a single-file `read` outside the roots is refused (rtok S1, S4).
- `Rtok.Plugins.Read.Tests` Verify: the native Read gate deny for an unranged Read of a 64K file on the claude, gemini and another host shape, each with its own deny text and a cost Measurement row.
- `Rtok.Plugins.Read.Tests` unit: a native Read with `limit` 1..300 passes, `limit` 301 is denied, a file under `read.native_max_bytes` 32K or edited in the last 5 turns passes, and Copilot's `path` key is read like `file_path`.
- `Rtok.Plugins.Read.Tests` unit: a gate input with a missing file or malformed payload allows the native Read unchanged (fail open).

Spec: catalog A5, catalog B11 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T4.2, T4.3, T4.4, T4.5, T4.6, T36.5, T36.6, T50.3, T55.13, T58.1, T59.2, T68.8, T122, T135, T136, T300, T351, T378, T383, T384, T435, T454, T529, T535, T590, T670; open S4 ([open work](../../docs/port/open-work.md)).

Check: Tests cover every mode, dedup and delta, the root and deny-list refusals over stdio and HTTP, and the native Read gate on each host shape.
