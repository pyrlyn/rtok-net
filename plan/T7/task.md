---
status: todo
priority: P0
complexity: 4
readiness: 0%
agent:
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
