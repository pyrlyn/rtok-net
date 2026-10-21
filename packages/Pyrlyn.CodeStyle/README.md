# Pyrlyn.CodeStyle

Shared analyzers, analyzer severities and build settings for pyrlyn .NET repositories, with
editorconfig, CSharpier and markdownlint templates.

## Install

Reference it once for every project, in `Directory.Packages.props` (central package management):

```xml
<ItemGroup>
  <GlobalPackageReference Include="Pyrlyn.CodeStyle" Version="0.1.0" />
</ItemGroup>
```

Without central package management, add it to each project (or `Directory.Build.props`) as
`<PackageReference Include="Pyrlyn.CodeStyle" Version="0.1.0" PrivateAssets="all" />`.

## What it brings

- Analyzers: Roslynator, Meziantou and VS Threading, as dependencies of this development
  dependency; they never flow to your own packages' consumers.
- `build/Pyrlyn.CodeStyle.props`, as defaults a value of your own overrides: `Nullable` enable,
  `AnalysisLevel` latest-recommended, `EnforceCodeStyleInBuild`, warnings as errors when `CI` is
  `true`, and nullable plus the trim and AOT warnings (IL2026, IL2104, IL3050, IL3053, IL3058)
  as errors always.
- `build/Pyrlyn.CodeStyle.globalconfig`: file-scoped namespaces (IDE0161), the Performance
  category as warnings, VSTHRD003 off (it needs a JoinableTaskFactory). A line in your
  `.editorconfig` overrides any of them.

## Templates

NuGet cannot place files in your repository, so the package carries them under `templates/`.
Copy them to the repository root once and again after an update:

```bash
v=0.1.0; t=~/.nuget/packages/pyrlyn.codestyle/$v/templates
cp $t/.editorconfig $t/.csharpierrc.json .
cp $t/.markdownlint.jsonc eng/
```

- `.editorconfig`: UTF-8, LF, final newline, 4 spaces (2 for JSON, XML, MSBuild and YAML).
  Append your own sections below it.
- `.csharpierrc.json`: CSharpier at 100 columns, LF, XML files at 2 spaces. Run CSharpier as a
  local tool (`dotnet tool install csharpier`, then `dotnet csharpier check .`).
- `.markdownlint.jsonc`: markdownlint's defaults without line length (MD013) and table style
  (MD060), with repeated headings allowed under different parents (MD024). Keep it as copied and
  put your own exceptions in the config that extends it:

```jsonc
// .markdownlint-cli2.jsonc
{
  "config": { "extends": "eng/.markdownlint.jsonc", "MD041": false },
  "globs": ["**/*.md"]
}
```
