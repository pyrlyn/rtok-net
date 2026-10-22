# T41 done

`rtok task` tracks tasks for agents: ids, a ready queue and claims, with disk, GitHub and GitLab adapters and a one-way export to GitHub and Linear. The MCP `task_*` tools and the task list id in `worktree add` wait on other lanes and moved to T41.1 (T15) and T41.2 (T16).

## What was done

- **`src/Rtok.Tasks`** (new):
  - `TaskId`, statuses with their aliases, the task model and `TaskJson`. The CLI and, later, the MCP tools share this JSON.
  - `TaskAllocator` gives each id from `task_counters` in one exclusive transaction; `seed` only raises a counter.
  - `TaskProject` holds the ready queue, claims, dependencies with cycle refusal, conflicts, parallel marks and priority.
  - `TaskCli` holds every command, and `TaskInit` writes `[tasks]` into `.rtok.toml`.
- **`ProjectKey`** moved from `Rtok.Config` to `Rtok.Tasks`:
  - It reads the origin through `Rtok.Git`'s `GitRepository.Open` (T24, N21), the one place LibGit2Sharp opens a repository; `Repository.Discover`, which finds nothing under NativeAOT, is not used.
  - The key is origin `host/owner/repo`, else the main checkout path, so a linked worktree shares its main checkout's key and counters.
- **Adapters:**
  - **Disk:** `<id> - <slug>.md` files with YAML front matter (SharpYaml), written through temp + rename. Done and closed tasks move to `tasks/done/`. A file whose name and front-matter id disagree is refused untouched. The claim lock `tasks/.claim.lock` is an exclusive OS lock on the file (flock on Unix): a leftover file blocks no one, and the file is never unlinked.
  - **GitHub:** `R12. Title` issues labelled `rtok` and `rtok:<id>`, sub-issues, and the Projects v2 Status over GraphQL. The token comes from GH_TOKEN, then GITHUB_TOKEN, then `gh auth token`.
  - **GitLab:** REST v4 with `status::` labels.
  - **Shared `RemoteClient`:** writes are paced 1 s apart, a `retry-after` or rate-limit reset of up to 1 min is waited out, Link pagination stays within the base URL, and errors never carry the token.
- **`rtok task export github|linear`:** mirrors the disk tasks one way and is idempotent (done→completed, closed→not_planned). `--dry-run` sends no write.
- **Store (`Rtok.Store`):** `task_counters` and `task_claims` queries. `ResolveAgent` now orders before `Take`, so EF precompiles it without a warning.
- **CLI (`Rtok.Cli`):** `rtok task init|create|list|show|status|next|ready|claim|release|dep|priority|conflict|parallel|sync|export`. `docs/cli/commands.json`, the help pages, the man page and the completion scripts are regenerated. `ParityMap` (T43) puts `task list|next|ready|show|status|sync` on the Tasks page and exempts the writing commands with their reasons; T43 wires the page's source.
- **Fuzz:** a `task-file` target with a corpus of 7 seeds. A parsed id prints back to itself; a parsed file renders to a file that parses again and renders byte-identical (render is a fixpoint). Comments, quoting and CRLF are not kept, so the raw input is not compared. The fuzzer found an `ArgumentNullException` on an empty list item (`- # note`). Such an item is now refused with a `TaskException`, covered by a unit test and corpus seed 07.
- **Test infrastructure:** `FakeService` binds WireMock to 127.0.0.1 and retries on a taken port. On `localhost`, a port taken by another test process between the probe and the bind answered with dropped connections in full solution runs.

## RIDs (N21: LibGit2Sharp ships native libgit2)

| RID | Where | Result |
| --- | --- | --- |
| osx-arm64 | this Mac, the `publish` build task (T57) | publishes. The AOT `rtok task init` takes prefix `Z` from origin `Me/Zeta-App` through libgit2, also from a linked worktree (next id `Z2`); `create` and `list --json` work on the store. |
| osx-x64 | this Mac, the `publish` build task, run under Rosetta | publishes; the same smoke run passes. |
| linux-x64, linux-musl-x64, linux-arm64 | `ci.yml` | CI only, not yet run. |
| win-x64 | `ci.yml` | CI only, not yet run. |

## How it was checked

- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3`.
- **Tests (`Rtok.Tasks.Tests`):** 128 test methods over TaskId (CsCheck round-trip and refusals), the allocator, the disk adapter on temp dirs, WireMock fakes of GitHub (with Projects v2) and GitLab, `RemoteClient` pacing and rate limits on a stepping clock, export, sync, init and `TaskCli` end to end.
- **Store tests:** `Rtok.Store.Tests` covers the task queries, and the AOT lane (`Rtok.AotTests`) adds them to its store test.
- **`Rtok.Cli.Tests` (Verify):** the `--json` transcript of `init`, `create`, `conflict`, `parallel`, `list`, `show`, `next`, `ready`, `status` and `sync`. It runs through `TaskCli` in the JIT test host, because a Release build of the binary that is not AOT-published cannot build the store's EF model.

Differences from the test list in `task.md`:

- The allocator runs with eight connections on parallel threads rather than four processes. SQLite's exclusive transaction is the same across connections.
- The claim lock test takes the lock twice in one process. The OS lock is per handle, and a leftover file or a stale timestamp does not take it.

Known and not from T41: `HookResidentTests.A_hung_resident_fails_within_the_budget` (T14) is flaky under load in full solution runs (see the shared lane notes).
