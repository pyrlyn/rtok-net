---
status: todo
priority: P2
complexity: 1
readiness: 0%
agent:
---

# T41.2. `worktree add` writes the task list id

T41 landed rtok's task ids and project keys, but `rtok worktree add` did not exist yet. It depends on T40 (worktrees): when it creates a worktree for a task, it points Claude Code's task list at that task, so the agent in the worktree shares the plan.

Done when:
- `worktree add` for a task writes `env.CLAUDE_CODE_TASK_LIST_ID=<project>-<task>` into the worktree's `.claude/settings.local.json`, keeping every other byte, and still creates the worktree when that file does not parse.

Tests:
- `Rtok.Worktree.Tests` integration: `worktree add` writes `env.CLAUDE_CODE_TASK_LIST_ID=<project>-<task>` into `.claude/settings.local.json`, keeps its other bytes, and still creates the worktree when that file is unparseable.

Spec: catalog A22, catalog B7 in [docs/port/catalog.md](../../docs/port/catalog.md); `plan/T41/done.md` for what T41 left here.

Ports: rtok's task list id in `worktree add` (T41's list).

Fuzz: none new; `worktree-arg` (T40) covers the arguments.

Check: after `worktree add` for a task, `.claude/settings.local.json` holds the task list id and its other bytes are unchanged.
