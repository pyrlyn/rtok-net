---
status: in progress
priority: P2
complexity: 2
readiness: 10%
agent: Claude Code / claude-opus-5-5
---

# T59. Bogus for test data whose values do not matter

The creator wants test data generated with Bogus. Tests now spell out made-up values (byte
and token counts, project names, paths, log text) even where the test checks only that a
row lands, counts once or goes with retention. Those literals hide which values the test is
about. Bogus fills the rest, seeded so every run sees the same data.

Done when:
- `Bogus` is a central package version; the commit gives the one-line reason. Only JIT test
  projects reference it: never a library, `Rtok.AotTests` or the fuzz targets.
- `Rtok.Store.Tests` has one seeded `Fakes` class with generators for the store inputs whose
  values tests do not care about (`MeasurementInput`, `UsageInput`, `Symbol`, session,
  usage, token and log fields), and its tests use them in place of those literals.
- Values a test is about stay literal: expected parser outputs, priced models and token sums
  (`Rtok.Agents.Tests`), snapshot inputs (Verify), byte bodies, session and plugin keys the
  assertions name, and parsers already covered by CsCheck.
- N11 and `tests/README.md` say when a test takes Bogus data, and the package map lists it.

Check: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`,
`dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3` are green, and
`git grep -l Bogus -- src packages/*/src tests/Rtok.AotTests tests/Rtok.Fuzz` prints nothing.

Tests: `Rtok.Store.Tests` unit (WriteTests, RetentionTests, BatchTests, LedgerTests) on
Bogus data.

## Plan

1. `Directory.Packages.props`: `Bogus` 35.6.5; `Rtok.Store.Tests` references it.
2. `tests/Rtok.Store.Tests/Fakes.cs`: a fixed seed per generator, so a test's data repeats.
3. Rewrite the don't-care literals in WriteTests, RetentionTests, BatchTests, LedgerTests.
4. N11, package map and `tests/README.md`; build, test, format, lint.
