---
status: in progress
priority: P0
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T7. Config: layered TOML with provenance, show/get/set/validate/init

One config file holds every setting, and every CLI flag is a config key (rtok D12, D14).

Done when:
- Layers resolve in this order: defaults < user file < `<git root>/.rtok.toml` < `.env` (RTOK_* lines only) < `RTOK_<SECTION>_<KEY>` env < flags. The source of each key is reported. The layering and provenance come from Pyrlyn.Config.Layered (T12.9).
- The typed schema denies unknown keys and range-checks values, with file:line errors. It uses the Tomlyn source-generated serializer.
- `config show [--sources]`, `get`, `set` (comment-preserving), `validate [path]` and `init` behave as in the catalog.
- Home resolution rules hold.
- Secrets are redacted in show, get, set and report.
- A reference `default.toml` is generated from the types.
- A test reads the command model (T12.11) and fails on any flag without a config key.

Tests:
- `Rtok.Config.Tests` unit: each layer (defaults, user file, `<git root>/.rtok.toml`, `.env`, `RTOK_<SECTION>_<KEY>`, flags) overrides the one before, and the key's source names that layer.
- `Rtok.Config.Tests` unit: only `RTOK_*` lines of the nearest `.env` are read, a directory named `.env` does not stop the walk, and legacy keys are folded before env and flags.
- `Pyrlyn.Config.Layered.Tests` CsCheck: for any generated stack of layers, every key resolves to the value and source of the last layer that sets it.
- `Rtok.Config.Tests` unit: an unknown key, a wrong type and an out-of-range value (`stats.since`, `report.since`, an enum) each report file:line and make `config validate` exit 1, a bad env override is reported the same way, and outside `validate` the key keeps its default with a warning.
- `Rtok.Config.Tests` unit: a relative `RTOK_HOME` resolves against the cwd, an empty one falls back to `<user home>/.rtok`, and with no home at all the temp dir is used, never the cwd.
- `Rtok.Config.Tests` Verify: `config show --sources` and `config show --json` on a layered fixture.
- `Rtok.Config.Tests` Verify: `config set` on a commented file changes only the edited key's line, and every other byte stays identical.
- `Rtok.Config.Tests` integration: a rejected `config set` exits 1 and leaves the file byte-identical.
- `Rtok.Config.Tests` integration: `config validate` with no path creates `<home>/config.toml` and prints `ok <home>/config.toml`, and an explicit missing path is an error.
- `Rtok.Config.Tests` unit: `config init` writes every default commented out, map headers included, and refuses to overwrite an existing file without `--force`.
- `Rtok.Config.Tests` unit: `otel.headers` is redacted in `config show`, `get` and `set` output and in the config rows handed to the report.
- `Rtok.Config.Tests` unit: `config/default.toml` regenerated from the types equals the checked-in and embedded file and loads as the defaults.
- `Rtok.Cli.Tests` unit: every flag in the command model maps to a config key by the naming rules, and every listed exception is still used.
- `Rtok.Config.Tests` Verify: `config set --dry-run` and `config init --dry-run` print a unified diff and leave the disk unchanged.
- `Rtok.AotTests` AOT: the Tomlyn source-generated context loads and validates `default.toml` without reflection.

Spec: catalog A30, catalog B19 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T0.2, T12.1, T12.2, T12.3, T12.4, T12.5, T12.6, T34.7, T36.7, T36.8, T36.9, T45.4, T184, T328, T362, T363, T364, T365, T379, T381, T594, T595, T630.

Check: Golden tests pass for `config show --sources` and `config set` on a commented file. The file stays byte-identical outside the edited key, and the flag-to-key coverage test passes.

Plan:
1. `src/Rtok.Config`: the typed schema (one class per section, snake_case, Tomlyn source-generated context), home and git-root resolution, `~` expansion, legacy keys.
2. Loader on Pyrlyn.Config.Layered: defaults, user file, `.rtok.toml`, `.env` (RTOK_* only), env, flags; then the validator (unknown keys, types, ranges, choices) with file:line.
3. `config/default.toml` generated from the types and their doc comments, embedded; a drift test regenerates it.
4. `rtok config show [--sources] | get | set | validate [path] | init` in `Rtok.Cli`, with secrets redacted; `set` through `TomlEditor`.
5. `tests/Rtok.Config.Tests`: goldens for `show --sources` and `set` on a commented file, validator and layering cases.
6. The flag-to-key coverage test (`tests/Rtok.Cli.Tests`), reading the command model from the built binary's `--help`.
7. `--dry-run` with a unified diff on `init` and `set` once T12.2 (L7) has landed the diff package.

Status: all seven steps are merged.
