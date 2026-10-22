---
status: in progress
priority: P1
complexity: 5
readiness: 10%
agent: Claude Code / claude-opus-5-5
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

Tests:
- `Rtok.Proxy.Tests` unit: the default options bind 127.0.0.1:8790 and never a wildcard address.
- `Rtok.Proxy.Tests` unit: the path-to-provider/api table maps `/v1/messages`, `/v1/chat/completions`, `/v1/responses` and Gemini `:generateContent`/`:streamGenerateContent`, and joins upstream URLs that carry a path or a trailing slash correctly.
- `Rtok.Proxy.Tests` WireMock: in plain mode request and response bodies and headers pass byte for byte, apart from the hop header.
- `Rtok.Proxy.Tests` WireMock: an SSE stream reaches the client unchanged chunk by chunk, also when it is larger than the usage tee's bound.
- `Rtok.Proxy.Tests` WireMock: for each wire (Anthropic Messages, OpenAI Chat Completions, OpenAI Responses with `response.completed`, Gemini) JSON and SSE responses give one usage row with the right `usage.api` and disjoint counters, including cache read/write and `thoughtsTokenCount` as output_extra.
- `Rtok.Proxy.Tests` unit: SSE usage is decoded per event with lossy UTF-8, so a multibyte character cut at the tail loses only that event.
- `Rtok.Proxy.Tests` WireMock: a cache-hit response, an upstream 5xx and an upstream connection failure each write exactly one usage row.
- `Rtok.Proxy.Tests` WireMock: `x-rtok-proxied` is forwarded as hops+1, hops>0 takes the plain path with no usage row and a warn line, hops>=8 returns 508 without calling upstream, and a non-number counts as 1.
- `Rtok.Proxy.Tests` integration: `/health` and `/live` answer a loopback client, refuse a non-loopback one without the dashboard token, and answer it with the token.
- `Rtok.Proxy.Tests` unit: a Host header containing `,` gets 400 and nothing is forwarded.
- `Rtok.Proxy.Tests` integration: a self-signed localhost upstream fails closed by default and succeeds when its CA is given in `SSL_CERT_FILE`.
- `Rtok.Cli.Tests` integration: `rtok wrap -- <cmd>` starts the proxy when none is up and the child sees `ANTHROPIC_BASE_URL` and `OPENAI_BASE_URL` pointing at it.
- `Rtok.Fuzz` fuzz: the `proxy-wire` target feeds arbitrary bodies and SSE to every wire's usage parser without an exception.
- `Rtok.Smoke` smoke: the published `rtok proxy` starts on Kestrel slim and answers `/health` on loopback.
- `Rtok.Benchmarks` bench: the proxy fixture bench replays the proxy fixture through a WireMock upstream and its result is recorded.

Spec: catalog A13 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T5.1, T5.2, T5.5, T11.1, T11.2, T11.3, T11.4, T11.6, T34.6, T36.18, T45.2, T51.1, T51.2, T51.3, T51.4, T55.14, T326, T686; open S5, S6 ([open work](../../docs/port/open-work.md)).

Fuzz: `proxy-wire` in `tests/Rtok.Fuzz`: request and SSE bodies for each wire give usage or none, plain passthrough bytes stay unchanged, and the Host header check never throws.

Check: WireMock upstream tests cover passthrough byte-identity, SSE usage for each wire, and loop detection, and the proxy fixture bench is recorded.

Plan:
1. `src/Rtok.Proxy` (app layer, Kestrel through `Microsoft.AspNetCore.App`): the four wires
   (`Wire` with Anthropic Messages, OpenAI Chat, OpenAI Responses and Gemini), the shared usage
   walk, SSE usage per event with lossy UTF-8, the upstream URL join, the hop count, the Host
   check, the live ring, TLS (platform roots plus `SSL_CERT_FILE`, fail closed) and the proxy
   base URLs that `wrap` and T35.4 share.
2. `ProxyServer`: Kestrel on `[proxy] bind:port` (default 127.0.0.1:8790), `/health`, `/live`
   and one forwarding handler. An `HttpClient` forwarder, no YARP: the tee and the recording
   need the body anyway. Plain mode (a kill switch or hops > 0) forwards bytes untouched;
   otherwise the call, its `call_io`, one `usage` row (zeros when there are no counters or
   upstream fails) and the provider `tokens` row are written after the response completes.
3. Rtok.Cli: `rtok proxy [--port] [--upstream] [--mode] [--dry-run] [--config]` and
   `rtok wrap -- <cmd>` (starts the proxy in-process when `/health` does not answer, sets
   `ANTHROPIC_BASE_URL` and `OPENAI_BASE_URL`, exits with the child's code, fails open).
4. Tests: `tests/Rtok.Proxy.Tests` (unit, WireMock upstreams, TLS with a generated
   self-signed CA), `Rtok.Cli.Tests` for `wrap`, fuzz target `proxy-wire`, a smoke test on
   the published binary, and a proxy fixture bench in `Rtok.Benchmarks` recorded in
   `research.md`.
