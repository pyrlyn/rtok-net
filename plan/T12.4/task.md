---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
---

# T12.4. Package Pyrlyn.Ignore: gitignore-correct walking

`search`, `tree`, the graph index and the watcher all walk a repo the way git sees it.

Done when:
- The walker applies `.gitignore` at every level, plus `.ignore`, `.git/info/exclude` and the global excludes file.
- Negation, directory-only patterns and anchoring follow git's rules.
- Nested linked worktrees are skipped.
- Include and exclude overrides work.
- Walking `/` and `$HOME` is refused.

Spec: catalog A5, catalog B11, catalog B15 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: A corpus test checks the walker against `git ls-files --others --cached --exclude-standard` on fixture repos, with no differences.
