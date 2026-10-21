---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
---

# T17. cmd plugin: `rtok run`, rule engine, Bash rewrite, `filter`

Shell output is the largest token sink; `rtok run` shortens it and archives the original.

Done when:
- `rtok run [--agent <id>] -- <cmd>`:
  - runs `sh -c`, merges the streams and keeps the exit code (128+signal on a signal death);
  - caps capture at 32 MiB and still drains the pipes;
  - archives the raw output and prints the trailer and marker under rtok's rules.
- The TOML rule engine is ported, reading `rules/default.toml` copied from rtok. Its passes are dedupe, collapse_columns, dir and diag grouping, keep patterns, and the fail tail. Rule sources merge in rtok's order.
- Bounded commands pass through unchanged.
- The PreToolUse(Bash) rewrite follows every never-wrap rule: sudo, heredocs, `&`, interactive stems, sub-agent git, cd hops.
- `rtok filter --stdin [--cmd]` and `rtok discover` behave as in the catalog.

Spec: catalog A4, catalog B12 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T3.1, T3.2, T3.4, T3.6, T34.8, T36.3, T36.4, T50.1, T50.2, T58.5, T59.1, T64.1, T64.2, T65.2, T65.3, T65.4, T160, T175, T176, T177, T233, T235.1, T235.2, T235.3, T238, T240, T247, T267, T268, T296, T297, T321, T325, T360, T366, T386, T437, T444, T447, T448, T457, T536, T662, T688.

Check: Golden tests from rtok's cmd fixtures pass with their `min_saving` floors, and the rewrite table test covers every never-wrap case.
