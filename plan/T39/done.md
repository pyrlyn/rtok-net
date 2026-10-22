# T39. Done

`rtok agents junk list|clear` in `src/Rtok.Cli`, the scan and the clear in
`src/Rtok.Agents/Junk` (new), and the idle walk in `Pyrlyn.FsKit` 0.3.0.

- `Pyrlyn.FsKit`: `FsTree.Ages` gives the newest file mtime under a tree, whether a read
  failed, a deadline, and every `CACHEDIR.TAG` cache below with its own newest. Links are never
  followed and a nested checkout (a folder holding `.git`) is skipped. `CacheDirTag.IsCacheDir`
  checks the signature, and a linked tag is no tag. T40's `worktree clean` can reuse both.
- Map: `JunkMap` encodes the research.md §22 rows once per host: data home, temp, logs and
  caches, Electron cache subfolders and per-commit `CachedData`. It also holds the
  no-row hosts and the protected settings and credentials. `JunkRoots` resolves `{claude}`,
  `{codex}`, `{xdg_*}`, `{copilot_cache}` and `{rtok_cache}` from the environment, ignoring
  an empty or relative value.
- Scan (`JunkScan.Build`): rtok's own row comes first, then each installed host (every host
  with `--all`). The row covers:
  - rtok's `rtok.log.<N>` past `[log] files`, archives past `core.retain_calls_days`, its
    cache, and the language servers' state;
  - the 13 kinds, each item with its D36 evidence (§22, a valid tag, rtok-owned,
    `[agents.junk] extra`, gc verdict) and why it is kept;
  - excludes, with nested items folded into their parent.

  A folder two hosts share is sized and counted once and shows `shared with`. A find with no
  evidence is listed as "not documented: not cleared" and counted in no total.
- Classes: safe, review, explicit (sessions only when named) and never. `JunkFilter`
  combines kinds, agents, `--include review`, `--older-than` and `--min-size`. With no
  selecting flag only rtok's own logs and archives go.
- Clear (`JunkClear.Run`): the plan is the list's own counted, documented items. These stay:
  - a folder holding a package-manager lockfile;
  - a live kind, §22 path or extra path of a running host ("agent running");
  - a session inside the 10-minute guard or under `stale_session_days`.

  Right before removal each item is checked again: gone, a symlink, real path moved, protected
  (the store with `-wal`/`-shm`, the MCP files rtok edits, §22 credentials and settings), a
  nested lockfile, a lost tag, the 60 s settle, or a held file. Cache, build and index keep
  their top folder and `CACHEDIR.TAG`. `--trash` hands each entry to Pyrlyn.Trash. Archives
  go only through the store's retention with their rows (`--older-than` only widens it), never
  to the trash.
- CLI: `junk list [--json --bytes --all --items --sort --min-size]` and
  `junk clear [--yes --include --kind --agent --older-than --trash --session-days --min-size
  --json]`. Clear is a dry run with a removal preview until `--yes`. `--session-days`
  overlays `agents.junk.stale_session_days` as a config flag. Exit codes: 0 when done, 1 when
  something planned stayed (with `error: some junk could not be removed`), 2 on a usage error.
  Help, man, completions and `docs/cli/commands.json` were regenerated. `JsonTests` and
  `FlagCoverageTests` list the junk commands and flags. `ParityMap` puts both on the Hosts page
  of web and TUI.
- No new NuGet dependency: `Rtok.Agents` gains project references to Pyrlyn.Ignore,
  Pyrlyn.Trash and Rtok.Agent.Sdk.

Seams left for other tasks:

- Worktrees, stale worktrees and the `RemoveWorktree` hook are empty inputs until T40 fills
  them.
- Language-server state roots wait for T24.
- The running probe matches process names from the host catalog (off in the host sandbox);
  a store-backed live-session check waits for the agent registry.
- doctor, web and TUI views of junk belong to T32, T44 and T45.

Checked:

- `Pyrlyn.FsKit.Tests`: `Ages` (newest file only, tagged caches with their own newest,
  nested checkout skipped, links not followed, timeout) and `CacheDirTag` (signature, short
  and linked tags).
- `Rtok.Agents.Tests` (fake homes in temp dirs), covering:
  - the class of every kind; a package lockfile is never a lock;
  - an undocumented find is listed but never planned, while a tagged cache and an extra path
    are;
  - `CLAUDE_CONFIG_DIR`, `CODEX_HOME` and `XDG_CACHE_HOME` move the roots;
  - a shared folder counts once;
  - sessions: the in-use guard, the threshold, explicit only;
  - credentials, settings, plugins, sessions and `rtok.db`/`-wal`/`-shm` survive every flag
    with `--yes`, and only safe kinds go by default;
  - the running seam; the dry run changes nothing; "Nothing to clear";
  - the top folder and tag are kept; `--trash` through a fake;
  - a failed removal fails the run;
  - archives through the store fake and a failing retention;
  - the re-check skips: changed since plan, symlink, protected, nested lockfile, lost tag,
    settle;
  - `RetentionDays`, `--min-size` and `--items` parsing, links and binary units.
- `Rtok.Config.Tests`: `RTOK_AGENTS_JUNK_STALE_SESSION_DAYS` and the `--session-days` flag set
  `stale_session_days`. The `[agents.junk]` range checks were already tested.
- `Rtok.Cli.Tests`: Verify goldens of `junk list` text and `--json` on a fake home with
  Claude Code installed. Also tested: exit 0 for "Nothing to clear", the dry run then
  `--yes`, exit 1 when a read-only folder blocks removal (Unix), and exit 2 with the message
  for each bad flag.
- Gates: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`,
  `dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3`, all green.
