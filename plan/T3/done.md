# T3 done

CI and the release pipeline run per OS. The work is in `.github/workflows/` and five scripts in `eng/` that the workflows call. The scripts run locally the same way, as documented in `CONTRIBUTING.md`, "CI and releases".

## What was done

### `ci.yml`
Triggers: push to `main`, pull requests, and dispatch. It has five jobs.

- **One build job per OS:**

  | Job | Runner | RIDs |
  | --- | --- | --- |
  | macOS arm64 | `macos-15` | `osx-arm64`, `osx-x64` |
  | Ubuntu x64 | `ubuntu-24.04` | `linux-x64`, `linux-musl-x64` |
  | Ubuntu arm64 | `ubuntu-24.04-arm` | `linux-arm64` |
  | Windows x64 | `windows-2025` | `win-x64` |

- **Each build job runs these steps:**
  1. `dotnet build rtok.slnx -c Release -warnaserror`.
  2. The JIT tests the change touches.
  3. An AOT publish with `-warnaserror` of `rtok` and `rtok-hook` for each of its RIDs. The native RID also gets the AOT lane.
  4. `Rtok.Smoke` against the published binaries (`RTOK_SMOKE_BIN`). This includes the AOT lane.
  5. `eng/smoke.sh` for every RID.
  6. The hook latency gate.
  7. A dry-run release archive per RID.
- **One lint job, on Ubuntu only:**
  - CSharpier;
  - the NuGet vulnerability check (`dotnet package list --vulnerable --include-transitive`, which fails on any hit or restore problem);
  - jscpd 5.4.2 with a 2 % threshold over `src`, `packages` and `tests` (`.jscpd.json`; 0.20 % today);
  - shellcheck on `eng/*.sh`;
  - actionlint;
  - the marketplace catalog check.
- **NuGet cache:** one per runner, keyed on OS, arch and the hash of `global.json`, `Directory.Packages.props` and the `*.csproj` files.

### Selective tests (`eng/affected-tests.sh`)
- A changed file belongs to the project whose directory holds it.
- Every project that references that project, directly or not, is affected too.
- The affected `*.Tests` projects go into a solution filter that `dotnet test --solution` runs.
- A change outside every project runs everything: build props, `global.json`, `eng/`, `.github/`, fixtures.
- Docs, `plan/` and `spikes/` run nothing.
- Pushes to `main` and dispatches run everything.
- It runs on bash 3.2, the macOS default.

### `release.yml`
Triggers: a `v*` tag, or dispatch (which builds without releasing). Each OS runner does this:

1. Checks that the tag matches `Version`.
2. Publishes its RIDs.
3. Packs them with `eng/package-release.sh`.
4. Unpacks every archive and smoke-tests it with `eng/smoke.sh`.

A last job then checks that every RID is there, checks `SHA256SUMS`, runs the strict marketplace check, and creates a **draft** GitHub release. Publishing the draft and `ketch push` stay manual, as they were in rtok.

### Archive layout (`eng/package-release.sh`)
- Name: `rtok-<rid>.tar.gz`, or `rtok-win-x64.zip`, plus a `.sha256` per archive.
- Each archive holds one `rtok-<rid>/` directory with:
  - both binaries and `LICENSE`;
  - `share/man/man1/*.1` from `rtok man --dir`;
  - `share/completions/{rtok.bash,_rtok,rtok.fish,rtok.ps1,rtok.lua}` from `rtok completions <shell>`;
  - `plugins/`, `skills/`, `rules/` and `modes/`, copied from the repo.
- The man pages and completions are written once by the runner's native `rtok`; they are the same for every RID.
- Strict by default: a missing piece fails the release. CI packs with `--allow-missing` and warns.

### Publish and smoke scripts
- `eng/publish.sh <rid>`:
  - writes `artifacts/dist/<rid>/`;
  - passes the host's commit as `SourceRevisionId`, so `--version` is right inside containers;
  - runs `linux-musl-*` inside `mcr.microsoft.com/dotnet/sdk:10.0.402-alpine3.24-aot`, pinned by digest.
- `eng/smoke.sh <rid> <dir>` checks two things:
  - `rtok --version` matches `rtok X.Y.Z (<sha9>)`;
  - `rtok-hook` answers every hook fixture with exit 0 and `{}`.

  It runs `osx-x64` under Rosetta and `linux-musl-*` in Alpine (pinned by digest).

### ketch and marketplace
- `ketch.toml` is carried over from rtok for `github:pyrlyn/rtok-net`:
  - `bin` pins both `rtok*` and `rtok-hook*`;
  - a comment explains the NTFS order (`rtok-hook.exe` sorts first) and ketch's stem-equals-name rule;
  - assets are matched by RID name. ketch's platform tokens include `osx`, `linux`, `win`, `x64` and `arm64` (`ketch-core/src/model.rs`), and musl is excluded.
- The Claude (`.claude-plugin/marketplace.json`) and Codex (`.agents/plugins/marketplace.json`) catalogs are carried over from rtok.
  - As in rtok (T183), they are published by merging to `main`.
  - `eng/check-marketplace.sh` checks them in lint. With `--strict` (release), it also requires `plugins/<host>` to exist.

### Actions pinning
Every action is pinned to a commit SHA with a version comment (rtok I-S2):

| Action | Version |
| --- | --- |
| checkout | v7.0.1 |
| setup-dotnet | v6.0.0 |
| cache | v6.1.0 |
| upload-artifact | v7.0.2 |
| download-artifact | v8.0.2 |
| NuGet/login | v1.2.0 |

`packages.yml` is pinned too. The actionlint image is pinned by digest.

### Fixes needed for CI
- `Rtok.AotTests`: a `#pragma` hid IL3000 only from Roslyn, and ILC repeated it at publish. Under `-warnaserror`, or `CI=true` (TreatWarningsAsErrors), that failed the publish. It is now `[UnconditionalSuppressMessage]`.
- `Rtok.Latency --budget-only`: the baseline is per machine, measured on an M3 Max, and GitHub runners are other machines. CI therefore keeps the 10 ms median rule and skips the "120 % of baseline" rule.

### dotnet-releaser: evaluated, not used
It publishes every RID from one host and owns the GitHub release, the changelog and the NuGet push. Its docs do not cover NativeAOT or merging per-OS builds. NativeAOT needs one runner per OS, and the own packages already publish through `packages.yml`. Four short scripts do the same job with less machinery.

## Checked locally

Gates, on macOS arm64, after rebasing on `main`:
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .` are green.
- actionlint 1.7.12 with shellcheck 0.11.0 is clean on all three workflows.
- shellcheck is clean on `eng/*.sh`.
- jscpd: 0.20 % (4 clones).

`osx-arm64` and `osx-x64`:
- `eng/publish.sh` passes for both.
- `eng/smoke.sh` passes for both; `osx-x64` runs under Rosetta.
- `Rtok.Smoke` passes with `RTOK_SMOKE_BIN` (8 tests).
- Latency gate with `--budget-only`: p50 5 to 7 ms on a loaded machine.
- A `--allow-missing` archive was packed, unpacked and smoke-tested. The strict pack fails as intended.

`linux-arm64`: the whole build job ran in `sdk:10.0.402-noble-aot` (pinned by digest, without `.git`):
- the Release build;
- `--all` affected tests: 319 passed;
- the AOT publish;
- `Rtok.Smoke`: 8 passed;
- latency: p50 about 1 ms;
- the archive.

`linux-musl-arm64`:
- `eng/publish.sh` ran its container path: it produced a musl ELF with the right commit.
- `eng/smoke.sh` passed inside Alpine.

The affected-tests script was checked by hand on these inputs:

| Changed file | Result |
| --- | --- |
| a package source file | its tests |
| `plan/` or `*.md` | nothing |
| `Directory.Build.props` | full run |
| `src/Rtok.Hook` | no JIT tests (smoke covers it) |
| `tests/Rtok.Testing` | its tests |

The vulnerability query found a seeded vulnerability and passes on the real tree.

## Not run, and why
- **There is no git remote**, so neither workflow has run on GitHub. The task's Check (a tag on a test branch gives archives for every RID, smoke-tested on their own runners) waits for a remote.
- **Never run here:** `linux-x64`, `linux-musl-x64` (musl was run on arm64 only) and `win-x64`.
  - Windows-only steps are written but unverified: `cygpath`, `7z` and `choco install hyperfine`.
- **The Windows latency gate reports but does not block** (`continue-on-error`) until T52 measures Windows.
- **A real release will fail in the strict pack until these exist:**
  - `rtok man` and `rtok completions` (T12.11 with the CLI shell T11);
  - `plugins/` and `skills/` (T36);
  - `rules/` and `modes/`, the data files that shipped compiled into rtok.

  CI packs the same layout with `--allow-missing` until then.

Commit: `T3: CI and release pipeline per OS`.
