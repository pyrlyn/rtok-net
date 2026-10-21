---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
---

# T15. MCP server: stdio, Streamable HTTP, wrap and one-shot call

The MCP server is the second agent-facing surface and carries every plugin's tools (rtok D21).

Done when:
- `rtok mcp` serves stdio with the ModelContextProtocol C# SDK. Tools come from the plugins plus `expand`; each description is at most 60 tokens; `readOnlyHint` is set where it applies; the `[mcp] tools` allow-list is honoured; one calls row is written per `tools/call`.
- Protocol details match rtok byte for byte: the `protocolVersion` echo, error codes and messages, `isError` on tool errors, validation of required parameters, and argument aliases. If the SDK cannot do this, the server is a hand-written JSON-RPC server instead.
- `rtok mcp --http`:
  - bound to 127.0.0.1:8791;
  - requires a bearer token of 16+ characters, compared in constant time;
  - refuses a non-loopback bind without TLS in front (rtok S8);
  - refuses to start in `/` or `$HOME` unless `allow_paths` names a project (rtok S1).
- The workspace root comes from MCP `roots`. When the cwd is not a repo, the error is a clear one (rtok T526).
- `rtok mcp -- <server>` wraps a foreign server: NDJSON and Content-Length framing, malformed frames forwarded, the exit code kept, only `tools/call` text shortened.
- `rtok mcp --call <tool> --json <args>` runs one tool once.

Spec: catalog A3, catalog B4 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T4.1, T36.10, T59.4, T70.3, T75, T142, T172, T190, T191, T192, T194, T213, T263, T265, T269, T353, T401, T458, T528, T537; open T526, S1, S8 ([open work](../../docs/port/open-work.md)).

Check: MCP contract goldens (initialize, tools/list, errors, a tools/call per tool) pass byte-exact over stdio and HTTP on the AOT binary.
