# rtok's open work and audit findings, as of the port

rtok's `plan.md` and `todo.md` held these open tasks and findings when the port was planned.
The "Port" column says whether the idea carries over to .NET: keep, adapt, or drop (specific
to rtok's old toolchain). Each kept item is folded into a task in `plan/`; its `Ports:` line
names the rtok id.

## 1. Active tasks (plan.md task table, 36 rows; all also in todo.md)

Legend: Port = keep / adapt / drop-rust-only. P = priority, C = complexity (1-5), R = readiness.
Many rows are measurement/probe tasks needing the creator's paid runs; in a port they become
"re-measure on the .NET build" rather than code.

| id | title | P | C | essence | Port |
|---|---|---|---|---|---|
| T124 | Realized `tools_rewrite` saving as a dated research.md row | P3 | 2 | Measure what `[proxy.tools_rewrite]` (max_description_tokens 60) really saves over >= 20 real sessions; local run says 16.2K tokens/request (42 %). Default stays off until 3 % gate + quality check. | keep (measurement; re-run after .NET proxy reaches parity) |
| T131 | Measure the spawn brief: cost row and on/off re-read share | P2 | 3 | Brief cost recorded as negative Measurement; `rtok stats` splits sub-agent re-read share by brief/no-brief via SPAWN_BRIEF_MARKER. Code done; needs paid flag-on window + default decision. | keep (port the fixture + split logic; measurement later) |
| T156 | Probe WorktreeCreate/Remove hooks and reflink-seeded target/ | P3 | 3 | No product code. (1) log hook payloads from claude --worktree / sub-agent / desktop; (2) reflink seeding of cargo target/ measured (cold 185 s vs seeded 371 s due to dunnage). | adapt: part (1) payload probe still relevant; part (2) is cargo-specific -> drop-rust-only (for .NET: bin/obj seeding is a different question) |
| T261 | CI ~9.5 min on macOS; webui recompiles 183 crates | P2 | 3 | CI split lint job, rust-cache for webui, DEBUG=0; draft PR #411 failed on jscpd 2.1 % > 2 % and 5-6 Windows test failures. Fold 82 test binaries estimate. | drop-rust-only (cargo/nextest/rust-cache). Adapt the idea: one test assembly, lint on ubuntu only, NuGet cache. Windows agents_install failures worth re-checking in .NET tests |
| T275 | Install/update removes rtok's MCP entry while a plugin serves MCP | P1 | 4 | D33: always write `rtok` entry in each agent's config; only remove deletes it; one shared MCP core driven by per-host McpSpec data, no per-host branches; table-driven Vfs test. Merged for all hosts; left creator-machine check. | keep (as D33 baked into design from day one) |
| T275.1 | `rtok mcp ping <agent>` | P2 | 3 | MCP tool `ping` returns `MCP <display name> жив`; headless hosts run `claude -p` etc. with a fixed prompt; desktop-only hosts: spawn configured server, initialize/tools/list/tools/call ping. `--json`, exit codes, timeouts. | keep |
| T277 | Move MCP core into crate `rtok-mcp` | P2 | 5 | a per-host MCP spec (McpSpec: paths per surface, format Json/Jsonc/Toml, key path, name, command form, entry shape, dup-name behaviour, ping cmd), registry, config (Fs trait), status, ping, doctor, an apply step. PR1+Claude done; rest of hosts pending. Guard test: no MCP keys under src/agents. | keep (as Rtok.Mcp.Install project from the start; skip the migration steps) |
| T278 | `agents info` reports real MCP state | P1 | 3 | Status parsed by exact key path + server name per surface (present/missing/stale+diff, plugin serves); same in --json, web model, agents list. Merged #464; creator check left. | keep |
| T279 | One plugin version scheme + `agents update` skips up-to-date plugin | P1 | 5 | All manifests at crate version; `.rtok-plugin-version` + receipt; SemVer compare (equal skip, newer update, lower warn; --force/--dry-run/--source); legacy 0.0.1 path; `agents outdated` (--json, --exit-code 10); CI version check script. Only Claude wired; some dead code left behind. | keep (version from assembly/`<Version>`; drive manifests from MSBuild) |
| T281 | Probe: tie a host session's hooks and its MCP server to one agent | P1 | 3 | Per-host research: how MCP process learns its session (env var, common ancestor pid, cwd+host+time); one MCP process per session? Docs table in research.md §26; probe kit run by creator. | keep (research is language-neutral; port the link rules) |
| T289 | Host-created worktrees join rtok: `worktree adopt` + post-create hooks | P2 | 4 | `rtok worktree adopt` / MCP worktree_adopt: claim host-made worktree (no git lock in self-evicting pools), origin derived from path; write rtok entry into .cursor/worktrees.json, .kilo/setup-script, Devin/Windsurf hook config; list shows origin. 4 stacked PRs. | keep |
| T356 | Never index $HOME or / as a graph root | P1 | 2 | Reuse walk_root_ok in graph root choice; error "no project: pass path or open a project"; retention drops existing home-root symbol rows (617K rows found). | keep (design in from the start; store migration only if reusing old DB) |
| T369.1 | Measure grep_symbol follow-up rate after opt-in window | P3 | 1 | Turn `plugins.guard.grep_symbol` on, measure follow-up Grep/Read share within 3 calls (<= 25 % -> propose default-on); idle-machine PreToolUse p95. | keep (measurement after port) |
| T370 | SessionStart repo map ranked by personalized PageRank | P1 | 4 | File graph from symbols scan (IDF-weighted edges), CSR power iteration 0.85/20 iters, global ranks stored per root (file_rank table, migration 0030), personalized (dirty/recent files, last checkpoint paths) computed in memory and never stored; one-pass budget fill; config map_rank refs\|pagerank. Backtest +17.4 pp; hook latency check open. | keep (pure algorithm, ports cleanly; use Span/arrays) |
| T385 | Proxy lanes + optimization plan (epic, 12 steps) | P1 | 5 | Lane classifier + calls.kind, per-lane policy, defaults bench, batch observe + parse_results, flex + 429 policy, per-lane cache-hit ledger + replay byte-stability, per-lane upstream + in-flight cap, P28 phases, routing, deferred tool schemas/thinking replay, `rtok batch` CLI + lane report. Most build steps landed in rtok (lanes, flex, routing, batch results). | keep (port the landed lane design as baseline) |
| T385.3 | Defaults bench: compress, context editing, skills, live_blobs | P1 | 3 | `rtok bench` cost per passed task per setting on/off; dated rows; needs creator API spend. | keep (measurement) |
| T385.9 | P28 Phase 2: async compressor on internal lane | P3 | 5 | LLMLingua-class compressor on `internal` lane, async, cached per archive id, agent lane never waits, original expandable; behind off flag; split .1 client+cache, .2 async live-zone use, .3 live bench. | keep (adapt: Channels/BackgroundService for async; flag off) |
| T385.9.3 | Gate P28 live bench for the compressor | P3 | 2 | Bench compressor vs lossless v0.1 incl. compressor tokens, must-keep fixture, expand recovery; local LM Studio attempt gave no evidence. | keep (measurement) |
| T394 | Run the paid live benches and record them | P2 | 2 | Nudges A/B, T9.2 legacy vs rtok, T68.9 graph MCP vs native; dated rows; gates P9 and P8b clause 4. | keep (measurement; also validates the port) |
| T395 | One real session as one OTel trace in SigNoz and Maple | P3 | 2 | Run docs/otel.md recipes with proxy in front of a live session; invoke_agent root + chat {model} spans; or drop the clause. | keep (OTLP JSON exporter; could use OpenTelemetry .NET SDK but D19 says project ledgers, so keep custom encoder) |
| T396 | Verify the usage readers against real files | P3 | 2 | Kilo, Gemini CLI, Copilot CLI (tokenDetails authoritative, last shutdown per session), pi, Grok signals.json readers checked on real data; fixtures per shape. Gemini/Grok left. | keep (port readers + fixtures) |
| T398 | Probe editors on a worktree.useRelativePaths worktree | P3 | 1 | VS Code, Zed, Cursor, lazygit on relative-link worktrees; four dated rows; setting stays opt-in until all pass. | keep (language-neutral probe) |
| T413 | More agent hosts (epic) | P2 | 3 | One sub-task per missing host: research row with sources, `Agent` impl + README, config paths, docs row, install matrix test against fake homes; reuse helpers. | keep (host table is data; port as data-driven) |
| T413.13 | `rtok agents install trae` | P3 | 2 | Trae docs name only project `.trae/mcp.json`; no documented user-level path -> no installer yet. | keep (blocked on docs) |
| T414 | Web dashboard restyle on the brand pack | P1 | 4 | React SPA restyle from `brand/` tokens (`--pyr-*`), React Aria Components, ECharts 6 behind own abstraction, grouped sidebar, command palette, clickable KPIs, live status, Δtok trend, URL filters, CSV/JSON export (T414.8-16). No brand file copies in web/. | port to Blazor WebAssembly (N13): the `--pyr-*` tokens, grouped sidebar, palette, KPIs, live status, Δtok trend, URL filters and export are rebuilt in `Rtok.Web.Client` (T44.2); ECharts behind own abstraction through JS interop |
| T414.7 | Re-shoot web/screenshots; close web admin gap in brand/ | P3 | 1 | Regenerate screenshots, mark web admin adopted in brand README/PROVENANCE. | keep (trivial, after the Blazor dashboard is served by .NET) |
| T416 | Shared `change-preview` crate for dry-run output | P1 | 3 | Unified diff (a/ b/ headers) or stat lines for edits, removal lines with sizes, totals line, colour via supports-color, human_bytes, Serialize for --json; published from pyrlyn/crates. (Cargo.toml already depends on change-preview 0.1.0.) | adapt: write a Rtok.ChangePreview library (DiffPlex for the diff) -> NuGet candidate shared with other .NET tools |
| T428 | SessionStart hook back under 10 ms | P1 | 3 | Per-phase timing; batch measurements in one transaction (defer/flush), read note bodies once; latency test with 40 notes; release gate re-run on quiet host left. | keep (design: one write transaction per hook from day one) |
| T436.4 | Spinner on agents install/update through ProgressRunner | P3 | 2 | One progress bar stepped per host on TTY, nothing on a pipe; done in code, creator manual run left. | adapt (Spectre.Console progress or own; TTY detection via Console.IsOutputRedirected) |
| T522 | `read` and `expand` accept agents' line-range spellings | P2 | 2 | parse_range accepts `420 - 640`, `580:650`, `[1, 50]`, `610 614`, `95<br>`, `235"-"270`, `1860_1960`, etc. (25 logged strings); still fails non-ranges. | keep (port parser + the 25 test strings) |
| T523 | `worktree add` takes `T504 read-hook-same-path` as id + slug | P2 | 2 | Split one argument into task id + slug (spaces to dashes); retry of an already-landed add returns the existing plan. | keep |
| T525 | SubagentStart and PreCompact under the 10 ms budget | P2 | 3 | SubagentStart p50 48.6 ms / p95 760 ms, PreCompact p50 25.7 ms; trace the work, get under 10 ms median or justify a higher budget; latency tests. | keep (design hot paths lean from the start) |
| T526 | worktree_list and search when `rtok mcp` started in $HOME | P2 | 2 | Use client `roots/list` root when cwd is not a repo; otherwise clear "pass a path or open a project" error, not git's fatal message. | keep |
| T527 | Worktree claim not held by an agent idle for days | P2 | 3 | Claim of ended agent or last_seen older than stale_lock (7d) does not block remove/adopt; refusal says how long ago holder was seen. | keep |
| T666.2 | Provider-backed memory extractor (blocked on creator) | P2 | 3 | Built-in `Extractor` (provider, model, key storage, per-run cost cap) beside the user-program extractor; off by default; fake-server test. | keep (blocked on decisions) |
| T687 | Hooks: PreToolUse/PostToolUse p50 to 10 ms | P2 | 4 | Re-measure §19.2 harness on idle machine (12.3/12.5 ms today), profile process start/config parse/store open/dispatch, remove largest cost. | adapt: the core NativeAOT question; measure .NET AOT hook client cold start vs Rust first (go/no-go metric for the port) |

Cards present in plan.md but NOT in the task table: T644 (tests remove temp dirs they create:
the in-memory test runtime leaks ~24K rtok-mem-* dirs) -> keep (use IDisposable temp dirs);
T672-T686 section header "Beat sqz, lean-ctx, token-optimizer and caveman per category" (no cards in
plan.md; source docs/research/competitors-techniques.md; rule: keep 100 % hashes/paths/WARN/ERROR,
never more tokens than raw, keep stderr + exit line on failure) -> keep as cmd-formatter acceptance rules;
T905 (podman ps uses docker ps formatter) done.
Historical review notes still marked open: T55.12 (Windows wrap_quote `''` under Git Bash drops
apostrophes) and T55.15 (live_blobs overwrites image/document source data -> invalid request) -> keep as
port regression tests.

---------------------------------------------------------------------------------------------------
## 2. Security findings (2026-10-14 audit of main@cd9644881) and cloud review

| # | Sev | Finding (one line) | Port |
|---|---|---|---|
| S1 | high | `rtok mcp --http` started in `/` or `$HOME` lets a token holder `read` any file there (single-file read lacks walk_root_ok). Fix: refuse --http in unwalkable cwd unless allow_paths; apply root check to read on HTTP. | keep (design in) |
| S2 | medium | Dashboard `/ws` has no auth (no-Origin clients pass): set config, doctor --fix, junk clear, expand bodies. Fix: per-run random token in URL (Jupyter style), refuse non-loopback web.host without it. (rtok has a token module now; verify the state there.) | keep (design in) |
| S3 | medium | `~/.rtok` store, archive, logs world-readable (umask; db 0644 917 MB). Fix: dirs 0700, files 0600, doctor warns/fixes. | keep (File.SetUnixFileMode / UnixCreateMode on .NET; ACLs on Windows) |
| S4 | medium | `read`/`search` serve project secrets (.env, .git/config, *.pem, id_*). Fix: default `plugins.read.deny_globs`, always enforced on HTTP. | keep |
| S5 | low | Proxy `/live` and `/health` unauthenticated; leak models/paths when bound wide. Fix: loopback only or same token as S2. | keep |
| S6 | low | `host_authority` takes the last comma-separated `Host` value. Fix: 400 on a Host containing `,`. | keep (Kestrel already rejects multiple Host headers; still validate comma) |
| S7 | low | Resident hook Unix socket chmod-ed after bind (umask window). Fix: create inside a 0700 dir or umask around bind. | keep (create socket in 0700 dir; Windows named pipe with PipeSecurity ACL) |
| S8 | low | `--http` is plain HTTP; bearer token in clear on non-loopback. Fix: docs + refuse non-loopback bind without https public_url. | keep |
| I-S1 | idea | One "exposure" check in doctor: wide binds, missing tokens, file modes. | keep |
| I-S2 | idea | Pin GitHub Actions to commit SHAs in release.yml. | keep (CI-level) |
| I-S3 | idea | Hash-shard `~/.rtok/archive` (tens of thousands of files in one dir). | keep (cheap to design in) |
| I-S4 | idea | Default retention for call_io bodies. | keep |
| I-S5 | idea | security.md threat model per surface (hooks, stdio MCP, HTTP MCP, proxy, dashboard). | keep |
| note | - | `cargo audit` could not run (broken advisory-db). | drop-rust-only (use `dotnet list package --vulnerable`) |

Cloud review (2026-10-08, main@a060af58; T457-T459 done):

| id | P | kind | one line | Port |
|---|---|---|---|---|
| T464 | P2 | move | `rtok-agent-sdk` backup (`_backup/` folder, N generations, prune, dedup) vs crate `file-backup` (writes beside file, never prunes); extend file-backup first. | drop-rust-only as a move; adapt: implement backup in Rtok.Agent.Sdk (NuGet candidate) |
| T465 | P2 | move | tools/dist-generate.sh, release.sh -> pyrlyn/ci; not shared copies, no move without creator OK. | drop-rust-only (cargo-dist); .NET release flow is new |
| T466 | P2 | move | `.github/actions/rustup-toolchain-cache` -> pyrlyn/ci; only rtok uses it. | drop-rust-only |
| T470 | P3 | move | tools/test-changed.sh, selective-check.sh -> scoped-check / pyrlyn/ci; compare first. | adapt (selective test runs per changed project) |
| (T416.1) | - | already tracked | dry-run rendering -> change-preview | adapt (see T416) |
| (T277) | - | already tracked | per-host MCP code -> crates/rtok-mcp; an unused apply step | keep design, skip dead code |
| (T436.3) | - | already tracked | OPERATION_ICONS -> shared crate `operation-icons` (merged in crates repo, unpublished) | adapt (small shared lib / NuGet) |
| (T279) | - | already tracked | test-only version-file writers and stale dead-code allowances | drop (do not port dead code) |

---------------------------------------------------------------------------------------------------
