---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
---

# T17.1. cmd plugin on the store: archived `run`/`filter` and `rtok discover`

T17 landed the cmd plugin with a CLI that has no store yet: `rtok run` prints its output raw and `rtok filter` only compresses, since nothing could archive the original for `expand`. It depends on T8 (the store and its `hook_inputs_since` query) and T9 (archive and expand): once the CLI has that host, it hands it to the plugin and `rtok discover` can rank what is still unfiltered.

Done when:
- `rtok run` and `rtok filter --archive` pass the CLI's store host to `CmdCommands`, so shortened output carries the `[rtok <id> · N lines]` trailer and `… N lines omitted (expand <id>)` markers and records its Measurement row; a failing store still prints the raw bytes (fail open).
- `rtok filter --stdin` records its rule row and trailer through the same host.
- `rtok discover [--since] [--limit] [--json]` ranks unfiltered command families by estimated output tokens (catalog B12): a `hook_inputs_since` store query and a port of rtok's `measure::discover::rank`.

Tests:
- `Rtok.Cli.Tests` integration: shortened `rtok run` output prints the trailer and marker, and `rtok expand <id>` returns the raw output byte for byte.
- `Rtok.Cli.Tests` integration: `rtok discover --json` ranks unfiltered command families by estimated tokens.

Spec: catalog A4, catalog B12 in [docs/port/catalog.md](../../docs/port/catalog.md); `plan/T17/done.md` for what T17 left here.

Ports: rtok's `measure::discover` and the store side of `run` (T17's list).

Fuzz: none new; `cmd-filter` already drives the archive path through `MemoryHost`.

Check: `rtok expand <id>` returns the raw bytes of a shortened `rtok run`, and `rtok discover --json` ranks unfiltered families.
