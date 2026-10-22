# Behaviour catalog ported from rtok

This is the functional spec for the .NET port. It lists what the original rtok
(https://github.com/pyrlyn/rtok, at `ef091589`) ships, area by area, as observable
contracts: commands, flags, formats, budgets and invariants. Each area names the rtok task
ids it came from, so a reader can look up the original card in rtok's `done.md`.

It was compiled from rtok's `done.md` (1164 closed tasks) in two halves, A and B. An area can
show up in both halves; read both entries. The tasks in `plan/` link to these sections in
their `Ports:` line. Lists named "drop" hold rtok tasks that are specific to its old
toolchain or were process-only, and are not ported.

Library choices for each area live in `research.md` and `plan/README.md`, not here.

## A. First half of rtok's done log

Every T-id that appears as a heading (`##`/`###`) or a bold `**Txx` entry in done.md lines 1-7800 appears exactly once in an `IDs:` line below (684 ids). A heading such as `T454` that occurs more than once, or `T127` (two headings), is listed once, and all of its parts are covered under that area.

---

### A1. Core scaffold, plugin registry & token estimator
IDs: T0.1 T0.4 T0.5 T0.8 T23.0 T23.1 T23.2 T23.3 T23.4 T23.5
- Single binary with subcommands `hook mcp proxy stats bench doctor setup(→agents install) run expand plugins` (and many later ones). Each plugin has a `Manifest{id, surfaces, default_on}`. `rtok plugins` prints a table of id, kind, enabled and surfaces.
- Token estimate is `estimate(text, class)`: chars/token Code 3.5, Prose 4.2, Json 3.0, Cjk 1.0, accuracy about ±15%. `[core] estimator_chars_per_token` defaults to 3.5. There is an optional calibration step (see Measure).
- The plugin contract types are Manifest, Measurement{kind, before/after bytes, est_before/after, ref_id}, ToolDef, Injection{plugin, text, priority}, PreToolDecision, PostToolUse, Surface. Event methods default to no-op. A plugin must provide `manifest()` and a dashboard page.
- A plugin receives these host capabilities through Ctx: estimate, record a Measurement, calls/tokens ledger, log, typed `plugin_config<T>(id)`, Archive, Notes, ReadCache, Symbols, Ledger.
- Ten built-in plugins: measure, cmd, read, archive, proxy, inject, guard, memory, graph, toon (later also compress, json_tree, docs, checkpoint). The registry also accepts plugins built outside the repo.
- Every plugin follows three rules: fail open, lossless (the original is recoverable through `expand`), and write a Measurement row for every transformation.

### A2. Hooks (host hook entrypoint)
IDs: T0.6 T2.1 T2.2 T53.3 T87 T93 T98 T118.1 T129 T130.1 T130.2 T130.3 T262.1 T262.3 T262.4 T262.5 T291 T295 T390 T390.1 T459 T461 T645 T10.11 T46.3
- `rtok hook <Event> --host <id>` reads the host JSON from stdin, dispatches to plugins in registry order, writes JSON to stdout and always exits 0 (fail-open). Garbage or empty stdin produces `{}` with exit 0. Stdin is capped at 8 MiB (`core.hook_max_input_bytes`). Over the cap the resident path is bypassed.
- Merge rule: the first `deny` wins, `updatedInput` is last-writer-wins, and `additionalContext` is concatenated under the inject budget. Each dispatch writes one calls row with surface=hook, keeping the host-adapted input.
- Latency budget: p95 < 10 ms over 200 spawns. Process start must avoid heavy frameworks (in Rust, about 1.3 ms of dyld without Security.framework). An optional resident process uses `hook.lock` plus a unix socket and exits when the lock file vanishes.
- I/O fields: session_id, transcript_path, cwd, hook_event_name, tool_name, tool_input, tool_response, prompt, source, trigger, agent_id/agent_type. Output goes in `hookSpecificOutput{hookEventName, permissionDecision, permissionDecisionReason, updatedInput, additionalContext}`. A Claude PostCompact gets `{}`.
- One canonical hook-event table (PreToolUse, PostToolUse, UserPromptSubmit, SessionStart, SessionEnd, PreCompact, PostCompact, SubagentStart, Stop…) holds per-host name maps. Host manifests (`plugins/*/hooks.json`) are generated from it, and a drift test checks manifests against the table. Every installer reads this table.
- Per-host envelope adapters: Claude, Codex (Pre/PostToolUse, SubagentStart), Copilot (camelCase `sessionId/toolName`, flat stdout, `subagentStart` additionalContext), Cursor (`beforeShellExecution`, `postToolUse`/`afterShellExecution`, `beforeSubmitPrompt`, `sessionEnd`), Gemini (`BeforeTool`/…), Devin payload, Grok (Claude-shaped).
- SubagentStart injects a "spawn brief" (shared builder with `handoff`). The brief's pointers carry symbol line ranges. The "already read" state is scoped per context window and per agent_id.

### A3. MCP stdio server & MCP wrapper
IDs: T4.1 T59.4 T70.3 T75 T142 T191 T263 T265 T269 T458 T36.10
- `rtok mcp` is a stdio JSON-RPC MCP server (rmcp). Its tools come from plugins' `mcp_tools()` plus `expand`. Every tool description is ≤60 tokens. One calls row (kind=mcp_call) is written per tools/call.
- Errors: -32700 parse and -32600 invalid request. Blank lines and notifications get no reply. Over-cap requests get a stub. `Content-Length` headers are read as bytes, with a body cap of 64 MiB (over the cap, the result is passed through raw).
- Workspace root comes from MCP `roots`. It is never `/` or `$HOME`.
- `rtok mcp --call <tool> --json <args>` is a one-shot CLI invocation of the same tool code (used by pi/opencode shims). It must shut down any LSP child on every exit path.
- `rtok mcp -- <server cmd>` (formerly `--wrap`) is a proxying wrapper for a third-party MCP server. It supports both NDJSON and Content-Length framing. Malformed frames are forwarded verbatim and the server's exit code is propagated. Only `tools/call` `result.content[].text` is shortened, using cmd rules (`[mcp]` defaults plus per-server/tool overrides). `isError` results are never shortened, and `tools/list` passes byte-identical. Shortened text gets the trailer plus an `expand` id.
- Shared slicing caps: `expand.max_lines` and `mcp.max_result_chars`.
- The server must survive a contended SQLite store at session start (busy timeout).
- An entry the user registered without `type: stdio` is still recognized as rtok's own.

### A4. cmd plugin: shell command output filtering (`rtok run`)
IDs: T3.1 T3.2 T3.3 T3.4 T3.6 T34.8 T36.3 T36.4 T50.1 T50.2 T58.5 T59.1 T64.1 T64.2 T65.2 T65.3 T65.4 T233 T267 T268 T360 T437 T444 T447 T448 T457
- `rtok run [--agent <id>] -- <cmd>` runs through `$SHELL -lc`, merges stdout and stderr, preserves the exit code, stores the raw output under `~/.rtok/archive/<id>` with an archive row, and prints the trailer `[rtok <id> · N lines · expand: rtok expand <id>]`. Capture is capped at 32 MiB total and the pipes are still drained. Non-UTF-8 input is decoded lossily, and multibyte characters are cut safely.
- PreToolUse(Bash) rewrite: `updatedInput.command = "rtok run -- " + orig`. It never wraps `rtok`, `sudo` (case-insensitive), heredocs (`<<`), background `&`, `-i/--interactive`, or the per-stem interactive table (python, node, psql, sqlite3, irb, bash/sh/zsh, docker exec/run). It is disabled by `plugins.cmd.rewrite=false`. Sub-agent commands containing the word `git` are never wrapped. An existing `rtok run --agent <id> -- ` prefix (id 1-64 `[A-Za-z0-9_-]`) is stripped and not doubled.
- Rule engine: `Rule{match, max_lines, head, tail, drop, keep, dedupe, collapse_columns, group}`. Built-in keep patterns are `error|warning|panic|FAIL|Traceback`. A failing command keeps `fail_tail_lines` (default 80). A stack trace is never cut. The trailer counts every dropped line.
- Rule selection uses the command stem after `cd … &&` hops, env prefixes and every pipe stage. It does not match on arbitrary argument words (e.g. `git commit -m "fix grep"`).
- Rule sources merge in this order: built-in `rules/default.toml` < user `plugins.cmd.rules` < sorted `~/.rtok/rules.d/*.toml` (`rules_dir`). There is a TOML schema for them.
- Passes: dedupe of normalized non-adjacent lines (timestamps, pids, ids, durations) shown as `line (×N)`; `collapse_columns` (runs of 2+ spaces folded); `group = "dir"` → `dir/ (N files): a, b, c …`; `group = "diag"` → `E0308 ×N: first message (file:line, …)` for rustc/tsc/eslint/dotnet CS/pytest. Trace grouping is capped at MAX_TRACE_LINES 400.
- Family formatters take argv and the output and either return the shortened text or decline, which falls back to the rule: cargo build/test/check, git, pytest, jest, vitest, go test, docker ps, kubectl get, ps aux. JSON output compaction (gh --json, aws, kubectl -o json, curl) strips nulls.
- Measurement kinds are formatter, rule and raw. Shipped savings are fixture-proven, e.g. tsc 1619→107 B, docker ps 3147→1190 B.

### A5. read plugin: read/search/tree/outline MCP tools + native Read gate
IDs: T4.2 T4.3 T4.4 T4.5 T4.6 T36.5 T36.6 T50.3 T58.1 T59.2 T68.8 T122 T454 T55.13
- MCP `read(path, mode=full|lines|map|signatures|diff|imports…, range)` returns line-numbered output capped at 20K chars (the cap includes the marker). The cut part keeps head and tail plus an archive id. The root guard allows only cwd or `allow_paths`.
- `search(pattern, path, max=50)` respects .gitignore and returns `path:line: snippet` with snippets ≤120 chars. Each file is limited to `search_max_bytes` 1 MiB. `tree(path, depth=2)` shows sizes. The cwd is canonicalized once per call.
- `map`/`signatures` outlines come from tree-sitter tags queries: rust, ts, js, python, dart, c, go, and later java, kotlin, swift, c#, ruby, php. A markdown outline covers headings. An `llms.txt` map follows links.
- Re-reads are deduped by sha256 → `unchanged since <archive_id> (N lines)`, invalidated on PostToolUse Edit|Write. Delta mode (default on) returns a unified diff against the archived previous read when the diff is ≤ `delta_max_ratio` 0.6.
- A ranged read (`mode=lines`) must never be answered with a whole-file "identical" pointer. Dedup is per range.
- Native Read gate: PreToolUse(Read) of a file larger than `read.native_max_bytes` 32K that was not edited in the last 5 turns → deny with "use rtok read(mode=map) first". A small ranged native Read is allowed because it is the host's Edit gate (T127 second heading).
- Copilot passes `path` instead of `file_path`. Both are accepted.

### A6. Archive / expand
IDs: T3.5 T5.3 T5.4 T36.20 T45.3 T55.11 T61.2 T67.1 T67.2 T189 T475
- `rtok expand <id> [--lines a-b] [--grep <regex>] [--context N]` returns archived originals. `--grep` gives numbered hits `N:line`.
- Proxy compress mode: a tool_result older than `keep_turns`=4 and larger than `min_tokens`=1500 is replaced with `[archived <id>: first 8 lines …`. JSON is folded before the archive pointer is placed.
- Decisions are keyed by (session, tool_use_id) and persisted, so a pointer stays byte-stable across requests (prompt-cache safe). Calling `expand` on an id freezes it, and the proxy stops rewriting it.
- MCP `expand(id, lines?)` is available through the proxy flow. Expand rate is tracked.
- Archive rows carry session attribution and the sha256 of the stored bytes. An out-of-range line request is an error.
- When post-tool injection exceeds the budget, the remainder is archived as `dropped:post_tool:<est> · expand: rtok expand <id>`.
- Skill bodies (injected `isMeta` records) are archived outside the live zone.

### A7. inject plugin (budgeted context injection, modes)
IDs: T2.4 T7.1 T188
- `Injection{plugin, text, priority}` entries are sorted by priority and emitted until `inject_budget_tokens` (800) is reached. Oversized candidates are fitted or dropped, and a `dropped:<plugin>:<est>` line names them. Measurement kind=inject is written for emitted and dropped entries.
- Modes are data files `~/.rtok/modes/{terse,yagni}.md` (≤250 tokens each), set with `setup --mode terse,yagni`. They inject at priority 5, once per session. Injection is byte-stable per turn.

### A8. guard plugin
IDs: T2.6 T50.4 T55.8 T55.9 T55.16 T57.1 T62.1 T369 T450
- Denies a duplicate Read/Bash within `plugins.guard.window_turns`=8 when an archive id exists. The reason names `rtok expand <id>`. The deny path reads only archive_size metadata, never the body (the hot path is ≤10 ms).
- Keys: `read\t{path}` and `bash\t{cmd}`. The Bash key keeps the effective (last) `cd` target. A mutating Bash clears all bash and read keys. Edit/Write clear the bash keys plus their own path's read key.
- Flag-aware read-only command classes: ls, cat, head, tail, grep, rg, find, tree, wc, and git status/log/diff/show/branch, taking redirects and pipes into account.
- Optional `deny_grep_glob` (default false): Grep is sent to `search` and Glob to `tree`, but only when read is enabled. A symbol-shaped Grep is answered with the definition from the graph.
- PreToolUse(Skill): an oversized skill body (`skill_max_bytes` 8192, `[plugins.guard] skills=false` by default) is resolved to its SKILL.md (project `.claude/skills` → plugin install path, using the correct marketplace/install entry) and digested.

### A9. memory plugin (notes, recall, observations)
IDs: T6.1 T6.2 T6.3 T66.1 T66.2 T69.1 T69.2 T69.3 T69.4 T69.5 T69.6 T292 T293 T294 T304 T308 T361 T460 T501 T666.1 T667 T669 T671
- MCP `mem_save(kind, title, body, project?)` upserts by project+kind+title with a revision count. `mem_search(query, limit=5)` returns ids, titles and 120-char snippets ranked by bm25 (FTS5). `mem_get(id)` returns the verbatim body. `mem_update(id, retire?, superseded_by?, pinned?)` handles the lifecycle; notes are never deleted. `mem_pack` returns tiers: abstract → first paragraph → body. The project key is the git root name.
- SessionStart recall: the newest titles with estimated body tokens, within `recall_tokens` 200 at priority 10, plus one line naming mem_search/mem_get. With zero notes it prints a line naming the project key. Prompt recall (`prompt_recall`=5) runs per UserPromptSubmit at priority 11 and is skipped when the top-N is the same as on the previous turn.
- `remember: <text>` in a prompt saves kind=user with the title set to the first 80 chars.
- Ranking: pinned first. Use counts and last_used are bumped by mem_get/mem_search hits. Optional decay `half_life_days` (0 = off, giving byte-identical id-desc order) uses score `ln(1+uses)*0.5^(age/N)`.
- CLI: `rtok memory import <file.jsonl>` (`{kind,title,body,ts?,project?}`, deduped by sha256 of the body; a missing file is an error), `memory export` (same JSONL format; round-trips id, ts, retired, superseded_by, pinned; excludes checkpoint:/session:), `memory status [--project] [--since 30d] [--json]`, `memory retire|pin|unpin|revise`, `memory bench`.
- `rtok memory sync [--file CLAUDE.md|AGENTS.md] [--budget N] [--dry-run] [--remove] [--force]` writes a block between `<!-- rtok:memory -->` markers. A sha256 guard against hand edits exits 1. Hooks never write this block.
- Observations: title, type, narrative ≤400, files. Duplicates within 5 s are dropped and redelivered events are not duplicated. Recall uses RRF k=60, ≤3 per session, ≤2 lines of the form `obs <id> <title> (<session>)`.
- Experimental, off by default: an opt-in extractor (an external program), an admission gate (hash/trigram), and hybrid ranking/forgetting.

### A10. Checkpoint / handoff / compaction survival
IDs: T2.5 T58.2 T59.6 T62.2 T71.2 T306 T375
- PreCompact reads the transcript and stores the last 20 prompts (≤300 chars each), touched paths with per-file actions, last error lines, loaded skills, and live archive ids (newest first, capped) as notes kind=checkpoint. SessionStart(source=compact) and PostCompact inject it within `checkpoint_tokens` 400.
- SessionEnd writes `session:<id>`. SessionStart(source=startup) restores the newest one. The hook stays ≤10 ms and fails open.
- MCP `handoff(budget_tokens)` returns a digest of note titles, live archive ids with tool and bytes, touched paths, and the last N prompts. Its description is ≤40 tokens. Ids are ordered by decision time and listed once each.
- Compaction events are mapped per host (Codex, Cursor, Gemini, Copilot). Post-event injection bytes are equal to Claude's.

### A11. graph plugin: code index, symbol/callers/impact/explore
IDs: T8.1 T8.2 T8.3 T8.4 T8.5 T8.6 T8.7 T8.13 T8.15 T8.16 T8.17 T21.2 T30.0 T30.1 T30.2 T35.1 T35.2 T35.3 T35.4 T35.5 T36.16 T36.17 T39.0 T41.1 T52.1 T52.2 T52.3 T52.4 T52.5 T59.3 T68.1 T68.2 T68.3 T68.4 T68.5 T68.6 T68.7 T68.9 T68.10 T181 T327 T368 T371 T376 T377 T387 T474 T620 T621 T646 T654 T663 T664 T665
- SQLite `symbols(root, path, name, kind, line, end_line, start_byte, end_byte, is_def, scope, file_sha, mtime, size, content_hash)` is built from tree-sitter tags (definitions and references, including type-position and scoped-call refs) while walking the repo with .gitignore respected. It is scoped per canonical root. Freshness is stat-gated (mtime+size; sha256 only when the stat differs). An unreadable file gets stat (0,0). A removed directory purges its rows. An extractor fingerprint (queries, grammar versions, INDEX_VERSION) triggers a rebuild. Storage is SQLite only (the LadybugDB experiment was removed).
- MCP tools: `symbol(name)` returns definitions plus source bodies (`body_lines` 40, whole definitions first), callees, and "+N other definitions of `<name>`". `callers(name)` returns reference sites grouped by file. `outline(path)`. `impact(name|path, depth=2 max 4, to?)` is a BFS over scope edges (one recursive CTE ordered by depth, path, scope), grouped as `path (N refs, depth d)` within `impact_tokens` 1500, with call paths A→B when `to` is given. `explore(query)` does identifier tokenization (first 8), exact then prefix best-5, `= name` + bodies, `paths:` caller chains ≤3 hops, and `impact:` lines. Descriptions are ≤25-60 tokens.
- Every answer is capped and gets an archive id. A staleness line `index <old> head <new>` is added when HEAD has moved. Ambiguous refs are marked. Ranking order is imported > same dir > IDF. Co-change data comes from `git log -n300`: `changes with: a (7)`, top 5.
- `rtok graph affected [--since <ref>|--staged] [--json]` maps changed files to tests via impact BFS depth 3 and is_test_path. It prints `file ← via symbol` plus a per-language test command, or `{"tests":[{file,symbol,command}]}`. When nothing matches it prints "no indexed test reaches the change; run the suite". There is also `graph review` (risk scores 0.30/0.05/+0.20/≤0.10), a dead-code report (skipping macros, main and tests), and a ranked repo map at SessionStart.
- Indexing: `plugins.graph.auto_index=true`, `watch = off|notify|watchman` (watcher indexes only changed paths; full walk on overflow). Cold index uses batched transactions (about 27 s for 3000 files cold, 0.05 s warm). Parsing runs on worker threads with a single writer, and queries are compiled once. A progress bar is shown on TTY only (hidden for MCP and the watcher).
- `[plugins.graph] exclude/include/extensions` map plus a query language (path scope and kind filters).
- Optional LSP backend (`backend="lsp"`, rust-analyzer, `dart language-server` for a pubspec.yaml root) uses the same tool names and falls back to tags per call. Reader/writer threads have deadlines and didChange versions are tracked. The LSP child is shut down at EOF.
- Recall gate: labelled truth recall ≥0.92. There is a with/without bench (`RTOK_BENCH_LIVE`).

### A12. Graph projects, links & graph diff
IDs: T329.1 T329.2 T329.3 T329.4 T329.4.1 T329.4.2 T329.5 T329.6 T329.7 T329.8 T329.18 T329.29 T329.42
- `projects` table (id, canonical root, name, origin manual|session|worktree|mcp|reference). `rtok graph projects [--json]` shows selected, id, name, origin, state (ok/stale/not indexed/missing), rows, files, pending and root. Subcommands: add, remove, select. `auto_add_projects=true` adds projects on session start, worktree add/adopt and MCP calls.
- Links (from, to, kind manual|auto, reason, unlinked) with cycle-safe scope and no self links, managed by `link`/`unlink`. Reference discovery covers Cargo path/[patch]/workspace, npm file:/link:/workspace:, Go replace/go.work, Python path deps and .gitmodules, with `reference_depth`=3 and `max_auto_projects`=20. Unlinks are remembered.
- A `project` argument on symbol/callers/impact/explore/outline and a CLI `--project` flag. The default is the cwd project plus its links. Dead-code and affected reports are scoped the same way.
- `rtok graph diff [--from REF|PROJECT:REF ...] [--to REF|working] [--project ID] [--from-export FILE] [--json]` plus MCP `graph_diff`. The old side comes from `git ls-tree` + `cat-file --batch`. Link changes and "changed, not analysed" are reported. Text export is ≤16 MiB.

### A13. API proxy (Anthropic / OpenAI / Gemini wires)
IDs: T5.1 T5.2 T5.5 T11.1 T11.2 T11.3 T11.4 T11.6 T31.0 T31.1 T31.2 T34.6 T36.18 T45.2 T51.1 T51.2 T51.3 T51.4 T55.14 T55.15 T59.5 T259 T326 T686
- `rtok proxy [--port 8790] [--upstream] [--mode passthrough|compress]` listens on 127.0.0.1:8790 and exposes `/health`. It forwards `/v1/messages` and everything else to `RTOK_UPSTREAM` (default https://api.anthropic.com). SSE passes through unchanged with a bounded tee for usage parsing. One path→provider/api mapping is used, with a real URL join.
- Wire abstraction (matches, tool_results, usage_from_body, usage_from_sse) for Anthropic Messages, OpenAI `/v1/chat/completions` (`RTOK_OPENAI_UPSTREAM`), `/v1/responses` (function_call_output; final `response.completed`) and Gemini `:generateContent`/`:streamGenerateContent` (functionResponse). Batch and Flex lanes (`[proxy.batch]`, `[proxy.lanes.<lane>]`) are supported.
- Usage rows: one per proxied request, including cache hits and errors. `usage.api` is anthropic|openai_chat|openai_responses|gemini. Counters are disjoint (output_extra such as thoughtsTokenCount; input_includes_cache).
- Optional semantic cache (opt-in with a threshold). The key is built from block text, tool_use id+name, tool_result id+content, and image/document sha256.
- `[proxy] context_management=false` (env `RTOK_PROXY_CONTEXT_MANAGEMENT`) adds the Anthropic `clear_tool_uses_20250919` edit.
- `[proxy.tools_rewrite] enabled=false, max_description_tokens=60, allow/deny` truncates tool descriptions at a sentence boundary and leaves schemas untouched. `compact_schema` drops duplicate defaults/examples and compacts enums. The original `tools[]` is archived content-addressed (about 32% cut on the fixture).
- `live_blobs` (default false) shrinks JSON and code text blocks inside the live zone. It never rewrites image/document data, which would produce an invalid 400 request.
- `rtok wrap -- <cmd>` makes sure the proxy is up (starting it in-process if needed) and sets `ANTHROPIC_BASE_URL`/`OPENAI_BASE_URL` for the child.
- `rtok stats --cache`: cache_read vs cache_creation per turn. It detects cache busts (creation >20K while read drops) and attributes them to tools or system prompt changes.

### A14. toon / json_tree / compress (proxy-side content rewriters)
IDs: T11.7 T11.8 T36.1 T36.2 T28.0 T28.1 T28.2 T127 T655 T657
- toon: renders JSON arrays of objects in tool results as TOON tables. It escapes `\n \r \t` in cells and archives the original string first. It respects archive `keep_turns` and the live-zone decision, so expand sticks. It parses tables out of MCP `[{type:text}]` text.
- toon and compress are on by default but act only in `proxy.mode="compress"` (T127; T127's second heading is the read edit-gate advice, see READ).
- compress: optional LLM compression, off by default and byte-identical when off. Admission uses a CAS queue. Auth is sent only when the wire provider is Anthropic.

### A15. Store (SQLite) & retention
IDs: T0.3 T13.2 T13.3 T13.4 T34.1 T36.11 T104
- SQLite in WAL mode with FTS5 and `foreign_keys=ON`. Tables: events, measurements(id, ts, session, plugin, kind, before_bytes, after_bytes, est_before, est_after, ref_id), archive, notes + notes_fts, hosts, providers, models, sessions, calls, tokens, logs, call_io, usage, symbols, projects, observations, otel_export, graph_events, task_claims… Migrations are numbered SQL files, with a migration list and schema drift guard test.
- Write API: upsert_session, upsert_model, insert_call, insert_call_io (stored inline up to `call_io_inline_bytes` 65536, otherwise archived; hooks never archive), insert_tokens, insert_log, purge_calls_older_than.
- Retention: `retain_calls_days` 30 purges calls, call_io, tokens, logs, usage and archive atomically. busy_timeout handles concurrent writers. LIKE prefixes are matched literally (escaped).

### A16. Measure / stats / bench
IDs: T1.1 T1.2 T1.3 T1.5 T9.1 T49.1 T49.2 T53.1 T55.7 T58.3 T59.8 T61.1 T65.1 T71.4 T125 T128 T219 T305
- Parses Claude JSONL transcripts (`~/.claude/projects/**/*.jsonl`, including `<session>/subagents/agent-*.jsonl` + meta.json) and Codex, OpenCode and Cursor session logs.
- `rtok stats` reports per-tool count/total/mean/p95 (nearest-rank, 2048-sample deterministic reservoir)/max, Bash by family (stripping cd and env; quoted paths handled; an unterminated quote fails open), MCP groups, usage totals, cache hit rate, median final context, context-token-turns, Edit old/new bytes share, a repeat column (sha256 of results), injected skill bodies per skill, thinking-block share, per-skill listing overhead, and archive replay (estimate). Flags: `--plugin`, `--cache`, `--price` (`[stats.prices."<model>"]` USD/MTok input/cache_write/cache_read/output).
- `--save-baseline <name>` / `--compare <name>`. `--calibrate` fits chars/token per class using count_tokens.
- `rtok bench`: runs `claude -p "<task>" --output-format json --settings A|B` × n (default 3) and prints mean tokens and cost per config. A coaching-nudge A/B is part of this.
- `report` token-sink ranking: top ten paths and commands.

### A17. Report (md / html / pdf / --ai)
IDs: T22.0 T22.1 T22.2 T22.3 T22.4 T22.5 T22.6 T34.4 T36.13
- `rtok report [--format md|html|pdf] [--out <path>] [--since <window>] [--ai]` uses the `[report] format/out/since/budget_tokens` config. Sections cover the window, cache, expand, config, doctor and recommendations, each with evidence row counts.
- HTML is self-contained (inline CSS and SVG bars, stable ids, no network). PDF is A4, Helvetica only, with a ToC, page numbers, bookmarks and native bars, all UTF-8 safe. Bars share one scale.
- `--ai`: toon tables, `#id` anchors, a units line, a budget of 8000 tokens (first-fit whole sections, `dropped:` names), and a task list `1. [rule] finding (evidence)`.
- Advice rules: expand rate > 0.05, net saving ≤0, cache busts, hooks with ≥10 calls and no Measurement, inject over budget, archive keep_turns vs re-reads, idle hooks. Ordered by recoverable tokens. With nothing to say it prints "No recommendations.".

### A18. Doctor
IDs: T1.4 T7.2 T42 T59.7 T61.3 T123 T331.1 T331.2 T331.3 T331.4 T331.5 T331.6 T331.7 T331.8 T331.9 T331.11 T331.12 T392
- `rtok doctor [--agent <host>] [--instructions] [--json]` lists hooks by event and tool, MCP servers with tool counts and estimated description tokens (from `~/.claude.json` and `.mcp.json`), the ANTHROPIC_BASE_URL chain, and module status per host (✓ installed, ✗ not, − unsupported). It advises `[proxy.tools_rewrite]` when description tokens ≥ `doctor.tools_rewrite_min_desc_tokens` and the chain contains rtok's proxy.
- Instruction audit: token counts for CLAUDE.md files and plugin SessionStart text, with duplicates flagged. Skills audit: one row per skill across all roots, plus duplicate skills and repeat loads per compaction window. Overlaps section: host-native features that duplicate rtok (Claude auto-memory, OpenCode…).
- Broken hooks: Claude settings.json and settings.local.json (user and project), other hosts' JSON/TOML hook files (Kimi `[[hooks]]`, CodeWhale, Codex config.toml), enabled Claude plugins' hooks. Commands are split POSIX-style with ~/$HOME expansion. On Windows, PATHEXT and cmd/PowerShell rules apply.
- Duplicates: duplicate hooks, and duplicate MCP entries normalized by PATH/symlink, `npx pkg@x`, URL case/trailing slash and env, plus MCP servers from Claude plugins. Each comes with a keep recommendation.
- `doctor --fix [--yes] [--only broken-hooks|…] [--dry-run]` applies minimal JSONC/TOML edits. It never removes the last copy, a plugin's own files or a managed file. An interactive checklist (project files pre-unselected, change the kept copy, per-file diff, confirm) is shared with the web "Fix selected" page and the TUI.

### A19. Agents install / hosts (install, uninstall, list, outdated)
IDs: T2.3 T4.7 T9.3 T10.1 T10.3 T10.5 T10.7 T10.8 T10.9 T10.10 T11.5 T21.1 T27.0 T34.5 T36.19 T37.0 T44.1 T44.2 T44.3 T44.4 T44.5 T46.1 T46.2 T46.4 T47.1 T48.3 T48.5 T48.6 T48.7 T48.8 T54.1 T71.3 T72 T76 T77 T78 T81 T85 T89 T90 T91.1 T91.2 T97 T99 T100 T112 T114 T115 T116 T121 T138 T139 T140 T141 T277.1 T279.1 T380 T382 T434 T463 T560 T560.1 T560.2 T652
- `rtok agents install|uninstall|list|info|outdated <host>[,<host>…] [--cli|--desktop] [--proxy] [--mode …] [--dry-run] [--yes]` (aliases `agent`, `setup`, `remove`, `--gui`). It writes hooks, the MCP registration (`mcpServers.rtok` / per-host shape), the proxy base URL and the host plugin link. Uninstall removes all of these while keeping foreign entries, and clears base URLs only if they still hold rtok's URL. `--replace` removes legacy hook stacks.
- Hosts covered: Claude Code (+Desktop `claude_desktop_config.json`), Cursor, Codex (`~/.codex/config.toml`, model_provider base_url), OpenCode (`mcp.rtok {type local, command [rtok,mcp]}`), pi, omp, Copilot CLI + app, Gemini, Kimi, ZCode, Kilo, Devin, CodeBuddy/WorkBuddy, Grok, Antigravity, Windsurf, Zed (`context_servers`, comment-aware edit), VS Code (`servers.rtok`), Aider (`openai-api-base` line edit), Command Code.
- Host plugin trees shipped from the repo: plugins/claude (`.claude-plugin/plugin.json`, hooks.json with 9 entries running `${CLAUDE_PLUGIN_ROOT}/scripts/hook.sh <Event>`, .mcp.json, skills), codex, cursor, kimi, grok, zcode, antigravity, kilo. Claude and Codex install idempotently from the GitHub marketplace (`claude plugin marketplace add` + `install rtok@rtok`; `.agents/plugins/marketplace.json`).
- Safety: writes are atomic and preserve symlinks (writing through to the target). A backup `.bak-<ts>[-n]` is made once per distinct content and never overwritten. A host config that cannot be read is refused rather than replaced. JSONC is kept byte-identical outside rtok's keys. The `Apply{dry_run, backup, yes}` contract prints NO_CHANGES on an empty diff and makes no rewrite.
- `agents list` shows host, app type cli/gui, version (`<bin> --version`, 3 s timeout), installed state, modules ✓/✗/−, and plugin version + source. `agents outdated` exits with `--exit-code 10` and compares semver ignoring build metadata. A warning is printed when `rtok` is not on PATH (on Windows the absolute exe is written). rtok binary detection handles rtok.exe and the self path.
- A desktop host app that is running is closed (waiting up to 30 s) before its config is written and reopened afterwards. Failures produce a "restart manually" warning. Interactive prompts (Confirm; default No; Esc → No; skipped without a TTY) must not erase typed input.
- Hub skill `skills/rtok/SKILL.md` (description ≤120 chars, body ≤2 KB) is installed into host skill roots with the `rtok-` prefix. Uninstall removes only rtok's directories.
- A support-matrix table for docs/agents.md is generated from host descriptors.

### A20. In-host plugins (pi / opencode / omp / cursor extensions)
IDs: T10.2 T10.6 T48.1 T48.2 T62.3 T70.1 T70.2 T70.4 T70.5 T70.6 T70.7 T92 T92.1 T197 T214
- `rtok filter --stdin [--cmd "<tool> <arg>"]` compresses text without executing anything. opencode `tool.execute.after` and pi `tool_result` use it, failing open.
- pi extension (`package.json pi.extensions`): `tool_call` bash → `rtok run -- …`. The context hook archives the live zone without the proxy. `registerTool` read/search/tree/symbol/callers/expand/mem_* go through `rtok mcp --call`. A `rtok-missing` hint is shown once per session.
- `rtok guard check --tool <name> --json <input>` returns an allow/deny verdict for pi/opencode. Compaction hooks go through `rtok hook PreCompact --host <host>`, appending and never replacing.
- Cursor `postToolUse` → `updated_mcp_tool_output` shortens MCP results the host launched.
- Spawns time out after 5000 ms with an abort signal, keeping the original on failure. Launcher scripts propagate exit codes.

### A21. Worktrees
IDs: T150 T151 T153 T524 T656
- A git porcelain parser (`git worktree list --porcelain -z`) produces records (path, head, branch, detached, bare, locked, prunable). Owner attribution and classification follow.
- `rtok worktree list [--json]` shows source size, cache size (build dirs) and orphans (a `.git` file pointing at a missing admin entry).
- `rtok worktree gc [--yes] [--owner] [--idle 30m] [--stale-lock] [--json]` uses a pure `decide()` returning Remove/DropRecord/Keep(reason). Merged detection is squash-aware (`git merge-tree`). It drops stale records left by `rm -rf` and keeps a worktree when its age is unknown ("age unknown: …"). It is a dry run by default. `worktree clean --idle` and `worktree remove --dry-run` also exist. Durations are parsed humantime-style (`30m`, `0s`).
- `worktree add` copies symbol rows into the new worktree's index.

### A22. Tasks
IDs: T442
- Tasks support claim/release/blocks/priority 0-4 and a ready queue, with disk/GitHub/GitLab backends. A claim lock `.claim.lock` is created with create_new and goes stale after 10 s. Labels: `rtok:owner:<agent>`, `rtok:needs:<id>`, `rtok:p:<n>`. Claims are stored in a `task_claims` table.

### A23. docs plugin
IDs: T455
- MCP `docs_resolve/docs_query/docs_get` over dependency docs (Cargo.lock-driven) with an FTS table, plus `rtok docs fetch`. Off by default.

### A24. Web dashboard (`rtok web`)
IDs: T19.1 T19.2 T19.4 T21.3 T36.14 T36.15 T60.3 T60.4 T60.5 T60.6 T60.9 T63.1 T193 T310 T310.1 T310.2 T310.3 T310.4 T310.5 T310.6 T310.7 T310.8 T310.9 T310.10 T310.11 T407 T414.1 T414.2 T414.3 T414.4 T414.5 T414.6 T414.8 T414.9 T414.10 T414.11 T414.12 T414.13 T414.14 T414.15 T414.16 T414.17 T483 T484 T623.1 T623.2 T641 T329.12 T329.13 T329.14 T329.15 T329.20 T329.21 T329.22 T329.23 T329.26 T329.27 T329.28 T329.32 T329.33 T329.35 T329.36 T329.43 T329.50 T329.51 T329.52 T329.53
- `rtok web [--port N|0]` uses `[web] host 127.0.0.1 port 3333` (`dashboard` is a hidden alias with a stderr notice, and the `[dashboard]` config section is folded into `[web]`). It prints `rtok web http://<addr>` to stderr and serves `/health`, `/ws` and the embedded SPA (hashed, precompressed, SPA fallback, CSP; `RTOK_WEB_DIST` overrides it).
- Security: an Origin check returns 403 on mismatch, and the Host header must be an IP or localhost. Paths never come from ws messages.
- The `/ws` contract is JSON Schema generated from server types into generated TS (`ws.schema.json`, `snapshot.gen.ts`). Request ids are echoed. The server owns totals and sends all four windows per frame. Messages: snapshot, plugin toggle, expand, doctor plan/apply, graph drill-down `{"graph":{project,expand,focus,depth,limit,query}}`, calls frames, link/unlink.
- SPA: React 19, TanStack Router/Query/Table/Virtual, ECharts, Three.js (3D projects overview with a 2D fallback), Storybook. Pages: overview, plugins (toggle), calls (expand), sessions (drill-down), doctor (Fix selected), logs, skills, stats, graph, hosts, config, services, worktrees, memory.
- UX contracts: ⌘K palette and `g o` shortcuts, filters in URL search params, clickable KPIs, CSV/JSON export with formula-injection escaping (`= + - @`), Δtok savings trend, live status age plus pause, theme toggle (`rtok-theme` in localStorage), loading/offline/Reconnect states, passthrough rows shown in bytes rather than tokens.
- Live graph calls panel: windows of 1/5/15 min, per-tool bars, backend shares, sparklines with SPARK_SLOTS 30, symbols_returned/files_touched/projects_hit tallies, compare mode colours, a `vscode://file/<root>/<path>:<line>` link, a narrow strip below 900px, and reduced-motion support. Graph events are stored in an append-only `graph_events` table written by every process.
- .NET note: the React SPA is not reused; the pages and UX contracts above are rebuilt in Blazor WebAssembly (N13) on Radzen.Blazor components and charts, with ECharts (graph view only) and Three.js through JS interop and no Storybook. The server side becomes ASP.NET Core Kestrel minimal API + WebSockets (AOT-friendly). The `/ws` types are shared C# contracts, so `ws.schema.json` and `snapshot.gen.ts` go away.

### A25. TUI (`rtok tui`) and the shared operator model
IDs: T15.0 T15.1 T15.2 T15.3 T15.4 T15.5 T15.6 T15.7 T15.8 T15.9 T15.10 T15.11 T15.12 T60.8 T60.10 T113 T478 T480 T487 T500
- One operator model (plain data snapshot: Overview, Plugins, Calls, Sessions, Doctor, Logs, Memory, Graph) feeds both web and tui. The TUI never opens the store and reads only the snapshot, which a background worker produces (with a collapsing queue). Parity tests map commands to pages (COMMAND_PAGES/EXEMPT) and web to tui.
- Keys: q/Esc/Ctrl-C quit and restore the terminal. Left/Right wrap. 1..9 jump to a tab. `?` opens a help overlay generated from a single KEYS table. `r` refreshes. Sessions has a ↑/↓ cursor and `l` for live-only. Calls has CALLS_ROWS=120 and Enter/z for detail. The footer reads `q quit · Left/Right or 1..N switch tabs · updated HH:MM:SS UTC`.
- Config: `[tui] tab, tick_secs=2 (≥1)`, env `RTOK_TUI_TAB/RTOK_TUI_TICK_SECS`. On a non-TTY it prints one stderr line and exits 1.
- The Plugins tab toggles through the config writer (toml_edit). The Doctor tab renders the text report verbatim and supports plan + confirm fix. The Logs tab is bound by `[log] lines`. The Overview tab shows CTT bars and a sparkline. A live graph calls pane shows `symbols returned N of M (K across projects)`.

### A26. Sessions view
IDs: T25.0 T25.1 T25.2 T25.3
- A session row (host from `--host`, project = git root basename, cwd) is written at SessionStart. Session totals for a window come from one query (a CTE).
- `rtok agent sessions [--all]` prints a table of agent, provider, model, in/out/cache, started and duration, newest first. Durations look like `45s` … `3d04h` (2 units). With no sessions it prints "nothing is running". `sessions watch` repaints in place on a TTY (cursor-up + clear, no raw mode). When piped it prints repeated plain tables, and a BrokenPipe is fine.

### A27. Logging
IDs: T24.0 T24.1 T24.2 T24.3 T24.4 T24.5 T225 T225.1 T270
- `[log] path ~/.rtok/logs/rtok.log, max_bytes 1048576, files 5, lines 200, level info, to_db true`. Rotation shifts `.N` files. Lines below the level are dropped before any IO, and a civil date is stamped. An `errors.log` sibling copy keeps error lines. A single funnel writes the file line and the logs row, failing open. Legacy `[core] log_*` keys are folded into `[log]` with a warning.
- `rtok logs [--lines N]` prints newest first, numbered 1=newest across rotated files ("no logs yet" when empty). `logs export` omits numbers and colour. `logs watch` repaints on a TTY and appends when piped, and detects rotation by content. A `tspin` pretty mode is optional. `RUST_LOG`-style stderr debug logging (→ `RTOK_LOG`).
- The demon pipes child stdout (info) and stderr (warn) into the sink with a single writer.

### A28. Demon (service supervisor) & info
IDs: T20.1 T40 T43 T73 T80
- `rtok demon start|stop|restart|status|kill [<service>]` runs one supervisor per service (hidden `demon supervise <name>`, setsid, `[demon] services`, backoff, state files). The status endpoint reports bind:port (proxy defaults to 127.0.0.1:8790). There is no list/update.
- Hidden `rtok demon upgrade` snapshots live services, stops them, runs the updater (`ketch upgrade rtok --yes` / `rtok-update` / `RTOK_UPDATE_CMD`) and restarts the same set even if the update failed.
- `rtok info` prints version, config, store, proxy and disk usage.

### A29. OpenTelemetry export
IDs: T16.1 T16.2 T16.3 T16.4 T16.5 T16.6 T16.7 T16.9 T36.12 T45.1 T53.4 T143 T592 T653
- `[otel] endpoint="" headers="" service_name="rtok" content=true content_bytes=65536 flush_secs=5`. Headers are secrets and are never printed.
- An OTLP/HTTP JSON encoder (Resource, Span, LogRecord, Sum) POSTs to `<endpoint>/v1/traces`, `/v1/logs` and `/v1/metrics` with ≤1000 rows per stream after a watermark (`otel_export(stream, mark)`, identity-based, with a sessions end_seq cursor advanced only after success). Each stream fails independently. An unreachable collector marks nothing.
- GenAI semconv mapping: a session span `invoke_agent {host}` with gen_ai.operation.name, gen_ai.agent.name, gen_ai.conversation.id, rtok.project/cwd/source, plus call spans. Metrics are cumulative monotonic sums (temporality=2) of `rtok.tokens{gen_ai.token.type=input|output|cache_read|cache_write}`. Semantic-cache hits are excluded from billed totals.
- Triggers: a proxy timer every flush_secs, an MCP thread plus a final flush at EOF, and hooks Stop/SessionEnd spawning a detached `rtok otel flush` outside the hook path. Flushes coalesce, with at most one running and one queued (flock `<db>.otel-flush.lock` + `.otel-flush-queued.lock`). Concurrent flushes never double-export. CLI: `rtok otel flush|status`.

### A30. Config
IDs: T0.2 T12.1 T12.2 T12.3 T12.4 T12.5 T12.6 T34.7 T36.7 T36.8 T36.9 T45.4 T363 T364 T365 T379
- `~/.rtok/config.toml` (`RTOK_HOME`). The typed schema denies unknown fields, with sections core, hook, mcp, proxy, stats, bench, doctor, setup, expand, filter, log, otel, web, tui, demon, report and `plugins.<id>`. `config/default.toml` is the reference.
- Layering: defaults → user file (`--config`/`RTOK_CONFIG`) → `<git root>/.rtok.toml` → `.env` (RTOK_* lines only, nearest .env walking up, then `~/.env`) → `RTOK_*` env → flags. Legacy keys are folded before env and flags. `~` is expanded in every path key.
- `config show [--sources]`, `config get/set` (toml_edit, comments preserved), `config validate [path]`. Unknown key, type or range errors report file:line and exit 1. Floats, enums (`embed_backend`), `stats.since` and `report.since` are range-checked, and env overrides are validated. Elsewhere invalid config gives a warning and falls back to defaults.
- Every write command supports `--dry-run`, which prints a coloured git-style unified diff. A flag↔key coverage test exists.
- Session id resolution: stdin id → `$<core.session_env>` → `unknown`.
- Secrets (otel.headers) are redacted in show/get/set/report.

### A31. CLI UX: completions, man, progress, colour, previews, prompts
IDs: T53.2 T60.1 T60.2 T276 T276.3 T276.6 T316 T317 T318 T319 T408 T409 T416.1 T416.2 T416.3 T416.4 T651 T20.2
- `--json` on every reading command (doctor, plugins, agents list, sessions, logs, demon status, otel status…). Golden CLI tests (trycmd) cover every subcommand.
- `rtok completions bash|zsh|fish|powershell|clink(lua) [--install|--uninstall]` installs to standard locations. `rtok man [--dir]` writes one roff page per subcommand (`rtok-<sub>.1`). Release artifacts ship `share/man/man1` and `share/completions/*`. An interactive picker (multi-select, `--list`) errors on a non-TTY.
- Progress: spinner and bars only on a TTY, never for pipes, --json, MCP or hooks. Labels look like `running <bin> <sub>…` and `probing MCP servers…`. Subprocesses time out and are killed. No prompt is shown under a spinner.
- Colour honours NO_COLOR, CLICOLOR, CLICOLOR_FORCE and TERM=dumb per stream.
- Previews: shared unified diff, file diff and human-bytes output. Destructive commands (worktree clean/gc/remove, agents junk clear, graph projects remove/unlink, completions uninstall) default to a dry run. `--dry-run` conflicts with `--yes`. `--stat` prints `- path  size  N files` plus totals.
- User-visible output never contains plan ids.

### A32. Windows portability
IDs: T55.1 T55.12 T83.1 T429 T430 T504
- Path display and read paths are case-insensitive, and `never_wrap` is case-insensitive (`Sudo.exe`). `file://` URIs are percent-encoded. Path identity is computed without the `\\?\` verbatim prefix.
- `wrap_quote` is PowerShell-safe. Under a POSIX host shell (Git Bash), nothing containing `'` is wrapped.
- Binaries are detected by PATHEXT, with a `cmd /C` version probe for `.cmd` shims. Log rotation works while the file is open. The hook stack is 8 MiB.

### A33. Experimental / feature-flagged extension points (default off)
IDs: T29.0 T29.1 T29.2 T32.0 T32.1 T32.2 T33.0 T33.1 T33.2
- Embeddings beside FTS5 (`embed_backend`, FTS5 default) for memory and graph search.
- WASM plugin host (out-of-tree plugins, with an example Measurement plugin). For .NET, consider dropping it or replacing it with a managed plugin ABI.
- Tiered L0/L1/L2 context loading for archive/inject, behind a flag.

---

### A-drop1. Rust-only, drop (cargo/crates/CI/tests/build infra, superseded Rust tech)
IDs: T0.7 T5.0 T8.8 T8.9 T8.10 T8.11 T8.12 T8.14 T8.18 T8.19 T10.4 T13.1 T17.1 T17.2 T18.1 T18.2 T18.3 T18.4 T18.5 T18.6 T18.7 T19.3 T23.6 T26.0 T26.1 T34.2 T34.3 T34.9 T37.1 T38.1 T38.2 T38.3 T38.4 T38.5 T45.5 T45.6 T47.2 T47.3 T47.4 T47.5 T55.10 T56.1 T56.2 T56.3 T56.4 T56.5 T60.7 T72.1 T74 T82 T84 T103 T106 T108 T109 T110 T111 T119 T120 T161 T163 T163.2 T163.3 T163.4 T163.8 T163.9 T222 T251 T252 T253 T254 T255 T280 T310.12 T312 T415 T427 T462 T467 T468 T469 T502 T503 T642 T647 T661 T662
Notes to keep from these: the version string format `rtok X (<sha9>)` and a self-updater (`rtok-update`); release targets macOS arm64/x64 + Linux x64; graph truth tests (recall ≥0.92) and MCP byte-exact contract tests should be re-created as .NET tests; an in-memory VFS abstraction for FS tests. Slint/WASM UI and LadybugDB were removed upstream.

### A-drop2. Process / audit / research / docs-only, drop
IDs: T9.2 T9.4 T9.5 T14.0 T14.1 T14.2 T14.3 T14.4 T14.5 T14.6 T14.7 T14.8 T14.9 T14.10 T16.8 T38.6 T41.2 T44.6 T48.4 T64.3 T126 T217 T262.2 T264 T359 T404 T530 T540 T547 T554 T561 T568 T572 T575 T578 T582 T585 T588 T591 T593 T596 T598 T600 T602 T603 T605 T607 T610 T612 T614 T616

## B. Second half of rtok's done log

Source: /Users/listepo/GitHub/pyrlyn/rtok/done.md from line 7796 to the end. There are 480 unique ids; one id appears twice as a heading in the source (config, area 19). Each id is listed exactly once, either on an area's `T-ids:` line or in the drop list at the end. The bullets are the contracts the C# port must reproduce.

### B1. Hook runtime, latency and fail-open
T-ids: T101, T170, T178, T200, T201, T204, T237, T245, T309, T83.15
- Fail-open matrix: 5 hosts (claude, cursor, copilot, devin, grok) × 8 events (SessionStart, UserPromptSubmit, PreToolUse, PostToolUse, PreCompact, PostCompact, SessionEnd, Stop) × stdin {empty, garbage, truncated, non-UTF-8, 1 MiB}. Every case exits 0 with the host's no-op reply and never rewrites anything.
- Hook budget: the original Check was p50 ≤10 ms in-process; it was later raised to p50 <20 ms as the host sees it (measured 12.3/12.5 ms). A slow hook (over `[hook] max_ms`, default 10) logs a warn line; `max_ms=0` turns that check off.
- A resident hook process is used: the std-only `rtok-hook` client talks to `rtok hook --serve`, one process per home. The resident refuses a client with another version or config fingerprint. The client waits 50 ms for an answer, then falls back to `rtok hook` and autostarts the resident detached. hooks.json order is `rtok-hook` → `rtok` → `hook.sh`, and hooks exec `rtok` directly with no extra `/bin/sh` launcher.
- Store lock policy: LOCK_WAIT is 5 ms busy, one attempt. A locked store makes the hook return `{}` at once. A compile-time assert keeps lock wait plus migrate ≤5 ms. When the lock blocks SessionEnd, stdin is handed to a detached retry child so `ended_at` is still written.
- Stdin is read through a cap, `core.hook_max_input_bytes` (8 MiB). Over the cap the hook fails open to `{}` with one stderr line, before any JSON parse. Over-cap bodies are not hashed when no archive dir exists.
- Each plugin call is isolated (try/catch per plugin). An exception logs `error plugin <id> "<event> panicked: …"` and drops only that plugin's output.
- A tool call is processed once: `once_key = <event>:<tool_use_id>`, with `measurements.once_key` unique.

### B2. Host plugin packages and the hook-command resolver
T-ids: T88, T95, T118.3, T132, T174, T195, T248, T250.1, T250.2, T250.3, T250.4, T349, T357, T622, T635
- Resolver used by every shipped hook command (POSIX): `command -v rtok >/dev/null 2>&1 && exec rtok hook <event> [--host X]; [ -x "$HOME/.ketch/bin/rtok" ] && exec "$HOME/.ketch/bin/rtok" hook <event>; exit 0`. The line has no brace group, so fish still parses it, except on Cursor (see below). Only the host's session-start event prints a missing-rtok note naming `ketch install pyrlyn/rtok`, in that host's shape: Claude `hookSpecificOutput.additionalContext`, Copilot `{"additionalContext"}`, Cursor `{"additional_context"}`. Grok gets no note on any event.
- Host-specific forms:
  - Copilot ships a PowerShell twin: `Get-Command rtok -CommandType Application`, then `%USERPROFILE%\.ketch\bin\rtok.exe`.
  - Cursor wraps the line in `{ …; }` because it appends a heredoc. The Windows copy rewrites it to the bare line.
  - Codex uses a `commandWindows` field with the bare line.
  - The Grok plugin is macOS/Linux only.
- Plugin trees:
  - devin: `.devin-plugin/plugin.json`, `hooks.json` and `.mcp.json`.
  - cline: a POSIX script; the event comes from its filename.
  - gemini: `gemini-extension.json`, with `mcpServers.rtok = {command: rtok, args: [mcp]}` and no `trust` field, plus `hooks/hooks.json`, which maps Gemini events to `rtok hook <ClaudeEvent> --host gemini`.
- The pi extension:
  - Returns `{failed}` on a non-ENOENT error and keeps the original output.
  - Sends the ketch hint once per session.
  - Keeps non-text blocks such as images in order; the compressed text replaces the text blocks at the first text position.
- Claude plugin extras:
  - `agents/rtok-scout.md`: model haiku; tools `mcp__plugin_rtok_rtok__{read,search,outline,explore,expand}`.
  - The worktree.sh fallback decodes JSON with jq, else perl `JSON::PP`; with neither it exits 1 naming "jq or perl".
- `rtok-hook` must sit on PATH:
  - The ketch manifest links both `rtok` and `rtok-hook`.
  - On Unix, rtok symlinks `rtok-hook` beside the first `rtok` on PATH when that binary is the running exe. It re-points a dangling or other-version link and never replaces a regular file or a foreign link. This runs at `rtok mcp`, `hook --serve` and `agents install`.
  - doctor prints a "hook client" advice line when hooks prefer `rtok-hook` and it is missing.
- Rule: every plugin README links the host's official docs for packaging, hooks, MCP and skills.

### B3. Host install, update and remove (`rtok agents install|update|uninstall`)
T-ids: T79, T86, T94, T96, T117, T118.2, T164, T169, T185, T186, T196, T242.1, T242.2, T242.3, T242.4, T242.5, T243, T244, T246.1, T246.2, T246.3, T246.4, T246.5, T246.6, T249, T271, T275.2, T307, T332, T333, T399, T413.1, T413.2, T413.3, T413.4, T413.5, T413.6, T413.7, T413.8, T413.9, T413.10, T413.11, T413.12, T413.14, T413.15, T426, T520, T521, T569, T570, T611, T625, T626, T627, T628, T629, T636
- Host config editing rule: only rtok's own entry changes; every other byte of the file stays as it was.
  - JSONC edits preserve comments (zed, vscode `chat.pluginLocations`, amp). Removing an entry skips comments when it looks for the comma and re-parses the result before writing.
  - TOML edits go through toml_edit (codex, kimi, reasonix, grok). An unparseable TOML file is refused and left untouched.
  - YAML edits are text-level and validated by a YAML parser (goose, continue).
  - Strict-JSON hosts refuse a broken file with a file+line message.
- Hosts added in this range, each with a `[setup.<host>]` path key and a shared `register_stdio_mcp` helper; unsupported surfaces report "not supported" with a reason:
  - cline (CLI + VS Code, 5 per-event hook links in ~/Documents/Cline/Hooks)
  - gemini (~/.gemini/settings.json; events BeforeTool/AfterTool/BeforeAgent/SessionStart/SessionEnd/PreCompress; 5 s timeout)
  - codewhale (`$CODEWHALE_HOME`, `[[hooks.hooks]] message_submit`)
  - mimo (local MCP shape `{type:"local", command:[..], enabled}`; plus a Desktop variant)
  - commandcode
  - roo, qwen, goose, droid, kiro (amazonq folded into it), amp, junie, augment, warp, openhands, continue, crush, reasonix
- Host adapters normalize payloads:
  - cline: run_commands→Bash, read_files→Read; output `overrideInput{commands}`.
  - codewhale: `text`→prompt.
  - Copilot: `toolArgs` arriving as a JSON string is decoded to an object; an existing `tool_input` wins.
- Install states and refresh:
  - `present()` requires the app binary or bundle. When every requested host is absent the command fails with `error: <host> is not installed`.
  - Linked plugins are up-to-date, stale (relinked) or foreign (never touched).
  - A foreign plugin dir counts as a declined plugin.
  - A stale rtok hook or MCP entry is rewritten (`~ <event> <cmd>`); a pair no longer listed is dropped (`- <event>`); a current entry produces byte-identical output (NO_CHANGES).
- `rtok agents update [hosts] [--dry-run --cli/--desktop/--all --no-restart]`:
  - Claude: `claude plugin marketplace update rtok` + `claude plugin update rtok@rtok`; on failure, uninstall + install.
  - Codex: `codex plugin marketplace upgrade rtok`. The installed cache bytes before and after decide between "updated" and "already current"; on failure the plugin and marketplace are reinstalled.
  - Restart orchestration runs every preflight before it quits any app.
- Ownership-checked removal:
  - An entry equal to what the installer writes is removed.
  - An rtok entry the user edited prompts `? remove … you changed it [y/N]`: `--yes` removes it, and with no TTY rtok prints `leave … (changed by you; remove by hand)`.
  - A non-rtok entry gets `leave … (not rtok's; remove by hand)`.
  - Skill copies count as edited when anything is newer than the `.rtok-owned` marker.
  - Codex proxy install keeps the replaced provider as `# rtok: was "openai"` and restores it on removal.
- Proxy URL ownership comes from parsing the URL; only an http(s) loopback host counts (aider, Claude legacy migration). Substring matching is not used.
- `_backup` generations:
  - `[setup] backup_files` (default 5, 0 keeps all) caps `_backup/<name>.bak-<ts>[-<n>]` per base name.
  - Deduplication compares only against generations of the same file.
  - The newest copy is never pruned, and fs errors are ignored.
  - A run that changes nothing deletes its own copies.
- SDK atomic write follows symlink chains (cycles or >40 links are refused, with links left as they were). Paths never contain a literal `~`.
- Invariants:
  - After installing twice there is ≤1 rtok MCP entry and ≤1 rtok hook per event per surface, across hosts that cross-load each other's files.
  - D33: rtok's own config entry is always written and never "fixed" away.
  - Desktop/plugin variants unregister the duplicate `mcpServers.rtok`.
  - Cached plugin versions are ordered by version precedence, not by name.

### B4. MCP server
T-ids: T172, T190, T192, T194, T213, T353, T401, T528, T537
- Protocol conformance:
  - `initialize` echoes a supported client `protocolVersion`, else the pinned `2025-06-18`.
  - `-32601` errors carry the message "Method not found" with the method in data.
  - `tools/call` validates the `required` fields of each tool's input_schema and answers `isError` "invalid params: missing `x`" with `(got: <keys>)`.
  - Every tool error sets `isError: true` with a single "Error:" prefix.
- Argument tolerance:
  - Line ranges accept `a-b`, `"a-b"`, `a,b` and `a, b`.
  - `search` accepts `query` as an alias for `pattern`.
  - File errors name the path without the "(os error N)" tail.
- The `[mcp] tools` allow-list filters the listed tools; `expand` is always listed; an unknown tool answers `unknown tool: <name>`. 28 read-only tools carry `readOnlyHint=true`, and the codex install writes `default_tools_approval_mode="writes"`.
- `rtok mcp --wrap` proxy framing:
  - A malformed frame is forwarded raw and the stream resyncs.
  - Only response-shaped frames (no `method`) drain a pending `tools/call`.
  - Cursor `afterMCPExecution` returns `{}`.
- `rtok mcp --http [ADDR]` serves Streamable HTTP, stateless with JSON responses, bound to `[mcp] http = 127.0.0.1:8791` by default. It always requires a bearer token of 16+ chars (`RTOK_MCP_TOKEN` or `[mcp] token`), compared in constant time, never logged and redacted in output. It shares the allow-list, dispatch and measurement code with stdio.
- `rtok mcp --host <id>` tells the server its host (used by the agent-link rules, see area 6).

### B5. Worktrees
T-ids: T133, T152, T154, T157, T158, T159, T285, T286, T289.1, T289.2, T289.3, T289.4, T289.5, T410, T411, T412, T418, T423, T424, T453, T660.1, T660.2, T660.3, T660.4, T660.5
- `rtok worktree add <task> [slug] [--owner] [--agent]` prints only the path.
  - Directory `<repo>-<task>`, branch `<task>[-slug]`, both matching `[a-z0-9._-]`.
  - Runs `git fetch origin <default>`, then `git worktree add --lock --reason … --no-track -b`.
  - Lock reason v2: `<owner> | <task> | <date> | agent <uuid>`; the 3-field form is still parsed.
  - The default root is `.rtok/worktrees`, relative to the main checkout (previously ~/.rtok/worktrees). rtok makes sure `.rtok/` is ignored. A root is checked with `contain()`.
  - `[worktree] enabled=false` makes every worktree command fail and drops the worktree MCP tools.
- Commands: `list`, `whoami [--json]`, `claim <path>`, `adopt [<path>] [--task] [--agent] [--owner] [--json]`, `remove <path|task> [--agent] [--keep-branch] [--json]`, `gc [--idle 24h] [--stale-lock 7d] [--owner]`, `clean [paths] [--idle 24h] [--yes] [--json]`, `migrate [--yes] [--owner] [--json]`.
  - MCP tools: worktree_add, worktree_list, worktree_remove, worktree_adopt. Each binds to the session's linked agent and errors when none is linked.
- remove refuses a dirty or untracked worktree, a foreign lock and the caller's cwd, with exit 1 and the reason. It never forces. The branch is deleted only when merged (squash-aware). A failed branch delete after a successful removal is still exit 0, with the note `removed; branch kept: <git msg>`.
- gc rules:
  - A live agent's worktree is kept, unless it is "done" (merged, clean, with its own commits outside base) and idle past `--idle`.
  - A foreign or bare lock on a merged, clean, idle worktree is reclaimed after `--stale-lock`.
  - clean removes only CACHEDIR.TAG-signed caches that are idle, never the caller's own worktree.
- Host-made worktrees:
  - adopt takes the task from `--task`, else the old lock, else the branch's first `-` segment.
  - Evicting pools (Cursor, Codex, Windsurf/Devin) get a store claim with no git lock.
  - Post-create scripts are installed with `agents install <cursor|kilo|windsurf|devin> --project`.
  - With 0 or >1 live agents, adopt parks a pending claim (`worktree_pending`); the next adopt or SessionStart inside the worktree completes it.
- Claude WorktreeCreate/WorktreeRemove hooks route to `rtok hook WorktreeCreate`, which prints the path. Removal never forces.
- Project identity without spawning git: the project-root lookup for a cwd returns `{checkout, main via commondir, git_dir}`; a submodule is its own project. `contain(root, path)` follows symlinks through the deepest existing ancestor and applies `..` lexically after that. Linked worktrees nested in a project are skipped by walks and watchers.
- Performance: list and gc run per-worktree git work in parallel (one thread per core), resolve the base tree once and skip lstat of directories. Relative worktree links (`useRelativePaths`) stay off.

### B6. Agent registry, identity and messaging
T-ids: T282, T283, T283.1, T283.2, T283.3, T284, T287, T288, T290, T455, T473, T613
- Table `agents(id UUIDv7, host_id, host_session_id, parent_id, cwd, started_at, last_seen, ended_at, activity ≤120, status_text, ancestors)`, unique on (host, host_session, parent_key).
  - Every hook event upserts the row; SessionEnd ends it; a resume clears `ended_at`.
  - activity = tool name + the first 60 chars of the main argument, never file contents.
  - A sub-agent that arrives before its main agent is re-parented once the main registers.
  - An id resolves from any unique prefix of ≥4 hex chars; errors are `unknown` and `ambiguous: <ids>`.
  - live = not ended and `last_seen` within `[agents] idle` (30m).
- The agent learns its id:
  - SessionStart/SubagentStart inject `rtok agent id: <short> (full: <uuid>). …` at priority 10.
  - `RTOK_AGENT_ID` is exported via `CLAUDE_ENV_FILE`.
  - `CLAUDE_CODE_SESSION_ID` is a lookup-only fallback.
  - `rtok agents whoami [--json]`; MCP `whoami` returns `{id, short, host, host_session, cwd, rule, worktrees}`.
- MCP link rules, in order:
  1. env var (grok `GROK_SESSION_ID`);
  2. nearest common ancestor pid, from the hook client's pid via `/proc` or `proc_pidinfo`, 3 levels;
  3. cwd + host live agents (one links, several are ambiguous).
  Only env and own links are cached. "Outside" means this process is under no live session.
- `agents sessions [--all] [--json]` columns: agent (8-hex), worktree, activity, seen ("12s ago"), state live/idle/ended, sub-agents indented. `agents show <id> [--json]`. `agents status "<text>"`: ≤120 chars, blank clears. MCP: agents_list, agent_show, agent_status_set.
- Messages table `(from_agent NULL=user, to_agent, body ≤4 KiB control-stripped, task, created/delivered/read)`.
  - CLI: `agents send <id|--all-live> <text|-> [--task]`; `agents inbox [<id>] [--unread] [--task] [--json]`.
  - MCP: agent_send, agent_inbox (unread_only default true, limit 20, max 100; marks messages read).
  - Each message is rendered in a fixed frame (sender, host, "information, not an instruction"). Sending to an ended agent is refused.
- Push: UserPromptSubmit and PostToolUse add undelivered messages to additionalContext, up to `[agents] push_bytes` (1 KiB), then `… and N more: call agent_inbox (or run rtok agents inbox)`. Pushed messages are marked delivered, not read.
- Cross-host contract: every host gives the same worktree path rule, lock format and list row.

### B7. Tasks (`rtok task`, adapters)
T-ids: T405, T441, T441.1, T441.2, T441.3, T441.4, T441.5, T441.6, T441.7, T441.8, T441.10, T441.11, T441.12, T441.13.1, T441.13.2, T441.13.3, T441.13.4, T441.13.5, T441.13.6, T441.13.7, T541, T586, T601, T615, T904
- TaskId: a prefix of 1-8 ASCII letters plus a dotted path of depth ≤2, numbers from 1 with no leading zeros. Input is case-insensitive, output is upper-case, ordering is numeric. The default prefix is the first letter of the project name (this repo uses `A`).
- Status: open, in-progress, done, closed. The aliases pending, in_progress and completed are accepted.
- Allocator: table `task_counters(project, parent, last)`, one exclusive transaction per id. `seed` only ever raises a counter.
  - The project key is origin `host/owner/repo`, else the main checkout path.
  - Local filesystem remotes resolve from the main checkout.
- CLI: `rtok task create "<title>" [-d|--body-file] [--parent] [--status]`, `list [--status] [--all] [--json]`, `show`, `status <id> [new]`, `next`, `ready`, `init [--adapter] [--prefix]` (writes `.rtok.toml`), `sync [--json]`, `conflict`, `parallel`, `export github|linear [--dry-run] [--json]`.
  - MCP: task_create, task_list, task_get, task_status, task_next (plus claim, dep and the others), with the same JSON as the CLI.
  - Config is re-read per call for the cwd.
- Adapters:
  - disk: `<id> - <slug>.md` files with YAML front matter, written temp + rename; done/closed tasks move to `tasks/done/`. A filename id that differs from the front-matter id is an error. The claim lock is an OS lock on a stable `.claim.lock` that is never unlinked.
  - github: labels `rtok` and `rtok:<id>`, issue title `R12. Title`, subtasks as sub-issues. Token from GH_TOKEN, then GITHUB_TOKEN, then `gh auth token`. Writes are paced 1 s apart and wait out retry-after (up to 1 min). Projects v2 Status is set over GraphQL.
  - gitlab: REST v4 with `status::` labels.
  - Markers: `rtok:conflicts:<id>`, `rtok:parallel`, `rtok:needs:`, `rtok:p:`.
- Finishing a parent with active subtasks fails, naming them, unless `--force`. Ids are never reused.
  - Claims are ordered by a monotonic seq; the latest claim is injected at SessionStart/PostCompact.
  - Conflict markers are symmetric.
- `worktree add` writes `env.CLAUDE_CODE_TASK_LIST_ID=<project>-<task>` into `.claude/settings.local.json` (fail open).
  - Export is one-way and idempotent: done→completed, closed→not_planned; Linear uses `LINEAR_API_KEY`.
  - Web Tasks page and TUI tab: read-only columns per status.
  - The `rtok-tasks` skill is installed by `agents install`.

### B8. Agent usage and cost (`rtok agents usage`)
T-ids: T358, T358.1, T358.2, T358.3, T358.4, T358.5, T358.6, T389
- Screen: header `rtok agents usage — logs from N agents, through <day> (<tz>)`. Below it:
  - summary lines: tokens, estimated cost, sessions, daily rows;
  - `! Cost is incomplete: N models have no price in [stats.prices]…` when any model is unpriced;
  - the per-agent table (cost descending; an all-unpriced agent shows `-`);
  - monthly totals, or day rows with `--daily`.
- Flags map to `[agents.usage]`: `--source logs|rtok|both` (default logs), `--host`, `--since/--until` (date or 30d/24h), `--daily/--monthly`, `--by agent|model`, `--tz <IANA>` (DST-aware buckets), `--unpriced`, `--json`. Units are decimal SI (22.1K, 155.45M, 26.52B); JSON has exact integers.
- tokens = input + cache_write + cache_read + output (+ reasoning). JSON keeps the legs separate. JSON fields: `source, tz, through, totals{…, cost_usd, sessions, daily_rows}, unpriced_models, unpriced[], agents[{host, name, tokens, cost_usd, saved_tokens?, saved_usd?, through_rtok_tokens?, coverage?}], periods[], skipped[]`.
- Model ids are normalized before price lookup: the provider prefix and any `-YYYYMMDD` suffix are stripped. The raw id stays in JSON. Prices come only from `[stats.prices]`, never from the network. Defaults include claude-fable-5-1 (10/12.5/0.25/50), claude-opus-5-5 (4/5/0.2/20) and claude-sonnet-5-5 (2/2.5/0.2/10) USD/MTok for input/cache-write/cache-read/output.
- Log readers, read on the fly and never stored:
  - Claude Code jsonl, deduplicated by message.id;
  - Codex token_count lines;
  - OpenCode/Kilo: SQLite `message.data` JSON, opened read-only;
  - Copilot: `modelMetrics` of `session.shutdown`;
  - Gemini: chat JSONL;
  - pi: usage entries;
  - Kimi: `usage.record` lines in wire.jsonl;
  - Droid, Grok, ZCode and Antigravity are marked unsupported in `skipped`.
  Per-host dirs come from `[agents.usage.dirs]` with env overrides.
- The `rtok` source groups store usage by host through the session (otherwise `unattributed (<api>)`) and adds saved tokens from measurements (expand counted as a cost): `rtok saved N tokens (≈ $X)`. Usage page on web and TUI with a 120 s background TTL.

### B9. Proxy (API proxy, lanes, cache, batch)
T-ids: T205, T212, T298, T303, T323, T385.1, T385.2, T385.4, T385.5, T385.6, T385.7, T385.8, T385.9.1, T385.9.2, T385.10, T385.11, T385.12.1, T385.12.2, T385.13, T432, T442, T532, T533, T583, T584
- Lanes: agent, bulk, batch, files, embeddings, meta, internal.
  - Classification order: by path (batch, files, embeddings, models/count-tokens on Anthropic/OpenAI/Gemini), then a `/lane/<name>/` prefix (which wins), then the `x-rtok-lane` header. Markers are stripped.
  - `calls.kind` is `api_request:<lane>`.
  - Per-lane policy can only narrow the global one (compress, toon, tools_rewrite, context_management, semantic_cache, timeout_s). batch and files pass through.
  - Per lane: upstream, max_in_flight, max_queued=8; a full queue returns 429 Retry-After. The agent lane is never capped.
- Hop header `x-rtok-proxied: <hops+1>`. hops>0 takes the plain path (no record, compress or cache) with a warn line; hops≥8 returns 508 Loop Detected; a non-number counts as 1.
- Semantic/direct cache key:
  - Includes every top-level param except messages, model, system, tools, stream, metadata and user, key-sorted.
  - Includes the full canonical tool definitions, tool_use input, unknown blocks as canonical JSON, OpenAI tool_calls/tool_call_id, the full canonical image/document source, and a per-message typed digest (is_error, name, image_url.detail, block types; cache_control dropped).
  - Is scoped by caller identity (sha256 of x-api-key, authorization, x-goog-api-key, anthropic-version, anthropic-beta, openai-organization), the method and the resolved upstream URL, all hashed.
  - Only whole 2xx bodies are cached; Responses API traffic is not cached.
- compress mode strips terminal noise from tool results and user text: ANSI, control and zero-width characters, keeping `\n \r \t`, whitespace and wrappers. Assistant turns are never touched. The result is byte-stable across turns so the prompt cache still hits.
- `rtok batch submit <jsonl>|status <id>|fetch <id> <out> --provider --url`:
  - `[proxy.batch] parse_results` turns results into usage rows, keyed by `sha256(api, target, line id)` with insert-or-ignore, so a repeated download adds nothing.
  - `calls.service_tier` comes from the response.
  - Flex: `[proxy.flex] on_429 none|backoff|default`, up to 5 retries, 30 s cap.
  - Prices `<model>@batch` and `<model>@flex`.
- Routing `[proxy.routing.bulk|internal] from/to/provider/gate` acts only when every field is set and never on the agent lane.
- LLM summaries `[plugins.compress.llm]` run through the internal lane into `archive_summaries` (first writer wins). A must_keep guard rejects a summary that drops identifiers, paths, numbers or errors. Archived blocks outside the live zone become `summary + expand(<id>)`.
- Shaping runs off the async thread (spawn_blocking); a failure forwards the original body. `/health` stays fast. SSE usage is decoded per event with lossy UTF-8, so a cut multibyte tail loses only that event.
- Bench floor: 86.4% end-to-end saving on the proxy fixture. Research-only items, not built: measurement of read duplication across sessions, deferred schemas, P28.

### B10. Store, SQLite, archive and expand
T-ids: T102, T127, T207, T208, T209, T210, T211, T324, T352, T431, T433, T531, T534, T354, T599, T672, T906, T907
- Migrations seen in this range: 0019 archive.agent_id; 0020 notes_topic unique (COALESCE(project,''), kind, title); 0022 call_io raw BLOBs; 0023 measurements_session_ts; 0025 worktree_claims; 0026 messages; 0029 agent_ancestors; 0031 task_counters; 0032 hook_sessions; 0033 note_files; 0035 note_versions; 0043 calls.service_tier; 0045 worktree_pending; 0046 messages.task; 0047 task_claim_seq; 0048 usage.result_key; 0049 archive_summaries.
- Typed queries throughout, with no LIKE prefix matching (byte-safe compare). Tables: sessions, calls, measurements, kv, archive, call_io, read_cache, archive_decisions, usage, symbols, extractor, symbol_stale, notes(+fts), plugin_state.
- Atomicity:
  - freeze + Measurement in one immediate transaction;
  - call_io writes files first, then one transaction; files are removed on failure;
  - upsert_note uses `ON CONFLICT … RETURNING`;
  - archive files are written temp + rename (content-addressed);
  - `measurement_totals` uses GROUP BY, with expand rows counted negative.
- Saved request bodies are cleaned (`sanitize`) before spill: harness wrappers (`<task-notification>`, `<ci-monitor-event>`, `<system-reminder>`, `<local-command-caveat>`), ANSI, CRLF and trailing whitespace. In JSON only string values are cleaned. Cleaning is idempotent in one pass and invalid UTF-8 is kept. `[core] store_raw=true` keeps bodies verbatim. Session fields of hook stdin are stored once in `hook_sessions` and spliced back on read.
- Non-UTF-8 bodies are stored byte-exact, so expand is lossless. `archive.agent_id` scopes dedup per sub-agent; `rtok run --agent <id>` takes `[A-Za-z0-9_-]{1,64}`.
- Retention: `core.retain_hook_bodies_days` (3) clears hook bodies in 5000-row transactions; symbols of vanished roots are dropped; `PRAGMA incremental_vacuum` runs afterwards. A new store uses `auto_vacuum=INCREMENTAL`; an existing one is converted once.
- Archive ids are printed short (8 hex) everywhere: trailer, marker, read dedup (`unchanged id <id>`), graph caps. `expand` resolves an 8+ char prefix; an ambiguous prefix lists candidates by 12-char prefix. Measurement `ref_id` keeps the full id.
- Lossless roundtrip: every shortening plugin's output expands back byte-identical (CRLF, non-UTF-8, empty bodies). `expand` given a non-hex id answers `(expand takes the hex id from an "expand <id>" trailer; it does not read stdin)`.
- `expand` validates the range against the real line count before the freeze transaction; an empty archive rejects every range.

### B11. Read plugin (read, search, tree, roots)
T-ids: T135, T136, T300, T351, T378, T383, T384, T435, T529, T535, T590, T670
- Native Read deny:
  - `[plugins.read] range_max_lines=300`: a native Read with limit 1..300 passes; anything else is denied, and each deny records a cost Measurement.
  - Deny text per host: claude `use mcp__rtok__read (ToolSearch select:mcp__rtok__read loads it); before Edit run native Read(limit=1) — it satisfies the edit gate`; gemini `mcp_rtok_read`; any other host `use rtok read`.
- Roots allowed by MCP read/search/tree:
  - cwd and `allow_paths`;
  - the worktrees of the cwd's repo and of every client `roots/list` root (cached 30 s);
  - every `file://` root (the first becomes the cwd).
  Scratchpads stay outside. A refusal reads `path outside cwd: <p> (outside this project and its worktrees; read it with the host's own file tool)`.
- Path resolution follows symlink semantics: before popping `..` the prefix is canonicalized if it exists.
- Modes: full, lines, map, signatures, stripped, `density:X`.
  - density keeps the rarest-word lines up to X of the bytes, with line numbers and `… N lines omitted` gaps.
  - Measurements: map/signatures against the whole file; a `read_cap` row when `max_chars` cuts a fresh full/lines read; search_cap/tree_cap rows only when cut; at most one row per call.
- Transcript cache keyed by (path, size, mtime_ns) in `transcripts-cache.json`. The stats rows read_whole and read_deny come from this plugin. A trigram prefilter for search was not built (p95 ≤157 ms at 2k files).

### B12. cmd output filters and `rtok run`
T-ids: T160, T175, T176, T177, T235.1, T235.2, T235.3, T238, T240, T247, T296, T297, T321, T325, T366, T386, T536, T662, T673, T674, T675, T676, T677, T678, T679, T680, T681, T682, T683, T684, T685, T688, T905
- Trailer `[rtok <id> · N lines]` and the marker `… N lines omitted (expand <id>)`:
  - printed only when the output differs from raw beyond whitespace runs and ANSI CSI/OSC;
  - not printed when the body is no larger than the trailer (raw printed, zero-saving Measurement);
  - with no archive, the marker names no id;
  - the measured "after" includes the trailer bytes.
- Bounded commands pass through unchanged: head (non-negative), tail (no +N, no -f), `sed -n a[,b]p`, grep/rg with -A/-B/-C/-m, `cat -n` on one named file. A recursive grep needs `-m` to count as bounded. A chain of 2+ distinct programs takes the `[script]` rule.
- PreToolUse Bash rewrite:
  - Leading `cd <dir> &&` hops stay outside the wrap: `cd x && rtok run -- 'cargo test'`.
  - A command with any other cd, pushd, popd, export, source, `.`, unset, alias or unalias stage is not wrapped.
  - mise exec/x, `just -c` and recipes are peeled.
- `rtok run`:
  - runs `sh -c` (not a login shell) with a reader per pipe, draining 200 ms after the child exits;
  - exits 128+signal on a signal death;
  - watch loops exit when the parent dies;
  - on store failure writes the raw bytes plus a trailing newline;
  - shows a spinner `running <cmd>…` only on a TTY.
- Formatters:
  - git log header `[N commits, +A/-D]`;
  - tiered diffs (tier 1 up to 120 lines; above that a per-file table plus full hunks for the smallest files; 100% of paths kept);
  - git status: one line per XY code with brace grouping, ahead/behind folded into the branch line;
  - ls -l: >30 files grouped by extension, a `dotfiles:` line;
  - find keeps full paths up to 50 hits;
  - grep/rg/git grep: path once, `line:text`, ±60 chars around the match, 12 matches per file, 16-line budget, then `path:l1,l2` anchors;
  - cat of a source file >80 lines prints definition spans + signatures; a repeat in the session prints a pointer or a unified diff;
  - logfmt constants header;
  - wc;
  - cargo warnings grouped by lint (`unused_imports ×3`);
  - lint diagnostics for ruff, mypy, rubocop and golangci-lint;
  - mise tidy;
  - python: pytest, unittest, tracebacks with `[previous frame repeated N more times]`;
  - gh: pr checks show non-pass rows plus counts; `run view --log` shows the failing step;
  - docker/podman ps;
  - kubectl columns by header offsets;
  - `shorten_ago` only for number + unit;
  - df, sqlite3, awk;
  - loop collapse `shape ×N (vals: a..b)` for for/while/until, never collapsing error words.
- Golden fixtures carry a `min_saving: <pct>` floor, and every formatter arm must have a golden. Token estimator Class rates: 3.5/4.2/3.0/1.0 chars/token.
- `rtok discover [--since] [--limit] [--json]` ranks unfiltered command families by estimated output tokens.

### B13. Guard (dedup deny of repeated read-only calls)
T-ids: T299, T322, T350, T445, T555, T556, T557, T558, T559
- Bash key `bash\t{cwd}\t{cmd}`:
  - whitespace is collapsed only outside quotes and escapes; newlines become `;`;
  - the `rtok run` wrapper is stripped first;
  - an unterminated quote gives no key (fail open);
  - cd hops are kept verbatim; a relative hop command is never keyed.
- Read-only classification:
  - split at every unquoted `| ; & newline`; `$(` and backticks are never read-only;
  - git branch is read-only only with listing flags;
  - writers: awk writers, a print-only sed parser (w, e, r, -f, -i, `[` in a regex count as writers), find -fprint*/-fls/-ok*, git --output and its abbreviations.
- Read key `read\t{path}[slice]` with offset/limit/pages as a suffix. `path_identity` resolves a relative path against the hook cwd lexically; no cwd or `..` gives no key, and such an Edit/Write clears every read key.
- A body over 256 KiB is not archived and its key is cleared. Deny Measurement: before = archived body, est = bytes/4, after = 0.
- Skill digest:
  - frontmatter host keys are parsed as YAML (quoted keys count); unparseable frontmatter is exempt;
  - the single-install fallback applies only to records with no scope or projectPath.
- Non-ASCII input never crashes (char-boundary-safe slicing).

### B14. Memory, checkpoints and docs plugin
T-ids: T202, T203, T373, T374, T417, T419, T452, T454, T472, T538, T539, T548, T549, T550, T551, T552, T553, T562, T563, T564, T565, T566, T617, T618, T668
- Notes and recall:
  - `note_files(note_id, path)` links notes to the files they name.
  - Prompt recall fuses FTS with file-linked notes through RRF; ties are broken by id; up to 2 notes come from prompt-named files.
  - Hybrid RRF over FTS and vectors applies when `[plugins.memory.embed]` is enabled. `provider="http"` POSTs to `<url>/embeddings` (OpenAI-compatible); the default is local hash vectors.
- `note_versions`: an upsert that changes the body stores the previous title and body (version = max+1) in the same transaction; `checkpoint:*` and `session:*` kinds are exempt. `rtok memory history <id>` prints oldest first.
  - A revise keeps the exact scope (a global note stays global).
  - Import reserves a body hash only after its topic is accepted.
- Privacy: `<private>…</private>` text is stripped from checkpoints before store and restore, and from each whole observation field before the 200-char cut.
- `mem_pack` budgets the serialized output (id/tier headers included); `used_tokens` equals the estimate of the returned text.
  - `observations_for_files` returns distinct ids.
  - Mention cleaning keeps leading dots (`.env`, `./src/x.rs:12-30`).
- memory sync:
  - writes a managed block `<!-- rtok:memory -->…<!-- /rtok:memory -->` with exactly one marker pair;
  - titles are rendered on one line (`<!--` becomes `&lt;!--`);
  - the guard digest is keyed by the resolved destination path;
  - only NotFound counts as an empty file; any other read error aborts.
- Checkpoint:
  - streams the transcript with bounded state (last 20 prompts, last 8 errors);
  - the prefilter tolerates spaced JSON and mixed blocks;
  - skips meta, compact-summary, non-human and wrapper prompts (`<task-notification>`, `<ci-monitor-event>`, `<local-command-*>`, `[Request interrupted by user`);
  - the handoff project comes from the shared resolver;
  - quality counts are stored in kv.
- Store query `recent_hook_inputs_for_event(session, event, limit)` feeds the checkpoint edit window (5 PostToolUse bodies) and the handoff ledger (100 PreToolUse bodies).
- Docs plugin (docs_resolve/docs_query/docs_get, llms.txt): local docs are scoped `local:<canonical cwd>`; an unchanged re-ingest keeps doc ids (SHA compare); query savings are estimated against full bodies.

### B15. Graph and code index (projects, backends, export, diff, health, live)
T-ids: T230, T329, T329.9, T329.10, T329.11, T329.16, T329.17, T329.19, T329.24, T329.25, T329.30, T329.31, T329.34, T329.40, T329.41, T329.54, T334, T335, T336, T337, T367, T372, T451, T476, T477, T481, T485, T486, T542, T543, T544, T545, T546, T561, T567, T571
- Project registry: `projects` (canonical root, name, origin manual/session/worktree/mcp/reference, status) with an index per root.
  - Auto-add: session cwd, worktrees, graph MCP roots, and manifest references (Cargo path/patch/workspace members, npm file:/link:/workspace: with `!negative` patterns subtracted, go.mod replace/go.work, pyproject paths, .gitmodules). `reference_depth` 3, `max_auto_projects` 20.
  - Scope = the project plus its transitive links; queries default to the cwd project and its links.
- CLI `rtok graph projects [add|remove|select|link|unlink|reindex] --json`; every graph command and MCP tool takes an optional `project`. A missing path fails with `<path>: No such file or directory (os error 2)` / `not a directory`; `/` and `$HOME` are refused.
- Backends: `[plugins.graph] backend = tags` (default, byte-stable), lsp, auto, text.
  - `lsp_timeout_ms` 40000; `backend_by_language` overrides.
  - lsp without a server answers from tags with the header `(tags; lsp: <reason>)` and an `lsp_fallback` row.
  - text runs in-process (ignore + regex, definition keywords; dead not offered); no rg, grep or ssh is spawned.
  - Measurement kinds lsp.*, tags.*, text.*.
- Capability cache per root (`plugin_state capability:<root>`), single-flight. Requests never re-probe; the 60 s health check does, and a recovery needs two good checks.
  - Health score 0-100: freshness 40, backend 30 (lsp 1, tree-sitter 0.6, text 0.3), links 30; rings green ≥80, amber 50-79, red <50; scope score = lowest.
  - Alerts: missing, unreachable, backend down, index failing (3×), link broken. MCP notice: "project B unreachable since 14:02; results exclude B".
- Export `rtok graph export --format json|svg|png [--project] [--focus --depth] [-o] [--no-redact]` and MCP `graph_export`, schema `rtok.graph.v1` (projects, links, nodes, edges, meta). Redaction is on by default (home, user, absolute roots, scope, download name). Images draw at most 200 nodes with "N nodes hidden".
  - Diff `rtok graph diff [--from REF|--from-export] [--to REF|working] [--json]`: the old side is built from the object DB with no checkout. MCP `graph_diff` returns a capped summary with a paging id.
- Index freshness:
  - the fingerprint includes the extension map;
  - a dry run with a stale fingerprint predicts the cold run;
  - the rank document is refreshed after any change;
  - include patterns are additive and walk their subtree with ignore off; exclude uses gitignore-style globs and beats include, for walks and watcher events alike;
  - cold index commits 200 files per transaction.
- `affected` links tests by name convention (`tests/foo.rs`, `foo_test.rs`, `foo.test.*`, `foo.spec.*`, `__tests__/`, `test_foo.py`, `foo_test.go`), marked `← via (by name)`.
- Web: 3D graph (projects overview + drill-down, 2D fallback, 500 visible node cap), a read-only live graph with metrics (`live_heat_window_s` 300, `live_max_events_per_s` 50, `live_feed_rows` 200), export/import, backend pill, health ring, alert badges, reindex/remove actions with a job tracker.
- TUI graph page keys:
  - s select, l link, u unlink, b link both ways;
  - i reindex, x remove + y confirm;
  - e export, v view export;
  - c compare;
  - d drill (folded counter `<label> · <n> folded`);
  - health columns;
  - live metrics line `latency p50 30 ms (p95 100 ms)`.

### B16. Inject, prose compression, TOON and json_tree
T-ids: T604, T606, T608, T609
- Inject mode files are read capped by characters, at (ceil(budget·rate)+1)·4 bytes, and a partial UTF-8 tail is dropped. Fitting Unicode content survives in full; output is byte-identical across repeated runs.
- `compress_prose` treats both the character before and the character after a word as glue, so identifier and path suffixes are kept (`foo_a`, `/tmp/a`, `item1a`). Non-ASCII letters count as glue.
- Code fences follow CommonMark: backtick or tilde, ≤3 spaces of indent, a closer at least as long as the opener, an unclosed fence running to the end. A mid-line triple-backtick stays inline.
- json_tree: a hoisted reference and a literal string are distinct; a reference prints bare and an id-shaped literal is quoted.

### B17. Agent junk cleanup (`rtok agents junk`)
T-ids: T182, T330, T330.1, T330.2, T330.3, T330.3.1, T330.3.2, T330.4, T330.5, T330.5.1, T330.5.2, T330.5.3, T330.5.4, T330.6, T330.7, T338, T339, T340, T341, T342, T343, T344, T391, T479, T577
- `rtok agents junk list [--json] [--bytes] [--all] [--items N|all|0] [--sort size|last-used|path] [--min-size]`:
  - one row per installed host (`--all` adds the rest) plus `rtok`;
  - folders shown as OSC 8 `file://` links;
  - sizes are allocated blocks, with hard links and clones counted once, units with 1 decimal at 1KB=1024;
  - a folder shared by two agents is counted once;
  - JSON `agents[]{name,id?,folders[]{path,size_bytes,kinds[]{kind,class,size_bytes,items,paths_sample}},freed_default_bytes,freed_review_bytes},totals`.
- Kinds and classes:
  - safe: cache, temp (>24 h, not open), build (idle-tagged caches only), locks (no holder; package lockfiles never), swap, crash-dumps (after 7 days), rtok-own (logs past `[log] files`, archives past `retain_calls_days`);
  - review: logs (>keep_logs_days 30), deps (only with a lockfile), backups (newest kept), index, stale-worktrees (gc rules);
  - explicit: sessions (Claude Code only, >`stale_session_days` 30, 10-min in-use guard);
  - never: snapshots, credentials, settings, plugins, rtok.db/-wal/-shm.
- Only documented paths (the research §22 map per host, all 24 hosts), valid CACHEDIR.TAG dirs and `[agents.junk] extra` are cleared. Undocumented paths are listed read-only as "not documented: not cleared". Env overrides XDG_CACHE_HOME, CLAUDE_CONFIG_DIR and CODEX_HOME are respected.
- `clear [--yes] [--include review] [--kind] [--agent] [--older-than 7d] [--trash] [--session-days N] [--json]`:
  - dry run by default; prints "Nothing to clear" (exit 0) when empty;
  - re-checks each item before removal (symlink, protected path, nested lockfile, 60 s settle, agent running; "changed since plan");
  - for a running agent, skips temp, locks, swap and index;
  - exit 0 when everything planned was cleared, 1 when something planned was not removed, 2 on usage errors;
  - keeps the top folder and CACHEDIR.TAG.
- Config `[agents.junk]` keys: stale_session_days 30, keep_logs_days 30, stale_worktree_days 14, crash_dump_min_age_days 7, temp_min_age_hours 24, exclude, extra[{host,kind,path}]. Each is range-checked, with `RTOK_AGENTS_JUNK_STALE_SESSION_DAYS` as the env override. doctor prints a reclaimable-space line with a hint above 1 GB.
- Web Hosts card "Clear safe junk" (plan over /ws, then apply only the planned paths); TUI Hosts c/y/n with "Freed X of Y planned".

### B18. Doctor
T-ids: T171, T173, T331, T331.10, T388, T393, T576, T664
- Read-only by default; when fixes exist it prints "N problems can be fixed: run rtok doctor --fix". `--fix` (checklist + diff + confirm), `--yes`, `--only broken-hooks|duplicate-hooks|duplicate-mcp`, `--dry-run`, `--json problems[]{kind,agent,source,path,detail,fixable,selected,keep}`, `--agent <host>`. Exit 1 if a selected item was not fixed.
- Broken hooks are resolved without running them:
  - shell words with `~`, `$HOME`, `${VAR}` and host vars expanded;
  - interpreter scripts (bash, sh, zsh, node, python, deno, bun, ruby, pwsh, npx, tsx, uv run), then a PATH lookup (PATHEXT on Windows);
  - suspect: not executable, or not verifiable (`$()`, backticks, eval, pipes).
- Duplicates:
  - hooks: same event + normalized matcher + normalized command; keep order plugin > project shared > user > local;
  - MCP: same name across sources, or different names with the same launch; env values are never printed;
  - backups use `_backup` generations; managed files are never edited;
  - D33: rtok's own entry is never removable; only a hand-written copy under another name is.
- Lines and checks:
  - `mcp_tool_search <state> (<source>)` from ENABLE_TOOL_SEARCH (settings env > shell);
  - skill descriptions over 120 chars with recoverable tokens;
  - duplicate `mcpServers.rtok` while the plugin is installed;
  - plugin hook counts;
  - `https://api.anthropic.com` is not treated as a custom base.
- Web/TUI fix flow: plan returns a token of the selected entries + diff; apply writes only if a rescan yields the same token, else it returns a fresh plan. The `--ai` report uses a compact one-line-per-host-variant doctor text.

### B19. Config
T-ids: T184, T328, T362, T381, T594, T595, T630
- Home is always absolute.
  - A relative `RTOK_HOME`/`HOME` resolves against the cwd.
  - An empty `RTOK_HOME` falls back to `<user home>/.rtok`.
  - With no home at all: the OS home lookup, then the temp dir, never the cwd.
- The `.env` search uses is_file (a directory named `.env` does not stop the walk). The ketch store version is picked by semver.
- `config init` writes every default commented out (`# key = val`), map headers included. `config set` uncomments in place, using TOML-aware spans so string payloads are never touched. doctor/validate report explicit keys that differ from the default (key, value, default, line).
- `config validate` with no path creates the default user file first and prints `ok <home>/config.toml`; an explicit missing path errors. `config set` validates by typed extraction (defaults + candidate) in both real and dry runs, and a rejected set leaves the file unchanged.
- Layers: defaults < user file < project `.rtok.toml` < env < flags, with the source of each value reported. Pages reload with the originally selected `--config` file.

### B20. Web dashboard and TUI (operator surfaces)
T-ids: T206, T226, T227, T228, T229, T231, T232, T260, T436.1, T573, T574, T346, T619, T637, T639
- Parity rule D27: every CLI read has a page on both web and TUI, and write actions use the same function as the CLI with a dry-run plan, then confirm.
- Pages added in this range:
  - Config: key = value (source), `/` filter, read-only; `config get --json {key,value,source}`;
  - Services: demon rows (service, state, pid, uptime, log) + otel status;
  - Worktrees: Background cache with a 300 s TTL;
  - Hosts: `agents list` verbatim, cached doctor refresh, "probing hosts…";
  - Stats: price + cache tables;
  - Graph status;
  - Sessions: live-only checkbox, `?` help overlay.
- Web snapshot: Config cloned under a mutex and built off-thread; concurrent ticks are coalesced; `/health` never waits.
  - Live calls: CallBatch carries every ended id, so the display cap never marks a completed call interrupted.
  - SPA asset paths are percent-decoded once per segment; malformed escapes, non-UTF-8 and encoded separators get 400.
  - Every request control spins, sets aria-busy and settles on the next snapshot frame.
- TUI:
  - theme palette (accent cyan, muted grey, green/yellow/red);
  - panels scroll by wrapped rows and clamp on resize;
  - an open filter takes keys before the global `?` and `r`; Ctrl+C quits;
  - the selected Calls row stays visible.

### B21. CLI UX, progress and style
T-ids: T83.7, T83.16, T107, T276.1, T276.2, T276.4, T276.5, T315, T345, T348, T436, T436.2, T903
- Output style:
  - `[ui] emoji=true`, `[ui] color=true` (env `RTOK_UI_EMOJI`/`RTOK_UI_COLOR`);
  - one table per line kind: success ✅ green, status 💡 cyan, warning ❗ (one code point) yellow, error ❌ red, plus clap help/error styles;
  - colour obeys NO_COLOR, TERM=dumb and CLICOLOR_FORCE/FORCE_COLOR;
  - never applied to hook stdout, MCP, the proxy, run/filter output, `--json` or pipes.
- Operation icons:
  - a verb→icon table, substring-matched in order so uninstall wins over install;
  - 📦 install, ⏫ update, 🧹 remove, plus index, worktree, compress, expand, bench, start, stop;
  - a measured 2-column gutter.
- ProgressRunner is the only process-spawn API:
  - modes Capture, Stream, Inherit, Detached, with `.cwd/.env/.stdin/.timeout/.progress`;
  - indicators: TTY (one shared multi-progress on stderr), else hidden (non-TTY, `RTOK_NO_PROGRESS=1`);
  - Batch bar (`bench [###] 3/36`); parsers Git, Percent, Cargo and JsonLines; a Windows `cmd /C` shim;
  - always hidden in hooks and MCP; spinners on migrations (skipped in hooks), memory sync, report, otel flush, info.
- `--agent` means an agent id on worktree commands, and a host id (plus the pseudo-host `rtok`) on doctor and `agents junk`.
- Bad arguments exit 2 with the clap message. `parse_since` rejects `5x`, `-1d` and empty. Usage shows the bin name `rtok` (no .exe). Displayed paths use `/` on Windows.
- Shell completions for bash, zsh, fish and clink must stay byte-identical to the goldens (generated from the CLI definition).

### B22. Measurement, stats, bench and report
T-ids: T105, T134, T137, T165, T167, T179, T180, T239, T241, T302, T355, T397, T402, T403, T579, T580, T581, T587, T632, T633, T638
- Measurement rows: before/after equal the returned bytes on every surface (hook, MCP read, proxy). Benchmark floors: replay bench 76%, proxy bench 86.4%.
- `rtok stats` rows:
  - images: PNG IHDR/JPEG SOF size, tokens ceil(w/28)·ceil(h/28); the high-res tier at 2576 px costs 4784 tokens;
  - repeat reads classes: fired, changed, ranged, hook_absent, declined, subagent; the write-signal command table;
  - dictionary: repeated paths of 16+ bytes and identifiers of 12+ bytes; the row is absent when nothing repeats.
- Transcript walkers track canonical dirs and files (a symlinked alias is counted once), walk real entries before links and skip dangling links.
- Report:
  - `--format md|html|pdf` is optional, and an explicit value beats config;
  - `--ai` budgets the whole serialized text (header, store path, `dropped:` line, anchors), degrading to the title + dropped list, then to empty;
  - non-finite values print `—`; u64 counts saturate when cast for charts;
  - top sinks are filtered to actionable classes (read, cmd, mcp) before the top-10 cut, ranked by estimated tokens;
  - PDF: an orphan heading moves with its block, chart rows are budgeted at their drawn height, and the contents page shows physical page numbers.
- Bench `run_bounded` drains with a finite post-exit window and an 8 MiB cap, with stdin null.
- Research results (no build): PostToolUse `updatedToolOutput` works when shaped like tool_response; HTTPS MITM interception rejected; web results are 9.6% of tool bytes; the native-Read deny is net-negative for already-ranged reads; a structured output schema reaches ≤0.64% of input; a path dictionary is 0.44% cache-safe.

### B23. Logs, demon and OTel
T-ids: T589, T597, T624
- `rtok logs` follow tracks file generations by file identity (handle), returns the unread tail across rotation, retention and an empty replacement, and restarts from the top on in-place truncation.
- `demon stop|kill|restart` ownership: a held supervisor lock lets it signal both pids; a same-boot state without a lock signals only the child; anything else signals nothing. Escalation re-checks the state file.
- The OTel integration check extracts the Jaeger trace id structurally (32 hex).

### B24. Windows platform
T-ids: T83.3, T83.8, T83.9
- Detached children (demon start, otel flush) must not inherit pipe handles; port: spawn with handle inheritance disabled.
- `rtok run` on Windows quotes the body for cmd.exe with raw-arg semantics.

### B25. Skills
T-ids: T155, T234
- Skills have one source, `skills/<name>`; installers copy them into each host's skill root (pi: ~/.pi/agent/skills), and plugins never bundle them. SKILLS = rtok, rtok-worktrees (worktrees), rtok-tasks.
- Body ≤2048 bytes and description ≤120 chars, enforced by tests. Skill sync per host reports "no changes" when nothing changed.

### B26. Distribution and release tooling
T-ids: T183, T272, T634
- The marketplace publish tool covers only the Claude and Codex git catalogs (`.claude-plugin/marketplace.json`, `.agents/plugins/marketplace.json`).
- The ketch manifest pins `bin` entries for `rtok` and `rtok-hook` (first-match glob hazard on NTFS, where `rtok-hook.exe` sorts first).
- PyPI publish: dry run and target are independent flags, so `--dry-run --testpypi` never uploads.

### B-drop. Rust-only or non-functional, drop (do not port)
- Rust toolchain, crate moves, lint and CI: T163.1, T163.5, T163.6, T163.7 (raw SQL to Diesel DSL refactor; the table list is recorded in area 10), T223 (windows-sys versions), T236 (just dunnage), T266 (shared test harness), T273, T274, T902 (Windows clippy), T301 (test-cov), T311 (debug-info measure), T313 (sccache), T314 (cargo-fuzz targets: cli-argv, config-toml, cmd-filter, hook-io, jsonc-edit, proxy-wire, transcript-jsonl, outline, text-ops; port idea: SharpFuzz), T329.48 (TUI Panel refactor), T420 (selective `just check`), T436.3 (operation-icons crate extraction), T631 (build.rs git rerun hints), T640 (fs-kit crate adoption), T663 (jscpd macros), T900 (dependency bumps), T903.1 (published completions crate), T220 (Diesel schema-drift guard test).
- Test-only fixes: T83.2, T83.4, T83.5, T83.6, T83.10, T83.11, T83.12, T83.13, T83.14, T161, T162, T166, T168, T215, T251, T320, T406, T421, T425, T560, T643, T644, T666, T901, T903.2.
- Process, docs and audit: T198, T199, T218, T221, T224, T347, T400, T441.9, T650.
