# T12.2 done

New package `packages/Pyrlyn.ChangePreview` (0.1.0), a port of the `change-preview` crate:

- `Preview` collects what a command would change: `Edit(path, before, after)` (dropped when the text is the same) and `Remove(path, bytes, files)`. It also has `IsEmpty`, `Totals()` and `Render(PreviewMode.Diff | PreviewMode.Stat)`. Diff mode prints every edit's unified diff. Stat mode prints aligned ` path | 7 +++----` lines; past 40 changed lines every bar shrinks by the same ratio, rounded up. Both modes then print the removal lines (`- path  12.1 GB  48 213 files`) and one totals line each for edits and removals, the crate's text byte for byte.
- `UnifiedDiff.Render(path, before, after)`: `a/` and `b/` headers, three lines of context, nearby changes merged into one hunk, `@@ -0,0 +1,2 @@` for an empty side, and `\ No newline at end of file`. The line diff is DiffPlex's, through a chunker that keeps line ends. DiffPlex's own `UnidiffRenderer` prints `-1,0` for an empty side and stray `+`/`-` lines at a final newline, so the hunks are formatted here. Checked against `git diff --no-index` for a new file, a missing final newline and two separate hunks.
- `PreviewText.Paint(text, color)` uses the crate's escapes: bold `---`/`+++`, cyan `@@`, green `+`, red `-`. The caller decides whether colour is on. The per-stream rule (NO_COLOR, CLICOLOR, CLICOLOR_FORCE, TERM=dumb) is T11's, so the package keeps no second copy of it. `PreviewText.HumanBytes` (binary units, one decimal, invariant culture) and `PreviewText.Group` (`1 204 311`).
- `PreviewJson.Serialize` writes the `--json` model through a source-generated context, AOT-safe: `{"edits":[{"path","insertions","deletions"}],"removals":[{"path","bytes","files"}]}`, with the diff text left out, as in the crate.

New dependency: DiffPlex 1.9.0, for the line diff (Myers) under the unified preview.

Checked:
- The crate's tests are ported: diff headers and body, a no-op edit, diff-mode and stat-mode totals, the 40-column bar cap, removal lines in both modes, an empty preview, human bytes, grouping, paint without colour, and the JSON shape.
- Further tests: exact escapes with colour on; `@@` ranges for a one-line hunk, an empty old side and an empty new side; the missing-newline marker; separate hunks and merged hunks.
- A CsCheck property applies every rendered diff to the old text the way `patch` does and gets the new text. It also checks that the edit counts match the `+`/`-` lines. Breaking the empty-range rule makes it fail.
- Verify snapshots of edit, stat, removal and totals output, with and without colour (`tests/SnapshotTests.*.verified.txt`, escapes shown as `␛`).
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .` are green.

Left to the consumers: `--dry-run`/`--stat` flags, dry-run defaults for destructive commands, and filling `Remove` from the file system belong to the commands that use the preview (rtok's `stat_preview.rs`/`removal_preview.rs` glue).
