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

Spec: catalog A29 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T16.1, T16.2, T16.3, T16.4, T16.5, T16.6, T16.7, T16.9, T36.12, T45.1, T53.4, T143, T592, T624, T653; open T395 ([open work](../../docs/port/open-work.md)).

Check: A WireMock collector test shows that each row is exported once, that an unreachable collector marks nothing, and that concurrent flushes never double-export.
