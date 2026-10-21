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

Tests:
- `Rtok.Worktree.Tests` CsCheck: the `git worktree list --porcelain -z` parser reads back every record (path, head, branch, detached, bare, locked, prunable), paths with spaces and newlines included.
- `Rtok.Worktree.Tests` CsCheck: lock reason v2 `<owner> | <task> | <date> | agent <uuid>` round-trips, and the 3-field form still parses.
- `Rtok.Worktree.Tests` integration: on a temp git repo `worktree add` makes `<repo>-<task>` on branch `<task>-<slug>` with a v2 lock, prints only the path, and never passes `--force`.
- `Rtok.Worktree.Tests` integration: `worktree add "<id> <slug words>"` splits one argument into id and dashed slug, and a retry of a landed add returns the existing path.
- `Rtok.Worktree.Tests` integration: `[worktree] enabled=false` fails every worktree command and drops the worktree MCP tools.
- `Rtok.Worktree.Tests` integration: `remove` refuses a dirty or untracked worktree, a foreign lock and the caller's cwd with exit 1 and the reason.
- `Rtok.Worktree.Tests` integration: `remove` deletes the branch only when merged, a squash merge included, and a failed branch delete still exits 0 with `removed; branch kept: <git msg>`.
- `Rtok.Worktree.Tests` unit: `decide()` keeps a live agent's worktree unless it is done and idle past `--idle`, keeps one of unknown age with "age unknown: …", drops the record of an `rm -rf`ed one, and reclaims a foreign lock on a merged, clean, idle one after `--stale-lock`.
- `Rtok.Worktree.Tests` integration: `gc` without `--yes` is a dry run that changes nothing on disk.
- `Rtok.Worktree.Tests` integration: `clean --idle` removes only idle CACHEDIR.TAG-signed caches and never the caller's own worktree.
- `Rtok.Worktree.Tests` unit: a claim held by an ended agent or one last seen longer than `stale_lock` (7d) ago blocks neither `remove` nor `adopt`, and a live holder's refusal says how long ago it was seen.
- `Rtok.Worktree.Tests` integration: `adopt` takes the task from `--task`, else the old lock, else the branch's first `-` segment, and with 0 or several live agents parks a `worktree_pending` claim that the next SessionStart in the worktree completes.
- `Rtok.Agents.Tests` integration: `agents install <cursor|kilo|windsurf|devin> --project` writes rtok's post-create entry and leaves the rest of each host file byte for byte.
- `Rtok.Mcp.Tests` integration: `worktree_add`, `worktree_list`, `worktree_remove` and `worktree_adopt` bind to the session's linked agent and error when none is linked.
- `Rtok.Hooks.Tests` Verify: each WorktreeCreate/WorktreeRemove payload captured by the re-run probe, kept under `tests/fixtures/hooks/`, parses and gets the path reply.

Spec: catalog A21, catalog B5 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T133, T150, T151, T152, T153, T154, T157, T158, T159, T285, T286, T289.1, T289.2, T289.3, T289.4, T289.5, T410, T411, T412, T418, T423, T424, T453, T524, T656, T660.1, T660.2, T660.3, T660.4, T660.5; open T156, T289, T398, T523, T527 ([open work](../../docs/port/open-work.md)).

Fuzz: `worktree-arg` in `tests/Rtok.Fuzz`: any `<id> <slug>` argument gives a valid git ref name or a refusal.

Check: Tests on temporary git repos cover add, adopt, remove refusals, the gc decisions and the stale-claim rule.
