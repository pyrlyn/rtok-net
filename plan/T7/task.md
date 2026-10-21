---
status: in progress
priority: P0
complexity: 4
readiness: 92%
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

Status: steps 1–6 are merged. Step 7 waits for T12.2; the task closes with it.
