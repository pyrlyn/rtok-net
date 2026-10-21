---
status: in progress
priority: P1
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T53. Fuzz targets for landed code and a fuzz step in every task

T2 left `tests/Rtok.Fuzz` with nine SharpFuzz stubs. Nothing feeds them, so parsers of
untrusted input (host configs, TOML, JSONC, shell words, ignore files, `--help` text) are
never fuzzed, and later tasks have no rule to add their own targets.

Done when:
- Every parser of outside input that is already on `main` has a wired target in
  `tests/Rtok.Fuzz`: a body that catches only the documented failure type and checks the
  code's own invariants (round trips, unchanged bytes, re-parse).
- Each target has a seed corpus in `tests/Rtok.Fuzz/corpus/<target>/`.
- `tests/Rtok.Fuzz.Tests` runs every wired body over its corpus and over CsCheck-generated
  and mutated inputs on each `dotnet test`, and fails on a target without a body or corpus.
- `eng/fuzz.sh <target>` instruments the assemblies with `sharpfuzz` and runs
  libfuzzer-dotnet; `tests/README.md` says how.
- Every open task in `plan/` whose code reads outside input names its target in a `Fuzz:`
  line, and `plan/README.md` records the rule.

Check: `dotnet test` runs `Rtok.Fuzz.Tests` green, and `rtok-fuzz --list` shows no landed
target as unwired.

Plan:
1. Targets on `main`: `jsonc-edit` (T12.3), `config-toml` (T7), `toml-edit` (T12.9),
   `shell-words` (T12.6), `ignore` (T12.4), `durations` (T12.8), `help-page` (T12.11),
   `mcp-config` (T34), `host-json` (T33).
2. Seed corpora from the packages' test cases.
3. `tests/Rtok.Fuzz.Tests`: corpus replay plus CsCheck byte and mutation runs per target.
4. `eng/fuzz.sh` and the `tests/README.md` section.
5. A `Fuzz:` line in each open task that parses outside input, and the rule in `plan/README.md`.
