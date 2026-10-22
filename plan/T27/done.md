# T27. Proxy core: Kestrel passthrough, SSE, wires, usage — done

What was done:
- `src/Rtok.Proxy` (new, app layer, Kestrel through the `Microsoft.AspNetCore.App` framework
  reference):
  - `Wire` and its four adapters: `AnthropicWire` (`/v1/messages`, session from
    `metadata.user_id`), `OpenAiChatWire` (`/v1/chat/completions`, session from `user`, injects
    `stream_options.include_usage` into streamed requests when `[proxy] include_usage` is on),
    `OpenAiResponsesWire` (`/v1/responses`, usage only from the terminal SSE events) and
    `GeminiWire` (`:generateContent` and `:streamGenerateContent`, model from the path,
    `thoughtsTokenCount` billed as output). `/v1/files` and `/v1/batches` without
    `anthropic-version` go to `openai_upstream`.
  - `UsageReader.FromResponse`: JSON or SSE, SSE decoded per `data:` line with lossy UTF-8, so a
    cut multibyte character loses only its event. `Usage.Merge` keeps the latest non-zero
    counter; input excludes the cache, so the counters are disjoint.
  - `UpstreamUrl.Join` (upstreams with a path or a trailing slash), `Hops` (`x-rtok-proxied`,
    a non-number counts as 1), `ProxyUrls` (the base URLs `wrap` and T35.4 share), `LiveCalls`
    (the `/live` ring) and `ProxyTls` (platform roots plus the `SSL_CERT_FILE` bundle; a
    missing, empty or unparsable bundle throws `ProxyTlsException`, never falls back).
  - `ProxyServer`: Kestrel slim (`CreateEmptyBuilder` + `UseKestrelCore`) on `[proxy]
    bind:port`, default 127.0.0.1:8790; a hostname bind is refused. `/health`, `/live` and one
    forwarding handler.
  - `ProxyHandler`: an `HttpClient` forwarder (no YARP; the tee and the recording need the
    body). It refuses a Host containing `,` with 400 (S6) and answers `/health` and `/live`
    only to loopback or with `ProxyOptions.AccessToken` as Bearer or `?token=` (S5). Hops ≥ 8
    get 508 without an upstream call; hops > 0 or any kill switch take the plain path, which
    forwards bytes and headers untouched apart from the hop header (hops > 0 also logs a warn
    line). The recording path forces `Accept-Encoding: identity`, streams the response to the
    client while a bounded tee (`TeeBytes`) keeps the head for usage, and after the response
    completes writes the call, its `call_io`, one `usage` row (zeros when there are no
    counters, upstream answers 5xx or cannot be reached) and the provider `tokens` row.
- `Rtok.Core.AccessToken`: the one place an rtok HTTP token is made and checked (32 random
  bytes, `Authorization: Bearer` or `?token=`, constant-time compare); there was no `WebToken`
  on `main` yet, so T44's `rtok web` should take its S2 token from here. `rtok proxy` makes its
  own per-run token for a non-loopback bind (a loopback bind only sees loopback clients, which
  need none) and prints it once to stderr; it is never written to disk. The proxy's token is
  its own: `rtok web` and `rtok proxy` are separate processes, each with its own token, which
  T49's threat model should record. A non-loopback client without it gets 403 on `/health`
  and `/live`.
- `Json.String` returns null for a JSON string with invalid UTF-8 (`GetString` throws
  `InvalidOperationException`); the fuzzer found it.
- Rtok.Cli:
  - `rtok proxy [--port] [--upstream] [--mode] [--dry-run] [--config]`: `--dry-run` prints the
    effective `[proxy]` settings; otherwise it serves until Ctrl+C. The flags go through the
    config layer as TOML values (`proxy.port` as a number).
  - `rtok wrap -- <cmd>`: if `/health` does not answer within 500 ms it starts the proxy
    in-process, then runs the command with `ANTHROPIC_BASE_URL` and `OPENAI_BASE_URL` on it and
    exits with the child's code (127 when it cannot start). A proxy that cannot start (port
    held, store error) logs a warning and runs the command without it.
  - `docs/cli/commands.json`, help, man and completion snapshots regenerated.

How it was checked:
- `Rtok.Proxy.Tests` (75): the default bind and refused hostnames; the path table and the
  upstream URL join; plain passthrough byte for byte with headers; the SSE stream larger than
  the tee reaching the client whole; per wire, JSON and SSE, one usage row with the right
  `usage.api` and disjoint counters (cache read/write, Gemini thoughts as output); lossy SSE;
  a cache hit, an upstream 529 and an unreachable upstream each one row; a silent upstream
  timing out; the hop header (forwarded as hops+1, plain with a warn line, 508 at 8, a
  non-number as 1); `/health` and `/live` for loopback, non-loopback without and with the
  token; Host with `,` → 400 and nothing forwarded; a self-signed localhost upstream failing
  closed and passing with its CA in `SSL_CERT_FILE`, and bad bundles throwing.
- `Rtok.Cli.Tests` `ProxyCommandTests`: `wrap` starts the proxy and the child sees both base
  URLs and its exit code comes back; a held port fails open; `wrap` with no command exits 2;
  `proxy --dry-run` prints the settings.
- `Rtok.Fuzz` target `proxy-wire` (corpus 01–09): each wire's usage parser, the session and
  model reads and request shaping on any bytes, and a plain-mode relay that must return the
  bytes unchanged unless the Host check refused them.
- `Rtok.Smoke` `ProxyTests`: the published NativeAOT `rtok proxy` answers `/health` on
  loopback and stops on Ctrl+C.
- `Rtok.Benchmarks` `ProxyBenchmarks`: rtok's 40 KB Messages fixture with a streamed answer,
  direct, through the plain proxy and through the recording proxy; recorded in `research.md`
  section G.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier check .`
  and `npx --yes markdownlint-cli2@0.23.3` green.

New dependencies: the `Microsoft.AspNetCore.App` framework reference in Rtok.Proxy (Kestrel);
WireMock.Net in Rtok.Proxy.Tests and Rtok.Benchmarks (the upstream stub).

Follow-up: the `agents-install` fuzz target (T35) failed once on a random seed
(`1myGq7o59rCg`, input base64 `ASpjcFNlcnZlcnM6IFtdCm5hbWU6IG4K`); not T27's, reported to the
lane lead.

Commit: `T27: Proxy core: Kestrel passthrough, SSE, wires, usage` on `main`.
