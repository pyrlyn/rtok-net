---
status: todo
priority: P0
complexity: 2
readiness: 0%
agent:
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
