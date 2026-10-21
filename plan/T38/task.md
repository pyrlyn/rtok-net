---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T38. Agent usage and cost: `rtok agents usage`

Users want token use and cost across all their agents, from the agents' own logs.

Done when:
- Log readers exist for Claude Code, Codex, OpenCode/Kilo (read-only SQLite), Copilot, Gemini, pi and Kimi, each with a fixture per shape (rtok T396).
- The flags, the units, the `--tz` buckets with DST and the JSON shape follow the catalog.
- Prices come only from `[stats.prices]`.

Tests:
- `Rtok.Agents.Tests` unit: the Claude Code reader totals a jsonl fixture and counts a repeated `message.id` once.
- `Rtok.Agents.Tests` unit: the Codex `token_count`, Copilot `session.shutdown` `modelMetrics` (last shutdown per session), Gemini chat JSONL, pi usage and Kimi `wire.jsonl` `usage.record` fixtures each give the expected totals per leg.
- `Rtok.Agents.Tests` integration: the OpenCode/Kilo reader opens a fixture SQLite read-only, totals `message.data`, and leaves the database bytes unchanged.
- `Rtok.Agents.Tests` unit: a malformed line is skipped without failing the run, and Droid, Grok, ZCode and Antigravity are listed in `skipped`.
- `Rtok.Agents.Tests` unit: per-host dirs come from `[agents.usage.dirs]`, and the env overrides win over them.
- `Rtok.Agents.Tests` unit: tokens are input + cache_write + cache_read + output (+ reasoning), and the JSON keeps each leg separate.
- `Rtok.Agents.Tests` unit: `--tz` buckets across a spring-forward and a fall-back transition (a fixed clock and an IANA zone) put each event in its local day, with the 23 h and 25 h days complete.
- `Rtok.Agents.Tests` CsCheck: decimal SI units (22.1K, 155.45M, 26.52B) agree with the exact integers in JSON to the shown precision.
- `Rtok.Agents.Tests` unit: `--source`, `--host`, `--since/--until` (a date or `30d`/`24h`), `--daily/--monthly`, `--by agent|model` and `--unpriced` map to their `[agents.usage]` keys.
- `Rtok.Agents.Tests` unit: model ids lose the provider prefix and a `-YYYYMMDD` suffix before lookup, the raw id stays in JSON, cost uses only `[stats.prices]` with no network call, and a model without a price lands in `unpriced_models`.
- `Rtok.Agents.Tests` unit: `--source rtok` groups store usage by host (else `unattributed (<api>)`) and takes saved tokens only from Measurement rows, with expand counted as a cost.
- `Rtok.Cli.Tests` Verify: the `rtok agents usage` screen over the fixtures: header, `! Cost is incomplete` line, per-agent table by cost descending with `-` for an all-unpriced agent, and the monthly and `--daily` rows.
- `Rtok.Cli.Tests` Verify: `rtok agents usage --json` has `source, tz, through, totals{…}, unpriced_models, unpriced[], agents[], periods[], skipped[]`.

Spec: catalog B8 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T358, T358.1, T358.2, T358.3, T358.4, T358.5, T358.6, T389; open T396 ([open work](../../docs/port/open-work.md)).

Fuzz: `usage-logs` in `tests/Rtok.Fuzz`: each host log reader over any bytes never throws and gives non-negative totals.

Check: Reader fixtures for each host give the expected totals, and DST bucket tests pass.
