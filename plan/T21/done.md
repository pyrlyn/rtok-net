# T21. Done

What was done:
- `src/Rtok.Plugins.Inject` is the `inject` plugin (Hook surface, on by default).
  - On SessionStart it offers the configured modes at priority 5. UserPromptSubmit gets nothing, so modes go in once per session.
  - The budget itself stays in `Rtok.Core.InjectBudget` from T6, so the logic lives in one place. That covers the priority order, the `dropped:<plugin>:<est>` lines, the prefix of the one entry that crosses the budget, and the one `inject` Measurement per dispatch. That row prices the emitted entries and the dropped ones; its `ref_id` names the first dropped entry.
- `ModeFiles` handles the mode files.
  - `modes/terse.md`, `yagni.md` and `nudges.md` are copied from rtok and embedded. The aliases `cave` and `pony` also name built-ins.
  - A file in `plugins.inject.modes_dir` wins over the built-in mode. Mode names come from `plugins.inject.modes`, else `setup.modes`.
  - A file is read capped at `(ceil(budget·rate)+1)·4` bytes and kept to `ceil(budget·rate)+1` characters. A partial UTF-8 tail is dropped, and invalid bytes decode to U+FFFD as in a full decode. The result is always a prefix of the decoded file.
  - A name containing a path separator, `.` or `..` never leaves `modes_dir`.
  - A missing or unreadable file falls back to the built-in mode, or injects nothing. It never throws.
- `ProseCompressor.Compress` ports rtok's `compress_prose` with the Lite, Full and Ultra intensities.
  - Fences follow CommonMark. Inline backtick spans are protected, and negations are kept.
  - Glue counts on both sides of a word. Glue is `.`, `-`, `_`, `/`, `\`, digits and non-ASCII letters or digits; combining marks also count.
- Fix over rtok: rtok's whitespace cleanup removed the line break after a closing fence. That glued the next line onto the closer (```` ```Then ````) and unmade the fence. Now the break stays. Covered by `TheLineAfterAClosingFenceStaysOnItsOwnLine`.
- Left to other tasks:
  - The `--mode` flag of `rtok agents install`. No task in `plan/` names it yet. The plugin reads `[setup] modes`, which that flag writes, and the test sets that key directly.
  - The checkpoint and session-recall text that rtok's inject also offered at SessionStart. It belongs to the memory and checkpoint plugins.
  - Linking the plugin into the hook and CLI composition roots. None links any plugin yet.

Commit: `T21: inject plugin, prompt modes and prose compression` on branch `T21-inject`.

How it was checked:
- `tests/Rtok.Plugins.Inject.Tests` has 76 cases.
  - Several plugins go through the Dispatcher by priority up to 800 tokens. They produce `dropped:inject:3` and `dropped:c:500` lines and one inject Measurement with `est_before` covering every entry. The entry that crosses the budget keeps a prefix.
  - `[setup] modes = ["terse", "yagni"]` gives each built-in exactly once at priority 5, and a later prompt gives nothing.
  - Each shipped mode is at most 250 tokens with the default estimator.
  - Capped reads: fitting ASCII, two-, three- and four-byte text survives whole. A huge emoji file keeps 3361 scalars with no U+FFFD. The byte cap and partial tails are tested.
  - CsCheck, 500 file samples: each capped read is strict-valid UTF-8 and a prefix of the decoded file.
  - Two runs give byte-identical text, for both a 1 MiB mode and a Unicode mode.
  - Missing files, folders, `../escape` and empty names inject nothing.
  - The rtok glue and fence tables are ported, plus 4-space indent, tilde info strings, inline triple backticks and pleasantry edge cases.
  - CsCheck, 2000 samples: output is never longer in chars or UTF-8 bytes, and every fenced block survives in order, byte for byte.
- Mutation checks:
  - Dropping the left-glue check fails 11 cases.
  - Restoring rtok's fence line-break behaviour fails 8.
- Gates: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .` pass.
