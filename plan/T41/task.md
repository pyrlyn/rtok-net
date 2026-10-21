---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T41. Tasks: `rtok task` with disk, GitHub and GitLab adapters

rtok tracks tasks for agents with claims and a ready queue.

Done when:
- TaskId rules and the counter allocator work.
- `rtok task create|list|show|status|next|ready|init|sync|conflict|parallel|export` and the MCP `task_*` tools return the same JSON.
- Adapters: disk (front-matter files, an OS claim lock), GitHub (labels, sub-issues, pacing, Projects v2) and GitLab.
- `worktree add` writes the task list id.

Tests:
- `Rtok.Tasks.Tests` CsCheck: TaskId parse and format round-trip: a 1-8 letter prefix, a dotted path of depth at most 2, case-insensitive input, upper-case output and numeric order (`A2` before `A10`).
- `Rtok.Tasks.Tests` unit: a leading zero, a 0, depth 3 or a 9-letter prefix is rejected, and the aliases pending, in_progress and completed map to their statuses.
- `Rtok.Tasks.Tests` integration: the allocator on a temp SQLite gives distinct ids when four processes create at once, `seed` only raises a counter, and ids are never reused.
- `Rtok.Tasks.Tests` unit: the project key is origin `host/owner/repo`, else the main checkout path, and a local filesystem remote resolves from the main checkout.
- `Rtok.Tasks.Tests` integration: the disk adapter writes `<id> - <slug>.md` with YAML front matter through temp + rename, moves done and closed tasks to `tasks/done/`, and errors when the filename id differs from the front-matter id.
- `Rtok.Tasks.Tests` integration: two processes claim at once and only one holds the OS lock on `.claim.lock`, which is never unlinked.
- `Rtok.Tasks.Tests` unit: finishing a parent with active subtasks fails naming them unless `--force`, and conflict markers are symmetric.
- `Rtok.Tasks.Tests` WireMock: the GitHub adapter creates `R12. Title` issues labelled `rtok` and `rtok:<id>`, links subtasks as sub-issues, and sets Projects v2 Status over GraphQL.
- `Rtok.Tasks.Tests` WireMock: GitHub writes are paced 1 s apart, a `retry-after` is waited out up to 1 min (fake clock), and the token comes from GH_TOKEN, then GITHUB_TOKEN, then `gh auth token` (fake runner).
- `Rtok.Tasks.Tests` WireMock: the GitLab adapter drives REST v4 with `status::` labels.
- `Rtok.Tasks.Tests` WireMock: `export github` is idempotent (done→completed, closed→not_planned), and `--dry-run` sends no request.
- `Rtok.Cli.Tests` Verify: `rtok task create|list|show|status|next|ready|sync|conflict|parallel --json` output over a temp disk store.
- `Rtok.Mcp.Tests` unit: `task_create`, `task_list`, `task_get`, `task_status` and `task_next` return the same JSON as the CLI `--json` for the same store.
- `Rtok.Cli.Tests` integration: `rtok task init --adapter disk --prefix A` writes `.rtok.toml`, and the next call reads it for that cwd.
- `Rtok.Worktree.Tests` integration: `worktree add` writes `env.CLAUDE_CODE_TASK_LIST_ID=<project>-<task>` into `.claude/settings.local.json`, keeps its other bytes, and still creates the worktree when that file is unparseable.

Spec: catalog A22, catalog B7 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T405, T441, T441.1, T441.2, T441.3, T441.4, T441.5, T441.6, T441.7, T441.8, T441.10, T441.11, T441.12, T441.13.1, T441.13.2, T441.13.3, T441.13.4, T441.13.5, T441.13.6, T441.13.7, T442, T541, T586, T601, T615, T904.

Check: Adapter tests run against a temp dir and WireMock fakes of GitHub and GitLab.
