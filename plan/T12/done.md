# T12. Done

Own NuGet packages have one layout, their own versions and a tag-driven publish.

What was done:
- `packages/Directory.Build.props` and `.targets` import the root files and give every `packages/<Name>/src/<Name>.csproj` its NuGet metadata (readme, project and release-notes URLs, repository), SourceLink, a `.snupkg`, an XML doc file outside Debug; `IsAotCompatible` comes from the root. `<Name>.Tests` projects become xUnit v3 (MTP v2) test executables with `Xunit` as a global using.
- Build guards: README.md, CHANGELOG.md or LICENSE missing, or no Description, fails the build; a `ProjectReference` outside `packages/` (so any `src/` project) fails it.
- Root: `IsPackable=false` (only packages pack); the short-sha `SourceRevisionId` target skips packable projects, so a nuspec carries the full commit SourceLink sets. `LICENSE` (GPL-3.0 text from gnu.org) at the root, copied into each package. `xunit.v3.mtp-v2` 4.0.2 pinned: the package tests' framework (N11).
- `eng/new-package.sh <Name> "<description>"` scaffolds `<Name>.slnx`, README, CHANGELOG, LICENSE, `src/` and `tests/` and adds both projects to `rtok.slnx`.
- `eng/publish-package.sh <Name>/v<version> [--dry-run]` checks tag against project version and CHANGELOG, refuses a README with TODO, runs the package tests, packs with `-warnaserror`, pushes (to a temporary local feed on `--dry-run`). `.github/workflows/packages.yml` runs it on `Pyrlyn.*/v*` and `Rtok.*/v*` tags: dry run, then NuGet trusted publishing (`nuget` environment, `NUGET_USER` secret).
- Docs: `packages/README.md` (layout, versioning, publishing), a pointer in `CONTRIBUTING.md`.

How it was checked (osx-arm64), with a scratch `Pyrlyn.Sample` made by the scaffold script and removed before the commit (T12.1-T12.11 add the real packages):
- `dotnet pack packages/Pyrlyn.Sample -c Release -warnaserror`: 0 warnings, `.nupkg` with README, dll and XML docs, plus `.snupkg`; nuspec has licence, readme, URLs and the full commit.
- In a clone with a GitHub `origin`, the PDB's SourceLink maps to `raw.githubusercontent.com/pyrlyn/rtok-net/<commit>/*`. Without a remote (this repo) Source Link is empty and pack stays warning-free.
- `eng/publish-package.sh Pyrlyn.Sample/v0.1.0 --dry-run`: tests pass, pack, push to the local feed, exit 0. Wrong version, unknown package, bad tag (exit 2), missing CHANGELOG entry and missing `NUGET_API_KEY` each fail with a message.
- Removing CHANGELOG.md and adding a reference to `src/Rtok.Hook` each fail the build with the guard's error.
- `rtok --version` still prints the short sha. `dotnet build rtok.slnx -c Release -warnaserror` 0 warnings, `dotnet csharpier check .` clean; `dotnet test` finds no test projects yet (T2).
- Not checked: the GitHub workflow itself (no remote here).

Commit: `T12: Own NuGet packages: layout, versioning and publishing` on branch `T12-package-layout`, fast-forwarded into `main`.
