# T7 done

**Layers and schema** (`src/Rtok.Config`), on Pyrlyn.Config.Layered and the Tomlyn source-generated serializer.
- Layers: defaults < user file < `<git root>/.rtok.toml` < `.env` (`RTOK_*` lines only) < `RTOK_<SECTION>_<KEY>` env < flags. Every key carries its source.
- The typed schema denies unknown keys and checks types, ranges and choices, with file:line. Outside `validate`, a bad value keeps its default with a warning.
- Home resolution (`RtokHome`), `~` expansion, the git root, the session id and legacy keys are ported. Legacy keys are folded before env and flags.
- `config/default.toml` is generated from the types and their doc comments, embedded, and guarded by a drift test.

**Commands** (`rtok config …`).
- `show [--sources] [--json]`, `get [--json]`, `set`, `validate [path]`, `init [--force]` and `path` behave as in the catalog (A30, B19).
- `set` goes through `TomlEditor`: comments and every other byte stay, and the result is validated before anything is written.
- Secrets (`otel.headers`, tokens) are redacted in show, get, set and the report rows.
- `init --dry-run` and `set --dry-run` print a unified diff from Pyrlyn.ChangePreview and write nothing. A dry `set` validates the value as a real run does. An absolute path loses its leading slash after `a/` and `b/`, as in `git diff --no-index`. When nothing would change, the output is `no changes`. The diff is coloured by the CLI's stdout colour rule.

**Flag coverage.** A test in `Rtok.Cli.Tests` reads the command model and fails on any flag without a config key, except listed action flags (now including `dry-run`). Every listed exception must still be in use.

Commits: the schema, loader, commands and coverage test were merged earlier (last c878569). The dry run is `T7: Config --dry-run diffs on init and set`.

**Checked.**
- `Rtok.Config.Tests`:
  - layering and the source of each key;
  - `.env` rules;
  - validator errors with file:line;
  - home resolution;
  - Verify goldens for `show --sources`, `show --json`, `set` on a commented file, `set --dry-run` and `init --dry-run`;
  - a rejected `set` (real or dry) leaves the disk unchanged;
  - `validate` with and without a path;
  - `init` refusing without `--force`;
  - redaction;
  - the `default.toml` drift test.
- `Pyrlyn.Config.Layered.Tests`: the CsCheck layering property.
- `Rtok.AotTests`: the source-generated context loads `default.toml`.
- `Rtok.Cli.Tests`: the flag-to-key coverage test, plus refreshed help and completion goldens.

`dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .` are green.
