---
status: in progress
priority: P2
complexity: 5
readiness: 5%
agent: Claude Code / claude-opus-5-5
---

# T28. Proxy rewriters: archive pointers, toon, json_tree, tools_rewrite

Compress mode rewrites old tool results without touching the system prompt, tools or the last two turns, and stays byte-stable for the prompt cache.

Done when:
- Old large tool results become archive pointers, keyed by (session, tool_use_id) and persisted. `expand` freezes a pointer.
- Skill bodies are archived outside the live zone.
- toon tables and json_tree folding work, each archiving the original first.
- `tools_rewrite`, `compact_schema`, `context_management`, terminal-noise stripping and `live_blobs` work. `live_blobs` never touches image or document data (rtok T55.15).
- The compressor hookup (split from T29): with `[plugins.compress.llm]` on, a pointer outside the live zone becomes `SummaryView.Stored` (`summary + expand(<id>)`) once a summary exists, and the ids without one go to `SummaryQueue.Submit` with `CompressAuth.FromHeaders` after the rewrite pass.

Tests:
- `Rtok.Proxy.Tests` unit: in compress mode a tool_result older than `keep_turns` (4) and over `min_tokens` (1500) becomes `[archived <id>: first 8 lines …`, while the system prompt, `tools[]` and the last two turns stay byte-identical.
- `Rtok.Proxy.Tests` integration: replaying a recorded session twice, and again after a proxy restart on the same store, gives byte-identical upstream requests because decisions are persisted by (session, tool_use_id).
- `Rtok.Proxy.Tests` integration: after `expand` freezes an id the proxy stops rewriting that tool_result in later requests.
- `Rtok.Proxy.Tests` CsCheck: every pointer, toon table and json_tree fold in a replayed session expands back to its original byte for byte.
- `Rtok.Proxy.Tests` unit: skill bodies (`isMeta` records) outside the live zone are archived, and inside it they are left alone.
- `Rtok.Plugins.Toon.Tests` Verify: a JSON array of objects in a tool result, and one inside an MCP `[{type:text}]` block, renders as a TOON table with `\n \r \t` escaped in cells.
- `Rtok.Plugins.JsonTree.Tests` Verify: json_tree folds a large JSON tool result before the archive pointer is placed, and the original is archived first.
- `Rtok.Proxy.Tests` Verify: `tools_rewrite` with `max_description_tokens` 60 cuts descriptions at a sentence boundary, honours allow/deny, leaves schemas byte-identical and archives the original `tools[]` content-addressed.
- `Rtok.Proxy.Tests` Verify: `compact_schema` drops duplicate defaults and examples and compacts enums.
- `Rtok.Proxy.Tests` unit: `context_management=true`, or `RTOK_PROXY_CONTEXT_MANAGEMENT`, adds the `clear_tool_uses_20250919` edit, and false leaves the body unchanged.
- `Rtok.Proxy.Tests` CsCheck: terminal-noise stripping removes ANSI, control and zero-width characters, keeps `\n \r \t`, never touches assistant turns, and stripping twice equals stripping once.
- `Rtok.Proxy.Tests` unit: `live_blobs` shrinks JSON and code text blocks in the live zone and leaves every image and document block byte-identical (rtok T55.15).
- `Rtok.Proxy.Tests` unit: each rewrite writes one Measurement row whose before and after bytes equal the original and the sent bytes.
- `Rtok.Proxy.Tests` WireMock: in passthrough mode no rewriter runs, and a rewriter that throws in compress mode forwards the original body.

Spec: catalog A14, catalog A6 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T5.3, T5.4, T11.7, T11.8, T36.1, T36.2, T55.15, T59.5, T61.2, T127, T609; open T55.15 ([open work](../../docs/port/open-work.md)).

Fuzz: `proxy-rewrite` in `tests/Rtok.Fuzz`: any request through each rewriter is valid JSON, deterministic, leaves the system prompt, tools and last two turns byte-identical, and every pointer expands back.

Check: Replaying a recorded session twice gives byte-identical requests, every pointer expands back to its original, and the image/document test passes.

Plan:
1. Store: `GetArchiveDecision` by (session, tool_use_id); `StoreRuntime` serves the archive
   capabilities (put, get, decisions, summaries) instead of the pending stubs, and takes the
   call it runs under.
2. Wires implement `IToolResults`: Anthropic (results, skill bodies, live blobs), Chat (results,
   live blobs), Responses and Gemini (results).
3. Plugins `Rtok.Plugins.Archive` (pointers, skills, live_blobs), `Rtok.Plugins.Toon` (the one
   TOON encoder, the report's table rendering uses its cells) and `Rtok.Plugins.JsonTree`,
   linked in `eng/RtokPlugins.targets`.
4. `Rtok.Proxy` compress pass: `Dispatcher.ProxyFilter`, the compressor hookup
   (`SummaryView`, `SummaryQueue`), terminal-noise stripping, `context_management`,
   `tools_rewrite` with `compact_schema`, one Measurement per rewrite; any throw forwards the
   original body.
5. Tests as listed, fuzz target `proxy-rewrite`, gates.
