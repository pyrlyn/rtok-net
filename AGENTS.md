# rtok-net — instructions for agents

**What.** rtok rewritten in C# on .NET 10: one tool that cuts tokens for coding agents; every method is a plugin. Surfaces: `rtok hook <event>` (and the tiny `rtok-hook` client), `rtok mcp`, `rtok proxy`. Release is NativeAOT, Debug is JIT.

**Read first.** `plan/README.md` (decisions N1–N18, package map, solution layout), `research.md`, and the catalog sections your task links in `docs/port/catalog.md`. No unplanned work: only tasks in `plan/`.

**Workflow.** Follow the project-plan skill. Claim a task in its `task.md` (`status: in progress`, your `<provider> / <model>` in `agent`, a short execution plan). One task, one git worktree under `.worktrees/<id>`, one branch `<id>-<slug>` from local `main`. Finish: `dotnet build -c Debug -warnaserror` and `dotnet test` green, commit `<id>: <title>`, write `plan/<id>/done.md`, rebase on `main`, then fast-forward `main` to your branch. There is no remote: never push.

**Rules.** No Rust code anywhere. NuGet first; missing pieces become own packages in `packages/` (`Pyrlyn.*` generic, `Rtok.*` rtok contracts). Fail open: the hook exits 0 within 10 ms, unmodified. Lossless: everything shortened comes back with `expand`. No Measurement row, no saving claim. Injections budgeted and byte-stable. Host configs: change only rtok's own entry, the rest byte for byte. No real agents in tests. One logic, one place. New dependency: one-line reason in the commit. Library code must stay AOT-clean (`IsAotCompatible`). The human is the only author: no `Co-Authored-By` or "Generated with" lines.
