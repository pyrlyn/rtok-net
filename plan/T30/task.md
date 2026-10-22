---
status: in progress
priority: P1
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
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

Tests:
- `Rtok.Measure.Tests` Verify: the Claude reader on a fixture home walks `projects/**/*.jsonl` and `<session>/subagents/agent-*.jsonl` with `meta.json`, and attributes sub-agent calls to their parent session.
- `Rtok.Measure.Tests` Verify: the Codex, OpenCode and Cursor readers each turn their fixture session logs into call records.
- `Rtok.Measure.Tests` integration: the transcript walker counts a symlinked alias once, walks real entries before links and skips dangling links.
- `Rtok.Measure.Tests` CsCheck: a transcript with any line truncated or replaced by garbage still yields every other line's records and never throws.
- `Rtok.Fuzz` fuzz: the `transcript-jsonl` target feeds arbitrary lines to every reader without an exception.
- `Rtok.Cli.Tests` Verify: `rtok stats` golden on the shared fixture, with every row (per tool, Bash families, MCP groups, cache, context-token-turns, repeat reads, images, dictionary) and with `--plugin`, `--cache` and `--price`.
- `Rtok.Measure.Tests` unit: p95 is the nearest rank over a 2048-sample deterministic reservoir, so the same input gives the same p95 on every run.
- `Rtok.Measure.Tests` unit: the Bash family strips `cd` and env assignments, handles quoted paths, and an unterminated quote falls back to the raw command instead of failing.
- `Rtok.Measure.Tests` unit: image tokens come from PNG IHDR and JPEG SOF sizes as ceil(w/28)·ceil(h/28), and the 2576 px high-res tier costs 4784 tokens.
- `Rtok.Measure.Tests` unit: repeat reads split into fired, changed, ranged, hook_absent, declined and subagent, and the dictionary row lists repeated paths of 16+ bytes and identifiers of 12+ bytes and is absent when nothing repeats.
- `Rtok.Cli.Tests` integration: `rtok stats --save-baseline a` then `--compare a` on the same fixture shows no change, and an unknown baseline name is refused.
- `Rtok.Measure.Tests` WireMock: `--calibrate` sends fixture samples to a fake `count_tokens` endpoint and fits chars/token per class, and an unreachable API leaves the stored ratios unchanged.
- `Rtok.Measure.Tests` unit: o200k counts from Microsoft.ML.Tokenizers match the checked-in counts for the fixture strings.
- `Rtok.Measure.Tests` integration: `rtok bench` runs a fake agent script for settings A and B the default 3 times each with stdin null and an 8 MiB output cap, and prints mean tokens and cost per config.
- `Rtok.Measure.Tests` integration: the replay bench on the shared fixture saves at least 76 %, and every saving it counts has a Measurement row whose bytes equal the returned bytes.

Spec: catalog A16, catalog B22 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T1.1, T1.2, T1.3, T1.5, T9.1, T49.1, T49.2, T53.1, T55.7, T58.3, T59.8, T61.1, T65.1, T71.4, T105, T125, T128, T134, T137, T165, T167, T179, T180, T219, T239, T241, T302, T305, T355, T397, T402, T403, T579, T580, T581, T587, T632, T633, T638.

Fuzz: `transcript-jsonl` in `tests/Rtok.Fuzz`: each transcript reader over any JSONL never throws and gives non-negative token sums.

Check: The replay bench reaches rtok's floor (76 %) on the shared fixture, and stats goldens match.

Plan:
1. New `src/Rtok.Measure` (Measure layer) and `tests/Rtok.Measure.Tests`. Reuse T38's walker (`LogFiles`), JSON helpers, Codex and OpenCode readers and `UsageCost` from `Rtok.Agents` (made public), not copies.
2. Port rtok's measure modules: the transcript parser (records, counters, compactions), sub-agents, image tokens, the dictionary row, the stats report (folds, table, JSON), baselines, cache health, the Bash family, the p95 reservoir.
3. A Cursor reader over `state.vscdb` (`cursorDiskKV` bubbles with `tokenCount`); rtok had none.
4. `--calibrate`: `count_tokens` over sampled text per class, fit chars/token, write `[estimator]`; any failure leaves it unchanged.
5. o200k (Microsoft.ML.Tokenizers) in the tests only, for fixture counts; the Release binary stays without the vocabulary.
6. `rtok bench`: tasks TOML, configs A/B, runs through `ProgressRunner` with null stdin, a timeout and an 8 MiB cap; mean tokens and cost per config.
7. CLI `rtok stats` and `rtok bench` with docs and goldens; fuzz target `transcript-jsonl` with a corpus.
8. Rows that need other lanes' plugins (cmd filter kinds, read deny reasons and outline, archive clip, proxy lanes, checkpoint notes) and the replay bench (hook, MCP, cmd and read surfaces) get a seam now and are wired when T17, T19, T9, T29, T22 and T13/T15 land.

Split: step 8, the replay-bench test and the `--suite graph` bench moved to [T30.1](../T30.1/task.md) with the creator's OK.
