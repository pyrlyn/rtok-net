---
status: in progress
priority: P0
complexity: 5
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T8. Store on EF Core + SQLite: model, migrations runner, retention

One SQLite file holds every ledger. Plugins never touch it directly (rtok D8, D13, D18), and the creator chose EF Core for it (N7).

Done when:
- `Rtok.Store` has a single `DbContext` with:
  - the entity set from the catalog: sessions, calls, call_io, tokens, logs, measurements (unique once_key), archive, read_cache, notes + FTS5, observations, usage, symbols, projects, agents, messages, task and worktree claims, otel watermarks, plugin_state, and the rest;
  - a compiled model and precompiled queries regenerated at publish;
  - WAL, `foreign_keys`, `busy_timeout`, `auto_vacuum=INCREMENTAL`.
- Migrations:
  - each is made with `dotnet ef migrations add`;
  - a per-migration SQL script is generated at build and embedded;
  - an embedded runner applies the pending ones in order and records them in `__EFMigrationsHistory`, because `Migrate()` does not work under AOT;
  - a schema snapshot test guards drift.
- FTS5 tables and triggers, which EF cannot model, are written as EF migration SQL operations, not ad hoc SQL.
- Retention is atomic: calls and hook bodies by age (`retain_calls_days`, `retain_hook_bodies_days`), symbols of vanished roots, and call_io bodies by default (rtok I-S4).
- Request bodies are cleaned before they spill, idempotently.
- The store directory is 0700 and its files 0600 (rtok S3).
- A database rtok-net did not create (the Rust rtok's `rtok.db`, no migration history) or one with a newer migration is refused with a clear message and left untouched; there is no import (README N15).

Tests:
- `Rtok.Store.Tests` integration: a fresh store in a `TempDir` reports `journal_mode=wal`, `foreign_keys=1`, a non-zero `busy_timeout` and `auto_vacuum=INCREMENTAL`.
- `Rtok.Store.Tests` integration: the embedded runner applies the embedded per-migration scripts in order on an empty file, records each in `__EFMigrationsHistory`, and a second run applies nothing.
- `Rtok.Store.Tests` Verify: the schema snapshot (tables, indexes, triggers from `sqlite_master` after every migration) matches the checked-in one, and the EF model has no changes pending against the last migration.
- `Rtok.Store.Tests` integration: the notes FTS5 table and its triggers come from migration SQL operations, and inserting, updating and deleting a note keeps `notes_fts` in step.
- `Rtok.Store.Tests` integration: a second Measurement with the same `once_key` adds no row.
- `Rtok.Store.Tests` integration: retention purges calls, call_io, tokens, logs, usage and archive older than `retain_calls_days` in one transaction, and an injected failure mid-purge leaves every row in place.
- `Rtok.Store.Tests` integration: `retain_hook_bodies_days` clears hook bodies in 5000-row transactions, call_io bodies are purged by default, symbols of vanished roots are dropped, and `incremental_vacuum` runs afterwards.
- `Rtok.Store.Tests` integration: two connections writing at once both succeed under `busy_timeout`, and a session-id prefix containing `%` or `_` matches only literally.
- `Rtok.Store.Tests` CsCheck: request-body cleaning strips harness wrappers, ANSI, CRLF and trailing whitespace, touches only JSON string values, keeps invalid UTF-8 bytes, and cleaning twice equals cleaning once.
- `Rtok.Store.Tests` unit: with `[core] store_raw=true` the body is stored verbatim.
- `Rtok.Store.Tests` integration: on macOS and Linux the store directory is created 0700 and the database, WAL and SHM files 0600 under a permissive umask.
- `Rtok.Store.Tests` integration: a file with no `__EFMigrationsHistory` (a Rust-rtok `rtok.db` fixture) and one with an unknown newer migration are refused with a message naming the file, and both stay byte-identical.
- `Rtok.Core.Tests` arch: no `Rtok.Plugins.*` project references `Rtok.Store` or EF Core.
- `Rtok.AotTests` AOT: a fresh store applies every migration and runs every repository method once with no query falling back to runtime model building.

Spec: catalog A15, catalog B10 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T0.3, T13.2, T13.3, T13.4, T34.1, T36.11, T102, T104, T207, T208, T209, T210, T211, T324, T352, T354, T431, T433, T531, T534, T599, T672, T906, T907; open S3, I-S4 ([open work](../../docs/port/open-work.md)).

Check: An AOT smoke test creates a fresh store, applies every migration, and runs every repository method once. Every published query is precompiled, so no query falls back to runtime model building.

Plan:
1. `src/Rtok.Store.Model`: entities for the final rtok schema (minus the unused `events`; plus `plugin_state`), one `RtokDbContext`, the initial EF migration (`dotnet ef migrations add`, dotnet-ef as a local tool); FTS5 tables, triggers and the expression index as `migrationBuilder.Sql` operations.
2. `eng/Rtok.Store.Scripts`: a JIT tool run at build that writes one EF-generated SQL script per migration (plus the history-table script); `src/Rtok.Store` embeds them.
3. `src/Rtok.Store`: open (0700 dir, 0600 files, busy_timeout, WAL, foreign_keys, auto_vacuum=INCREMENTAL on a new file), refusal of a store rtok-net did not create or one with a newer migration (N15), the embedded migration runner under one write transaction, the ledger write API (sessions, hosts, models, calls, call_io with spill and sanitize, hook session fields, tokens, usage, logs, measurements with once_key, kv, plugin_state) and atomic retention.
4. Tests: `tests/Rtok.Store.Tests` (JIT: schema snapshot, model drift, refusal, permissions, sanitize, retention, every repository method); an AOT smoke in `tests/Rtok.AotTests` (T2) that applies every migration and runs every repository method with precompiled queries.
5. Release `-warnaserror`, `dotnet test`, csharpier; `done.md`.
