# T13. Done

`rtok hook <event>` and `rtok hook --serve` now run one pipeline (`Rtok.Hooks/HookPipeline`)
from the host's bytes to the host's reply, for every host in the catalog. Commit: `T13: Hook
surface: rtok hook <event> for every host`.

What was done:
- **Event table** (`Rtok.Hooks/Events/HookEvents`): one row per host, host event, rtok event
  and matcher for the 13 hosts, as rtok's table has them. `HookManifests` writes each host's
  `hooks.json` from it (Claude also gets the D31 worktree entries), and lists the script
  names for Cline and Command Code. Verify snapshots hold every manifest; T36 writes the
  `plugins/` trees and its drift test compares them with the generator (moved by the creator).
- **Envelopes** (`Rtok.Hooks/Hosts`): `HookInput` is the canonical (Claude-shaped) payload,
  `HostAdapters` brings Cursor, Copilot, Gemini, Codewhale, Cline, Devin, Command Code and
  Grok to it, and `HostWriters` writes a `HookReply` in each host's shape. The empty reply is
  `{}` everywhere.
- **Config**: `LoadedConfig.Json(key)`, the plugins' `IHost.ConfigJson`, delegating to
  `ConfigJson.Find` (T43's, kept as the one TOML-to-JSON writer).
- **Store**: the hook lock policy (`FailFast`: 5 ms, one attempt), `BeginBatch()` for one
  write transaction per event, `EndSession`, `StoreLockedException`.
- **Runtime** (`Rtok.Store/StoreRuntime`): the store-backed `ICapabilities`. It lives in the
  store, which already references `Rtok.Core` (T17), so the two keep one direction. The members
  other tasks own answer empty or throw into the plugin guard (`StoreRuntime.Pending.cs`:
  archive T9, notes T22, read cache and symbols T24).
- **Pipeline**: the stdin cap from `core.hook_max_input_bytes`; the adapter; the once key
  `<event>:<tool_use_id>` (the pipeline keeps the last 256 replies, so a resident runs the
  plugins once; the measurement is deduped by the once key across processes); the dispatch for
  PreToolUse, PostToolUse, SessionStart, UserPromptSubmit, SubagentStart, PreCompact,
  PostCompact (Claude's with a summary answers `{}`, the rest inject as
  SessionStart("compact")) and SessionEnd (`ended_at`); the slow note over `[hook] max_ms`
  (stderr and a warn row; 0 turns it off). Every row of an event (session, call, call_io,
  measurements, logs) lands in one transaction. A locked store answers `{}` with one stderr
  line and writes nothing.
- **SessionEnd retry** (`HookDeferral`): a SessionEnd turned away by the lock is spooled to
  `<home>/run/session-end-<guid>.json` (0600) and handed to a detached `rtok hook SessionEnd`
  through `RTOK_HOOK_DEFERRED`; that child waits for the lock with the steady policy. Only a
  spool file directly in `<home>/run` is read.
- **Worktree events**: `rtok hook WorktreeCreate|WorktreeRemove` exits 1 with one stderr line,
  so the plugin's `worktree.sh` falls back to the host's own worktree and Remove deletes
  nothing (D31). The git arm and its test moved to T40.1 (L7), by the creator's decision.
- `Rtok.Cli` imports `StoreHost.targets`: it opens the store now.
- `HookSandbox` retries deleting its home for up to 5 s: the resident now writes its store
  there and can still be writing when a smoke test ends.

Checked:
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet csharpier check .` and
  `npx --yes markdownlint-cli2@0.23.3` are green. `dotnet test`: 2203 of 2276 pass, 71 skip,
  2 fail outside T13's code: `ProgressRunnerTests.An_inherited_slow_child_is_killed_at_the_timeout`
  is a timing bound that fails under nine lanes' load and passes alone, here and on `main`; the
  `agents-install` fuzz target (T35) fails on mutated seeds (CsCheck seed `0g1WRfv-8Rm9`, a Goose
  YAML with a `? rtok:` complex key), reported to the creator's session.
- `Rtok.Hooks.Tests`: table uniqueness; manifest snapshots; envelope
  snapshots for Claude, Codex, Copilot, Cursor, Gemini, Devin and Grok on the four events each
  host has; CsCheck fail-open (random, truncated and non-UTF-8 stdin, 8 hosts × 8 events, the
  no-op reply and no plugin run); the cap and `core.hook_max_input_bytes`; once in one and in
  two processes; slow and `max_ms = 0`; the locked store; the SessionEnd retry against the
  published `rtok`; one commit per SessionStart, SubagentStart and PreCompact.
- `Rtok.Smoke`: the fail-open matrix on the AOT binary (5 hosts × 8 events × empty, garbage,
  truncated, non-UTF-8 and 1 MiB stdin, exit 0 and `{}`), and every `tests/fixtures/hooks`
  payload with the store locked. With `HookTests`: 90 of 90 pass.
- `Rtok.Fuzz`: the `hook-io` target (`Targets/HookIo.cs`): the first byte picks the host and
  the event, the rest is stdin through `HookInput.Parse`, the host's adapter and its writer;
  an adapted payload stays a JSON object, every reply is JSON and the empty one is `{}`. Its
  corpus is the 25 host fixtures; `Rtok.Fuzz.Tests` runs it with random and mutated input.
- `Rtok.Latency` runs the new SessionStart, SubagentStart, PreCompact, PostCompact,
  UserPromptSubmit and SessionEnd fixtures. Under load average 170 the medians were 11 to 24 ms
  (p95 18 to 70 ms), against the 5.5 ms quiet baselines, so the 10 ms median is **not proven**
  here; the baseline was not updated. The quiet-machine run goes with the 40-notes check below.

Not done here, and where it goes:
- The 40-notes check (note bodies read once) and the Latency median with 40 notes on a quiet
  machine need notes: T22. One transaction per event is checked now; the note half lands with
  T22's injections.
- WorktreeCreate printing a path and WorktreeRemove refusing a dirty worktree: T40.1 (L7 writes
  it). Through the resident the worktree events answer `{}`; T40.1 routes them.
- The `plugins/*/hooks.json` drift test: T36 (added to its task).
- Agent registration and the core injections: T37. Archive, so a PostToolUse overflow is not
  lossless yet: T9. Cursor's `updated_mcp_tool_output`: the MCP wrap. The over-cap and failure
  lines go to stderr only; they are not in T10's rotating log yet.
- The session's project is the main checkout's directory name; rtok's origin-URL
  `project_name` has no port yet.
