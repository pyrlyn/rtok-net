# T8. Store on EF Core + SQLite: model, migrations runner, retention — done

What was done:
- `src/Rtok.Store.Model`: the entities of the final rtok schema (without the unused `events`, plus
  `plugin_state`), one `RtokDbContext`, the initial migration from `dotnet ef migrations add`
  (dotnet-ef is a local tool). FTS5 tables, their triggers and the expression index are
  `migrationBuilder.Sql` operations inside that migration. A design-time factory lets the library
  be its own startup project.
- `eng/Rtok.Store.Scripts`: a JIT-only tool that Rtok.Store runs at build. It writes one
  EF-generated SQL script per migration, plus the history-table script. Rtok.Store embeds them as
  `Rtok.Store.Migrations.<id>.sql`.
- `src/Rtok.Store`:
  - `RtokStore.Open` creates the directory 0700 and the files 0600 (the database, `-wal`,
    `-shm` and archived bodies). A directory that already exists keeps its mode.
  - It refuses a file rtok-net did not create (tables but no `__EFMigrationsHistory`, like the
    Rust rtok's `rtok.db`) or one with a migration this build does not know. It refuses by
    reading only, and the file is left byte for byte.
  - Then it sets busy_timeout, WAL and foreign keys, and `auto_vacuum=INCREMENTAL` on a new file.
  - The embedded runner applies the pending scripts in one write transaction with foreign keys
    off, as EF's table rebuilds expect. It checks foreign keys before the commit.
  - The write API covers:
    - sessions, hosts, providers and models;
    - calls with parent, ms, kind, tier and error;
    - tokens, usage (deduplicated by result key), logs;
    - measurements, with the once key so a call delivered twice counts once;
    - kv and plugin_state.
  - call_io:
    - Request bodies are cleaned before they spill, unless `store_raw`. Cleaning is
      idempotent, and a JSON string is cleaned in place.
    - A body over the inline cap is archived, content-addressed (one row and one file per
      body). On the hook path it keeps only its size.
    - Bytes that are not UTF-8 are kept exactly.
    - Hook stdin is split, so its session fields are saved once in `hook_sessions`.
    - A failed insert removes the archive file it created.
  - Retention, each step atomic:
    - calls by age, with the logs, tokens, call_io and archives only they referenced; usage,
      measurements and child calls stay, detached;
    - hook bodies by age, in batches, with the lock released between them;
    - orphan hook sessions;
    - symbol rows of vanished roots (and of `/` or home);
    - incremental vacuum, plus the one-time switch of an older file.
  - `Housekeeping` turns every failure into a warning.
- `src/Rtok.Store/StoreHost.targets`, imported by every executable that opens the store. At AOT
  publish:
  - EF scaffolds the compiled model in Rtok.Store.Model, which is the only place EF finds it.
  - EF precompiles every query in Rtok.Store.
  - Nothing generated is committed.
- `research.md` F records what the EF 10.0.13 precompiler needs; the store code follows it:
  - only locals in queries;
  - one captured value per `ExecuteUpdate`;
  - queries ended by an operator;
  - no `AsNoTracking`;
  - non-sealed entities.

Deviation: none from the done criteria. On SQLite linking (README N8, "T8's call, with the
creator"), the creator chose option (a): `libe_sqlite3` beside the binary, from the
`bundle_e_sqlite3` that Microsoft.EntityFrameworkCore.Sqlite brings. N8 records it. Static
linking moved to the new task T54, which runs in L2 before T50.

How it was checked:
- `tests/Rtok.Store.Tests` (JIT, 95 tests):
  - sanitize: the cases ported from rtok, plus CsCheck properties against the old regex
    fixed point;
  - hook-field split and rebuild;
  - open: modes, WAL, auto_vacuum, seeds, foreign keys, refusal of a foreign and of a newer
    store with the file unchanged, the empty file;
  - a Verify snapshot of `sqlite_master`;
  - model drift (`HasPendingModelChanges`) and the embedded script list;
  - every write method, call_io spill and cleanup, retention, vacuum, housekeeping.
- `tests/Rtok.AotTests/StoreTests.cs` (NativeAOT, run by Rtok.Smoke):
  - a fresh store applies every migration and runs every repository method once, including
    retention;
  - EF can neither build a model nor compile a query there, so a query the publish missed
    throws.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .`
  are green.
