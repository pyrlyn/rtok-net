---
status: in progress
priority: P2
complexity: 5
readiness: 5%
agent: Claude Code / claude-opus-5-5
---

# T29. Proxy lanes, batch, flex, routing, cache keys, compressor

Different kinds of traffic need different policies; the agent lane is never slowed or rewritten into a batch (rtok T385).

Done when:
- The lane classifier works: path first, then the `/lane/<name>/` prefix, then the `x-rtok-lane` header; markers are stripped before upstream.
- Per-lane policy can only narrow the global one. Per lane: upstream, in-flight caps, and 429 Retry-After when the queue is full.
- The semantic and direct cache keys match the catalog.
- `rtok batch submit|status|fetch` works, with `parse_results` deduped into usage.
- Flex: `service_tier` and the 429 policy.
- Routing never acts on the agent lane.
- The async LLM compressor and summaries run on the internal lane with the must-keep guard (rtok T385.9), behind an off flag.

Tests:
- `Rtok.Proxy.Tests` unit: the lane table classifies by path (batch, files, embeddings, models/count-tokens on Anthropic, OpenAI and Gemini), then the `/lane/<name>/` prefix, then `x-rtok-lane`, and sets `calls.kind` to `api_request:<lane>`.
- `Rtok.Proxy.Tests` WireMock: the `/lane/<name>/` prefix and the `x-rtok-lane` header never reach upstream.
- `Rtok.Proxy.Tests` CsCheck: for any global policy and lane override, the effective lane policy (compress, toon, tools_rewrite, context_management, semantic_cache, timeout_s) is never wider than the global one.
- `Rtok.Proxy.Tests` WireMock: a lane goes to its own upstream, and with `max_in_flight` 1 and `max_queued` 8 the next request gets 429 with Retry-After.
- `Rtok.Proxy.Tests` WireMock: the agent lane under the same load is never capped, queued or routed, even when a routing rule would match.
- `Rtok.Proxy.Tests` unit: the cache-key table changes the key for each included field (extra top-level params, canonical tools, tool_use input, image/document source, typed message digest, caller identity headers, method, upstream URL) and keeps it for excluded params and `cache_control`.
- `Rtok.Proxy.Tests` CsCheck: the cache key does not depend on top-level param order or on `cache_control`, and two different `x-api-key` values never share a key.
- `Rtok.Proxy.Tests` WireMock: only whole 2xx bodies are cached, and a 4xx, a 5xx, a cut stream or Responses API traffic is never served from cache.
- `Rtok.Cli.Tests` WireMock: `rtok batch submit <jsonl>`, `status <id>` and `fetch <id> <out>` work against a fake batch API for each `--provider`.
- `Rtok.Proxy.Tests` integration: `parse_results` writes usage rows keyed by `sha256(api, target, line id)` with `calls.service_tier` from the response, and fetching the same results twice adds no row.
- `Rtok.Proxy.Tests` WireMock: flex requests carry `service_tier`, and each `on_429` value (none, backoff, default) behaves as in the catalog, backoff retrying at most 5 times under a 30 s cap on a fake `TimeProvider`.
- `Rtok.Proxy.Tests` unit: routing `[proxy.routing.bulk|internal]` acts only when from, to, provider and gate are all set.
- `Rtok.Proxy.Tests` WireMock: with the compressor flag off requests are byte-identical, and on it runs on the internal lane without delaying the agent request, the first summary wins in `archive_summaries`, and archived blocks outside the live zone become `summary + expand(<id>)`.
- `Rtok.Proxy.Tests` unit: the must_keep guard rejects a summary that drops an identifier, path, number or error line from its original.

Spec: catalog B9 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T28.0, T28.1, T28.2, T31.0, T31.1, T31.2, T205, T212, T259, T298, T303, T323, T385.1, T385.2, T385.4, T385.5, T385.6, T385.7, T385.8, T385.9.1, T385.9.2, T385.10, T385.11, T385.12.1, T385.12.2, T385.13, T432, T532, T533, T583, T584, T655, T657; open T385, T385.9 ([open work](../../docs/port/open-work.md)).

Fuzz: `lane-classify` in `tests/Rtok.Fuzz`: any path and headers give one lane with the markers stripped, the agent lane is never routed, and cache keys are deterministic.

Check: Lane and cache-key table tests pass, batch results never double-count, and the agent lane is never capped or routed.

Plan:
1. `Rtok.Proxy` lanes: `Lane` (agent, bulk, batch, files, embeddings, meta, internal) and
   `LaneClassifier` (path, then `/lane/<name>/`, then `x-rtok-lane`; markers stripped), the
   effective policy (global switch AND lane switch), per-lane `HttpClient` timeouts and
   upstreams, and `LaneGate` (max_in_flight, max_queued, 429 + Retry-After). `calls.kind`
   from the lane.
2. Routing (`[proxy.routing.bulk|internal]`, all fields set, never the agent lane) and Flex
   (`service_tier`, `on_429` none, backoff, default on a `TimeProvider`).
3. Cache keys (direct tier) per catalog B9 and an in-memory TTL cache of whole 2xx bodies,
   never Responses API traffic, scoped by caller identity, method and upstream URL.
4. `rtok batch submit|status|fetch` and `[proxy.batch] parse_results` usage rows keyed by
   `sha256(api, target, line id)`, `calls.service_tier` from the response.
5. The LLM compressor on the internal lane behind `[plugins.compress.llm]`, `archive_summaries`
   first writer wins, and the must_keep guard.
6. Tests (unit, CsCheck, WireMock), fuzz target `lane-classify`, gates.
