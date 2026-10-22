---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T56. StyleCop.Analyzers and SonarAnalyzer.CSharp

Roslynator, Meziantou and BannedApiAnalyzers run on every project (T1). The creator wants
StyleCop.Analyzers and the Sonar rules (SonarLint's rule set, shipped for builds as the
SonarAnalyzer.CSharp package) on top. Starts after T55 lands: it builds on T55's shared
linter and formatter settings instead of editing the same files in parallel.

Done when:
- `StyleCop.Analyzers` (1.3.x beta; the 1.1.118 stable predates current C#) and
  `SonarAnalyzer.CSharp` are global analyzer references next to the others, with
  `PrivateAssets` so no package depends on them; the commit gives the one-line reason.
- Their rules are configured where T55 keeps the shared settings: StyleCop's layout and
  spacing rules that CSharpier owns are off, so `dotnet csharpier check .` and the analyzers
  never disagree; rules that duplicate a CA, IDE, Roslynator or Meziantou rule keep one owner;
  documentation and file-header rules match what the repository already does.
- Every remaining finding on `main` is fixed, or turned off with a one-line reason; no
  blanket `NoWarn`.
- Debug builds still skip analyzers (N3), and `plan/README.md` lists both packages in the
  package map.

Check: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and
`dotnet csharpier check .` are green with both analyzers loaded (an SA and an S rule
seeded into a scratch file fails the build).
