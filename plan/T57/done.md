# T57. Done

`eng/Rtok.Build` is a Cake Frosting console project with one task per former `eng/*.sh`
script: `publish`, `smoke`, `package-release`, `publish-package`, `affected-tests`,
`check-marketplace`, `new-package`, `fuzz`. Run it with
`dotnet run --project eng/Rtok.Build -- --target=<task>`; without `--target` it lists the
tasks and their arguments. The eight scripts are deleted; `ci.yml`, `release.yml`,
`packages.yml`, CONTRIBUTING.md, `packages/README.md`, `tests/README.md` and
`plan/README.md` call the tasks.

- The BCL replaces jq (System.Text.Json), 7z (ZipFile), tar (TarFile, Unix modes kept),
  `sha256sum`/`shasum` (SHA256, same file format) and curl (HttpClient). CliWrap runs
  dotnet, git, docker, clang and the binaries under test.
- `affected-tests` runs `git diff --name-only <base>...HEAD` itself (`--base=<ref>`), so no
  pipe is needed.
- `new-package` XML-escapes the description; the script wrote `&` raw and broke the csproj.
- `package-release` accepts `--rtok` without `.exe` on Windows, as Git Bash did.
- Failures exit non-zero; Cake does not keep the old 2/1 split between usage and failure.
- Cake.Frosting pulls System.Security.Cryptography.Pkcs 10.0.12 (GHSA-846f-f764-76mx);
  `Directory.Packages.props` pins 10.0.13.
- The lint job's `shellcheck eng/*.sh` step is gone with the scripts.
  `packages/Pyrlyn.TreeSitter/native/build-native.sh` stays, as the creator decided: MSBuild
  runs it inside the build.

## Checked on macOS arm64

- `check-marketplace` and `--strict`: same results as the script.
- `affected-tests`: the same `.slnf`, byte for byte, as the script for three history ranges,
  `--all`, single-file changes in a package, an app project and a test project (15, 9, 1
  projects), and a plan-only change (none).
- `new-package`: the same files as the script except the escaped description.
- `publish-package --tag=Pyrlyn.Durations/v0.1.0 --dry-run`: tests, pack, local push ok; a
  wrong version fails with the script's message.
- `publish --rid=osx-arm64 --aot-tests` (with T3's `TreatWarningsAsErrors` change from
  `main` carried over), then `smoke` on `artifacts/dist/osx-arm64` and on the unpacked
  archive, `package-release`, and `Rtok.Smoke` with `RTOK_SMOKE_BIN`. Against the scripts,
  `smoke` prints the same lines and the `.tar.gz` holds the same files with the same modes
  and contents; the `.sha256` passes `shasum -c`. The zip path ran with stand-in Windows
  binaries.
- Not run here: Linux (musl through docker), Windows, osx-x64 under Rosetta; CI covers them.
- `fuzz` stops where `eng/fuzz.sh` did on `main`: the `dotnet publish` of tests/Rtok.Fuzz
  fails with NETSDK1191 (no RID for PublishAot). Up to there it downloads and pins the
  harness source; the steps after it are not run.
- Gates: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet csharpier check .`,
  markdownlint and actionlint are green. `dotnet test`: everything passes but
  `FuzzTargetTests.MutatedSeedsRun(agents-install)` (seed `2lNIJ27z8H8h`, also fails on
  `main`) and Rtok.Smoke `HookTests` that fail on and off (address in use, temp dir not
  empty) while other worktrees keep `rtok hook --serve` residents running; they pass on a
  rerun.
