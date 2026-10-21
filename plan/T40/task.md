---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T40. Worktrees: add, adopt, list, remove, gc, clean

One task, one branch, one worktree, one PR (rtok D16). rtok manages worktrees for many agents at once.

Done when:
- `rtok worktree add|list|whoami|claim|adopt|remove|gc|clean|migrate` and the MCP tools behave as in the catalog. Worktrees are made through git (ProgressRunner): lock reason v2, squash-aware merge detection, never forcing.
- `add` accepts `<id> <slug>` in one argument (rtok T523).
- A stale claim held by an ended or long-idle agent does not block (rtok T527).
- Host-created worktrees can be adopted, with post-create hooks for Cursor, Kilo, Windsurf and Devin (rtok T289).
- The probes (WorktreeCreate payloads, editors on relative-path worktrees) are re-run (rtok T156, T398).

Spec: catalog A21, catalog B5 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T133, T150, T151, T152, T153, T154, T157, T158, T159, T285, T286, T289.1, T289.2, T289.3, T289.4, T289.5, T410, T411, T412, T418, T423, T424, T453, T524, T656, T660.1, T660.2, T660.3, T660.4, T660.5; open T156, T289, T398, T523, T527 ([open work](../../docs/port/open-work.md)).

Check: Tests on temporary git repos cover add, adopt, remove refusals, the gc decisions and the stale-claim rule.
