---
status: todo
priority: P2
complexity: 5
readiness: 0%
agent:
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

Spec: catalog B9 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T28.0, T28.1, T28.2, T31.0, T31.1, T31.2, T205, T212, T259, T298, T303, T323, T385.1, T385.2, T385.4, T385.5, T385.6, T385.7, T385.8, T385.9.1, T385.9.2, T385.10, T385.11, T385.12.1, T385.12.2, T385.13, T432, T532, T533, T583, T584, T655, T657; open T385, T385.9 ([open work](../../docs/port/open-work.md)).

Check: Lane and cache-key table tests pass, batch results never double-count, and the agent lane is never capped or routed.
