---
status: todo
priority: P0
complexity: 5
readiness: 0%
agent:
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

Spec: catalog A15, catalog B10 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T0.3, T13.2, T13.3, T13.4, T34.1, T36.11, T102, T104, T207, T208, T209, T210, T211, T324, T352, T354, T431, T433, T531, T534, T599, T672, T906, T907; open S3, I-S4 ([open work](../../docs/port/open-work.md)).

Check: An AOT smoke test creates a fresh store, applies every migration, and runs every repository method once. Every published query is precompiled, so no query falls back to runtime model building.
