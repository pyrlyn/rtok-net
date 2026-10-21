---
status: in progress
priority: P1
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
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

Plan:
1. `eng/new-package.sh Pyrlyn.Ignore`; reference `Pyrlyn.FsKit` (`IsUnwalkableRoot`).
2. `GitIgnorePattern` / `IgnoreFile`: gitignore(5) lines (comments, escapes, trailing spaces, `!`, dir-only, anchoring, `**`, brackets) compiled to NonBacktracking regexes with literal and `*.ext` fast paths.
3. `RepoWalker.Walk(root, WalkOptions)`: DFS, sorted, `.git` skipped; precedence overrides > `.ignore` > `.gitignore` (deepest first) > `info/exclude` > global excludes (`core.excludesFile` or XDG default); parent ignore files up to the repo root; a nested repo starts a new git scope; linked worktrees below the root skipped; include (additive, ignore off) and exclude (beats include) overrides; `/` and `$HOME` refused.
4. Tests: pattern unit cases from gitignore(5), and a corpus test that builds fixture repos and compares files to `git ls-files --others --cached --exclude-standard`.
