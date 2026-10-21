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

Spec: catalog B17 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T182, T330, T330.1, T330.2, T330.3, T330.3.1, T330.3.2, T330.4, T330.5, T330.5.1, T330.5.2, T330.5.3, T330.5.4, T330.6, T330.7, T338, T339, T340, T341, T342, T343, T344, T391, T479, T577.

Check: Fake-home tests show that credentials, settings and sessions are never removed by default, and that a changed item is skipped.
