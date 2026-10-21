---
status: todo
priority: P1
complexity: 5
readiness: 0%
agent:
---

# T27. Proxy core: Kestrel passthrough, SSE, wires, usage

The proxy sees real token usage and is where the live zone of old tool results gets rewritten (rtok D9, D11).

Done when:
- `rtok proxy` runs on Kestrel slim at 127.0.0.1:8790.
- Plain mode forwards bytes untouched.
- SSE passes through with a bounded tee for usage parsing.
- Wire adapters exist for Anthropic Messages, OpenAI Chat Completions, OpenAI Responses and Gemini.
- One usage row is written per request, cache hits and errors included.
- The hop header and loop detection work.
- `rtok wrap -- <cmd>` works.
- `/health` and `/live` answer only on loopback or behind the dashboard token (rtok S5). A Host header containing `,` gets 400 (rtok S6).
- TLS uses the platform store plus `SSL_CERT_FILE` and fails closed.

Spec: catalog A13 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T5.1, T5.2, T5.5, T11.1, T11.2, T11.3, T11.4, T11.6, T34.6, T36.18, T45.2, T51.1, T51.2, T51.3, T51.4, T55.14, T326, T686; open S5, S6 ([open work](../../docs/port/open-work.md)).

Check: WireMock upstream tests cover passthrough byte-identity, SSE usage for each wire, and loop detection, and the proxy fixture bench is recorded.
