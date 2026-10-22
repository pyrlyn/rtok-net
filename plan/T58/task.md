---
status: in progress
priority: P1
complexity: 1
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T58. jscpd ignores generated EF migrations

The CI lint job's duplication step fails on `main`: 6.0 % against the 2 % threshold, almost all
of it in the EF Core migrations under `src/Rtok.Store.Model/Migrations/`, which `dotnet ef`
generates and nobody edits (found in T55).

Done when `.jscpd.json` leaves `**/Migrations/**` out and `npx --yes jscpd@5.4.2 --config
.jscpd.json .` passes; hand-written code keeps the same threshold.

Check: the jscpd command exits 0 on `main`.

Plan:
1. Add `**/Migrations/**` to the `ignore` list in `.jscpd.json`.
2. Run jscpd, markdownlint and CSharpier; no C# changes, so no build or test run is needed.
