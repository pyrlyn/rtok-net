---
status: in progress
priority: P2
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T57. Build tasks in C# with Cake Frosting instead of bash

The `eng/*.sh` scripts (T3, T12, T53) need bash, jq, 7z, `sha256sum` or `shasum`, so a
Windows contributor needs Git Bash and every script carries its own copy of the same path,
RID and process plumbing. The creator chose Cake Frosting: one C# console project,
`eng/Rtok.Build`, run with `dotnet run --project eng/Rtok.Build -- --target=<task>` on any OS.

Done when:

- `eng/Rtok.Build` (Cake.Frosting) has one task per former script, with the same checks,
  outputs and exit codes: `publish`, `smoke`, `package-release`, `publish-package`,
  `affected-tests`, `check-marketplace`, `new-package`, `fuzz`.
- jq, 7z, `sha256sum`/`shasum` and curl are no longer needed: JSON, zip, tar.gz, SHA-256 and
  downloads come from the BCL. docker (musl), clang (fuzz) and git stay external tools.
- `eng/*.sh` are gone; `ci.yml`, `release.yml`, `packages.yml`, CONTRIBUTING.md,
  `packages/README.md`, `tests/README.md` and `plan/README.md` call the new tasks.
- `packages/Pyrlyn.TreeSitter/native/build-native.sh` stays: MSBuild runs it inside the build,
  where a nested `dotnet run` would be slow and race parallel builds (creator's call). The
  spikes keep their scripts.

Check: each task run on macOS gives the same result as its script did (`publish` +
`smoke` + `package-release` for osx-arm64, `affected-tests` on a diff and `--all`,
`check-marketplace`, `new-package` into a scratch name, `publish-package --dry-run`).

## Plan

1. `eng/Rtok.Build/Rtok.Build.csproj` (Cake.Frosting 6.3.0, JIT only, may write to the
   console) in `rtok.slnx`; CliWrap runs the tools (already pinned), so no `Process.Start`.
2. `BuildContext`: repo root, arguments, RID helpers; one task class per script.
3. Workflows and docs. The lint job's `shellcheck eng/*.sh` step goes with the scripts
   (`build-native.sh` was never under it).
4. Run every task against its old script output, then the repo gates.
