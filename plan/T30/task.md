---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
---

# T30. Measure: transcripts, `rtok stats`, `rtok bench`, tokenizers

No saving claim without a measurement (rtok D3); `stats` and `bench` are how rtok proves its value.

Done when:
- Transcript readers exist for Claude (with sub-agents), Codex, OpenCode and Cursor.
- `rtok stats` prints every row and flag from the catalog: per tool, Bash families, cache, context-token-turns, repeat reads, images, dictionary, prices.
- Baselines (`--save-baseline`, `--compare`) work.
- `--calibrate` uses the Anthropic `count_tokens` API.
- `rtok bench` runs the A/B harness.
- Token counts use Microsoft.ML.Tokenizers (o200k) for fixtures.

Spec: catalog A16, catalog B22 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T1.1, T1.2, T1.3, T1.5, T9.1, T49.1, T49.2, T53.1, T55.7, T58.3, T59.8, T61.1, T65.1, T71.4, T105, T125, T128, T134, T137, T165, T167, T179, T180, T219, T239, T241, T302, T305, T355, T397, T402, T403, T579, T580, T581, T587, T632, T633, T638.

Check: The replay bench reaches rtok's floor (76 %) on the shared fixture, and stats goldens match.
