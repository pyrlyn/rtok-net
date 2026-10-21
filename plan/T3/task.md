---
status: in progress
priority: P0
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T3. CI and release pipeline per OS

NativeAOT cannot cross-compile between operating systems, so CI and releases need one runner per OS.

Done when:
- CI runs per OS: macOS arm64, Ubuntu x64, Ubuntu arm64 and Windows x64. Each runner does all of this:
  - builds with `-warnaserror` (analyzers and IL warnings);
  - runs the JIT tests;
  - publishes `rtok` and `rtok-hook` with AOT for its RIDs (`osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `win-x64`);
  - runs the AOT tests, the smoke tests and the hook latency gate.
- Further gates: NuGet vulnerability check (`dotnet list package --vulnerable`), CSharpier check, and a duplication check.
- GitHub Actions are pinned to commit SHAs (rtok I-S2).
- Tests run only for the projects a change touches, with a full run on main (rtok T470 idea).
- One NuGet cache per runner, and lint runs on one OS only (rtok T261 idea).
- Releases are archives per RID holding:
  - both binaries;
  - `share/man/man1`, `share/completions/*`;
  - `plugins/`, `skills/`, `rules/`, `modes/`.
- The ketch manifest links both `rtok` and `rtok-hook`; mind the NTFS sort order, where `rtok-hook.exe` sorts first. The Claude and Codex marketplace catalogs are published.
- Evaluate dotnet-releaser for this flow; keep it only if it fits.

Spec: catalog B26 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T183, T272, T634; open T261, T465, T470, I-S2 ([open work](../../docs/port/open-work.md)).

Plan:
1. `eng/affected-tests.sh`: changed files to the JIT test projects they touch (reverse project references); anything outside a project forces a full run.
2. `eng/publish.sh <rid>`: one AOT publish of `rtok`, `rtok-hook` (and the AOT lane) per RID; `linux-musl-x64` inside an Alpine SDK container.
3. `eng/package-release.sh`: the per-RID archive layout, checksums and the ketch manifest.
4. `.github/workflows/ci.yml` (four OS runners plus one lint job) and `release.yml` (tag `v*`), every action pinned to a commit SHA; pin `packages.yml` too.
5. Validate locally: actionlint and shellcheck, the scripts on osx-arm64, osx-x64, linux-arm64 and linux-musl-x64 (containers), an archive built end to end. Evaluate dotnet-releaser.

Check: A tag on a test branch produces release archives for every RID, and each archive's `rtok --version` and `rtok-hook` smoke test pass on its own OS runner.
