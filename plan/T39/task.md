---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T39. Agent junk cleanup: `rtok agents junk`

Agents leave caches, logs and temp files behind; rtok clears only what is documented as safe (rtok D36).

Done when:
- `junk list` and `junk clear` behave as in the catalog: classes safe, review, explicit and never; documented paths only; re-checks before each removal; dry run by default; `--trash` via Pyrlyn.Trash; the exit codes.

Tests:
- `Rtok.Agents.Tests` unit: each kind maps to its class (safe, review, explicit, never), a package lockfile is never a `locks` item, and credentials, settings, snapshots, plugins and `rtok.db`/`-wal`/`-shm` are never planned under any flags.
- `Rtok.Agents.Tests` integration: on a fake home with every host's documented paths, `junk clear --yes` removes only safe items, and credentials, settings and sessions stay.
- `Rtok.Agents.Tests` unit: an undocumented path is listed as "not documented: not cleared" and never planned, while a valid CACHEDIR.TAG dir and an `[agents.junk] extra` path are.
- `Rtok.Agents.Tests` unit: XDG_CACHE_HOME, CLAUDE_CONFIG_DIR and CODEX_HOME move the scanned paths.
- `Rtok.Agents.Tests` integration: the re-check before removal skips an item changed since the plan ("changed since plan"), a symlink, a protected path, a nested lockfile and an item inside the 60 s settle.
- `Rtok.Agents.Tests` unit: with the agent running (fake process seam), temp, locks, swap and index are skipped, and a session inside the 10-min in-use guard or younger than `stale_session_days` is kept.
- `Rtok.Agents.Tests` integration: `junk clear` without `--yes` removes nothing, and an empty plan prints "Nothing to clear" and exits 0.
- `Rtok.Agents.Tests` integration: clear exits 0 when everything planned was cleared, 1 when a planned item was not removed and 2 on a usage error, and keeps the top folder and CACHEDIR.TAG.
- `Rtok.Agents.Tests` unit: `--trash` hands each item to a Pyrlyn.Trash fake and deletes nothing itself.
- `Rtok.Agents.Tests` integration: sizes count allocated blocks, a hard link once and a folder shared by two agents once (Unix).
- `Rtok.Agents.Tests` unit: `[agents.junk]` keys are range-checked, and `RTOK_AGENTS_JUNK_STALE_SESSION_DAYS` overrides `stale_session_days`.
- `Rtok.Cli.Tests` Verify: `rtok agents junk list` text (OSC 8 links, 1-decimal units at 1KB=1024) and `--json` with `agents[]{name,id?,folders[]{path,size_bytes,kinds[]},freed_default_bytes,freed_review_bytes},totals`.

Spec: catalog B17 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T182, T330, T330.1, T330.2, T330.3, T330.3.1, T330.3.2, T330.4, T330.5, T330.5.1, T330.5.2, T330.5.3, T330.5.4, T330.6, T330.7, T338, T339, T340, T341, T342, T343, T344, T391, T479, T577.

Check: Fake-home tests show that credentials, settings and sessions are never removed by default, and that a changed item is skipped.
