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

Tests:
- `Rtok.Mcp.Tests` Verify: `tools/list` holds the plugins' tools plus `expand`, with `readOnlyHint` on the read-only ones.
- `Rtok.Mcp.Tests` unit: every tool description is at most 60 tokens by the estimator.
- `Rtok.Mcp.Tests` unit: the `[mcp] tools` allow-list hides unlisted tools, always keeps `expand`, and a hidden tool's call answers "unknown tool: <name>".
- `Rtok.Mcp.Tests` integration: each `tools/call` writes exactly one calls row with kind `mcp_call`.
- `Rtok.Mcp.Tests` Verify: wire JSON for `initialize` echoing a supported `protocolVersion` or else `2025-06-18`, and for `-32700`, `-32600` and `-32601` "Method not found" with the method in data.
- `Rtok.Mcp.Tests` unit: a `tools/call` missing a required field answers `isError` "invalid params: missing `x`" with `(got: <keys>)`, and every tool error carries a single "Error:" prefix.
- `Rtok.Mcp.Tests` unit: blank lines and notifications get no reply, and a `Content-Length` body is read as bytes with a 64 MiB cap.
- `Rtok.Mcp.Tests` unit: line ranges `a-b`, `"a-b"`, `a,b` and `a, b` and `query` for `pattern` reach the tool as the same arguments.
- `Rtok.Mcp.Tests` integration: `rtok mcp --http` binds 127.0.0.1:8791 and answers 401 with no bearer, a wrong one or one under 16 characters, and the token never appears in logs or output.
- `Rtok.Mcp.Tests` integration: `--http` refuses to start on a non-loopback bind without TLS in front (rtok S8) and in `/` or `$HOME` unless `allow_paths` names a project (rtok S1).
- `Rtok.Mcp.Tests` integration: in a non-repo cwd the root comes from the client's `roots/list`, and with no roots `worktree_list` answers "pass a path or open a project" instead of git's fatal message.
- `Rtok.Mcp.Tests` integration: `rtok mcp -- <fake server>` over NDJSON and Content-Length passes `tools/list` byte-identical, forwards a malformed frame raw and resyncs, leaves `isError` results whole and keeps the server's exit code.
- `Rtok.Mcp.Tests` CsCheck: the wrapper shortens only `tools/call` `result.content[].text`, and the shortened text comes back byte for byte through `expand`.
- `Rtok.Cli.Tests` integration: `rtok mcp --call <tool> --json <args>` prints the same result as the stdio `tools/call` and leaves no child process behind on any exit path.

Spec: catalog A3, catalog B4 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T4.1, T36.10, T59.4, T70.3, T75, T142, T172, T190, T191, T192, T194, T213, T263, T265, T269, T353, T401, T458, T528, T537; open T526, S1, S8 ([open work](../../docs/port/open-work.md)).

Fuzz: `mcp-jsonrpc` in `tests/Rtok.Fuzz`: NDJSON and Content-Length streams into the stdio server with stub tools get a JSON-RPC reply or are dropped by the spec, and the wrap splitter forwards malformed frames unchanged.

Check: MCP contract goldens (initialize, tools/list, errors, a tools/call per tool) pass byte-exact over stdio and HTTP on the AOT binary.
