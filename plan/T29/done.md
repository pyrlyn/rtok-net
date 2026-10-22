# T29. Proxy lanes, batch, flex, routing, cache keys, compressor — done

What was done:

- Lanes (`src/Rtok.Proxy`):
  - `Lane` (agent, bulk, batch, files, embeddings, meta, internal) and `Lanes.Classify`: the
    path decides first (batch, files, embeddings and models/count-tokens on Anthropic, OpenAI
    and Gemini), then the `/lane/<name>/` prefix, then `x-rtok-lane`; nothing makes a request
    agent. The prefix is cut and the header is dropped before upstream; `calls.kind` is
    `api_request:<lane>` (`api_request` for the agent lane).
  - `LaneRules`: each rewrite switch (compress, toon, tools_rewrite, context_management,
    semantic_cache) only when the global switch and the lane's are both on. The read timeout
    is the shorter of the lane's and the global one, with one exception: a flex lane keeps what
    it set and, when it set nothing, waits at least 900 s, because flex answers slowly by design.
    The parent session was asked about this narrowing; no answer came, so it follows rtok
    T385.2 and T385.5.
  - Per-lane upstreams and `LaneGate` (`max_in_flight`, `max_queued`, then 429 with
    `Retry-After: 1`). The agent lane is never capped, queued or routed. Batch and files pass
    through.
- Routing and Flex:
  - `ModelRoute`: `[proxy.routing.bulk|internal]` swaps `model` only when `from`, `to`,
    `provider` and `gate` are all set, before the call is recorded.
  - `Flex` and `FlexRetry`: `service_tier = "flex"` on a lane that asks for it, and `on_429`
    `none`, `backoff` (at most 5 retries under a 30 s cap, on `ProxyOptions.Time`) or `default`
    (one retry on standard processing).
- Cache:
  - `CacheKey` per catalog B9, at parity with rtok. Included in the key: extra top-level
    params, canonical tools, tool_use input, image/document source, the typed message digest,
    the caller identity headers, the method and the upstream URL. Excluded params and message
    `cache_control` do not change it. A tool definition's `cache_control` does change the key,
    because rtok's tools fingerprint keeps it.
  - `ResponseCache` (the direct tier) keeps whole 2xx bodies in memory for `ttl_s`. It never
    caches Responses API traffic, streams, a capped tee or a 4xx/5xx.
  - The embedding (semantic) tier is not built; only the direct tier exists.
- Batch:
  - `rtok batch submit|status|fetch` (`src/Rtok.Cli/BatchApi.cs`, `BatchCommands.cs`) for
    `--provider anthropic|openai`, through the proxy by default. Fetch refuses an existing
    output file and removes a partial one on failure.
  - `[proxy.batch] parse_results` (`BatchResults`): a 2xx GET of `/v1/messages/batches/<id>/results`
    or `/v1/files/<id>/content` writes one usage row per result line, keyed by
    `sha256(api, target, line id)`, so fetching twice adds nothing. A file fetch tags its call
    `api_request:batch`.
  - `calls.service_tier` is read from the response (`UsageReader.ServiceTier`).
- Compressor (rtok T385.9.1, T385.9.2), behind `[plugins.compress.llm] enabled` (off by default):
  - `MustKeep`: the span extractor and guard, with source-generated regexes.
  - `compress-prompt.toml` is embedded as data. `CompressPrompt.Version` is the first 12 hex
    characters of the file's SHA-256, plus the model.
  - `Compressor`: one Anthropic Messages request per archive id to the proxy's own `internal`
    lane (or `url`). It forwards only the agent's credential and version headers, and only from
    the Anthropic wire (`CompressAuth`). Outcomes are cached, skipped, rejected, failed or
    stored.
  - `SummaryQueue`: a compare-and-swap in-flight cap, a seen set bounded at 10 000, and jobs off
    the caller's thread.
  - `SummaryView`: renders `summary + expand(<id>)`, offers ids that have no summary yet, and is
    used only when shorter than the pointer.
  - `RtokStore.GetArchiveSummary` / `PutArchiveSummary`: the first writer wins.
- Fuzz target `lane-classify` (`tests/Rtok.Fuzz/Targets/LaneClassify.cs`) with a seed corpus.

Left for T28: the archive pointers and decisions that `SummaryView` and `SummaryQueue.Submit`
plug into do not exist yet. T28's compress pass calls `SummaryView.Stored` for each pointer
outside the live zone. After that pass the handler calls `SummaryQueue.Submit` with
`CompressAuth.FromHeaders`. Until then the compressor runs only when called directly, and the
agent request path is unchanged with the flag on or off.

Commit: `T29: Proxy lanes, batch, flex, routing, cache keys, compressor`.

New test dependencies: WireMock.Net in `Rtok.Cli.Tests` (a fake batch API for `rtok batch`
tests); CsCheck in `Rtok.Proxy.Tests` (lane-policy and cache-key properties).

How it was checked:

- `Rtok.Proxy.Tests` (214), `Rtok.Cli.Tests` (`BatchCommandTests`, goldens regenerated),
  `Rtok.Archive.Tests` (the first-summary-wins store test) and `Rtok.Fuzz.Tests`
  (`lane-classify` corpus, random and mutated inputs) all pass.
- Full gates:
  - `dotnet build rtok.slnx -c Release -warnaserror --no-incremental`
  - `dotnet test`
  - `dotnet csharpier check .`
  - `npx --yes markdownlint-cli2@0.23.3`
