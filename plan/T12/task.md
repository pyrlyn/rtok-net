---
status: in progress
priority: P0
complexity: 2
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T12. Own NuGet packages: layout, versioning and publishing

Functionality with no NuGet answer becomes its own package (N5). They all need one shape.

Done when:
- `packages/<Name>/` holds the project, its tests, README, CHANGELOG and LICENSE.
- Each package has its own version, SourceLink, and `IsAotCompatible`.
- A pack-and-publish workflow pushes a package on a `<Name>/v<version>` tag.
- A package depends on no `src/` project.
- The subtasks T12.1-T12.11 each add one package to this layout.

Ports: no rtok task; new for .NET.

Check: `dotnet pack packages/*` produces one package per folder with zero warnings, and a dry-run publish of one package succeeds.

Plan:
1. `packages/Directory.Build.props` and `.targets`: import the root files, package metadata, SourceLink, snupkg, docs; guards for README/CHANGELOG/LICENSE, Description and `src/` references.
2. Layout `packages/<Name>/{<Name>.slnx, README.md, CHANGELOG.md, LICENSE, src/<Name>.csproj, tests/<Name>.Tests.csproj}`; `eng/new-package.sh` scaffolds it and adds both projects to `rtok.slnx`.
3. `eng/publish-package.sh <Name>/v<version> [--dry-run]`: checks tag, csproj version and CHANGELOG, tests, packs, pushes (to a local feed on `--dry-run`); `.github/workflows/packages.yml` runs it on tags with NuGet trusted publishing.
4. Verify with a scratch package that is not committed: pack with zero warnings, dry-run publish, guard failures.
