---
status: in progress
priority: P2
complexity: 2
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T55. Package Pyrlyn.CodeStyle: shared analyzers and format settings

rtok-net's lint and format setup (Roslynator, Meziantou, the .NET analyzers, CSharpier) lives in
root files that another repository cannot reuse. The creator wants the generic part in one
package other pyrlyn repositories reference with a single line, plus three additions: VS Threading
analyzers (in the README package map, never installed), an explicit CSharpier config and markdownlint.

Done when:
- `packages/Pyrlyn.CodeStyle` packs a development dependency that brings Roslynator, Meziantou
  and VS Threading analyzers, MSBuild props (AnalysisLevel, EnforceCodeStyleInBuild, Nullable,
  warnings as errors in CI) and a `.globalconfig` with the shared severities.
- It packs templates for what NuGet cannot ship: `.editorconfig`, `.csharpierrc.json`,
  `.markdownlint-cli2.jsonc`.
- rtok-net consumes the same props, globalconfig and templates from source; analyzer versions
  stay only in `Directory.Packages.props`; rtok-only rules (banned APIs, CA2007, EF interceptors,
  the Debug loop) stay in the root files.
- CI lint runs markdownlint.

Check: `Pyrlyn.CodeStyle.Tests` keeps the root config files equal to the templates, and a packed
`Pyrlyn.CodeStyle.nupkg` holds the props, globalconfig, templates and the three analyzer
dependencies without excluded analyzer assets.

Plan:
1. `eng/new-package.sh Pyrlyn.CodeStyle`; no build output, `DevelopmentDependency`, analyzer
   PackageReferences with `PrivateAssets=none`, versions from the GlobalPackageReference items.
2. `build/Pyrlyn.CodeStyle.props` and `.globalconfig`; the root `Directory.Build.props` imports
   them, generic lines leave the root `.editorconfig` and `Directory.Build.props`.
3. VS Threading in `Directory.Packages.props`; fix or configure what it reports.
4. Templates; the root `.csharpierrc.json`, `.markdownlint-cli2.jsonc` and the head of
   `.editorconfig` equal them (package test). markdownlint step in `ci.yml`.
5. Build Release with `-warnaserror`, test, CSharpier, markdownlint, pack and inspect the nupkg.
