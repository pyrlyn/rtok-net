---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T47. OpenTelemetry export of the ledgers

Observability is a projection of the stored ledgers, at least once, with watermarks (rtok D19).

Done when:
- The `[otel]` config keys work.
- An OTLP/HTTP JSON encoder (STJ source gen) writes traces, logs and metrics, with GenAI semantic-convention mapping. Each stream has its own watermark and fails independently.
- Coalesced flushes run from the proxy, MCP and session end.
- `rtok otel flush|status` works.
- One real session shows up as one trace in SigNoz or Maple (rtok T395).

Tests:
- `Rtok.Otel.Tests` unit: `[otel]` defaults are `endpoint=""`, `service_name="rtok"`, `content=true`, `content_bytes=65536` and `flush_secs=5`, and an empty endpoint exports nothing.
- `Rtok.Otel.Tests` unit: `content_bytes` cuts exported bodies, and `content=false` exports none.
- `Rtok.Otel.Tests` Verify: the OTLP/HTTP JSON for traces, logs and metrics from a fixture store, with the `invoke_agent {host}` session span, its `gen_ai.*` and `rtok.*` attributes, and its call spans.
- `Rtok.Otel.Tests` unit: metrics are cumulative monotonic sums (temporality 2) of `rtok.tokens` per `gen_ai.token.type`, and semantic-cache hits are left out of billed totals.
- `Rtok.Otel.Tests` WireMock: each row is exported once across two flushes, at most 1000 rows per stream per request, and a watermark advances only after a 2xx.
- `Rtok.Otel.Tests` WireMock: an unreachable collector and a 5xx mark nothing, so the next flush sends the same rows.
- `Rtok.Otel.Tests` WireMock: when `/v1/logs` fails and traces and metrics succeed, only their watermarks advance.
- `Rtok.Otel.Tests` integration: several processes flushing at once never double-export, with at most one flush running and one queued on `<db>.otel-flush.lock` and `.otel-flush-queued.lock`.
- `Rtok.Otel.Tests` unit: a recorded fixture session exports every span under one 32-hex trace id with the `invoke_agent` span as root.
- `Rtok.Proxy.Tests` unit: the proxy flushes every `flush_secs` on a fake `TimeProvider`.
- `Rtok.Mcp.Tests` integration: `rtok mcp` runs a final flush at stdin EOF.
- `Rtok.Hooks.Tests` unit: Stop and SessionEnd start a detached `rtok otel flush` and return without waiting for it.
- `Rtok.Cli.Tests` Verify: `rtok otel status` shows the endpoint and each stream's watermark and never prints `headers`.
- `Rtok.Cli.Tests` WireMock: `rtok otel flush` exports pending rows to a fake collector and exits 0.

Spec: catalog A29 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T16.1, T16.2, T16.3, T16.4, T16.5, T16.6, T16.7, T16.9, T36.12, T45.1, T53.4, T143, T592, T624, T653; open T395 ([open work](../../docs/port/open-work.md)).

Fuzz: `otlp-encode` in `tests/Rtok.Fuzz`: any strings in ledger rows encode to valid OTLP JSON.

Check: A WireMock collector test shows that each row is exported once, that an unreachable collector marks nothing, and that concurrent flushes never double-export.
