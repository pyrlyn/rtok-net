# Own packages

Code with no NuGet answer becomes its own NuGet package here (plan/README.md N5, N17):
`Pyrlyn.*` for generic code, `Rtok.*` for rtok's public contracts.

## Layout

```text
packages/<Name>/
  <Name>.slnx                 the package and its tests; dotnet pack|test packages/<Name>
  README.md                   packed as the NuGet readme
  CHANGELOG.md                one "## <version>" entry per release
  LICENSE                     GPL-3.0-or-later, the repo's licence
  src/<Name>.csproj           the library: Version, Description, PackageTags
  tests/<Name>.Tests.csproj   xUnit v3 on MTP; Xunit is a global using
```

Create one with
`dotnet run --project eng/Rtok.Build -- --target=new-package --name=<Name> --summary="<description>"`.
It writes this layout and adds
both projects to `rtok.slnx`, so `dotnet build rtok.slnx` and `dotnet test` cover them.

`packages/Directory.Build.props` and `.targets` give every package its NuGet metadata,
SourceLink, a `.snupkg` and an XML doc file (outside Debug), plus `IsAotCompatible` from the
root files. The build fails when:

- README.md, CHANGELOG.md or LICENSE is missing, or Description is unset;
- a package or its tests reference a project outside `packages/` (never `src/`).

A package may reference another package's project; the reference becomes a NuGet dependency.

## Versioning

Each package has its own SemVer `Version` in `src/<Name>.csproj`, independent of rtok's
version. A release bumps it, adds the `## <version>` entry to CHANGELOG.md and is tagged
`<Name>/v<version>`.

## Publishing

```bash
dotnet run --project eng/Rtok.Build -- --target=publish-package --tag=Pyrlyn.FsKit/v0.1.0 --dry-run
```

The task checks that the tag matches the project version and CHANGELOG.md, that the README
has no TODO left, runs the package's tests, packs with warnings as errors and pushes. With
`--dry-run` it pushes to a temporary local feed. On GitHub, pushing the tag runs
`.github/workflows/packages.yml`: a dry run, then a push to nuget.org through NuGet trusted
publishing (`nuget` environment, `NUGET_USER` secret, a trusted-publishing policy for this
repo and workflow on nuget.org).
