# Pyrlyn.ChangePreview

Dry-run previews of what a command would change: git-style unified diffs, stat lines, removal lines with sizes, totals, human byte sizes and optional colour.

## Install

```bash
dotnet add package Pyrlyn.ChangePreview
```

## Use

```csharp
using Pyrlyn.ChangePreview;

var preview = new Preview()
    .Edit("rtok.toml", before, after)            // dropped when nothing changes
    .Remove(".worktrees/T1/bin", 48_000_000, 1_204);

var text = preview.Render(PreviewMode.Stat);     // or PreviewMode.Diff
Console.Out.Write(PreviewText.Paint(text, color: stdoutWantsColor));
var json = PreviewJson.Serialize(preview);       // for --json
```

```text
 rtok.toml | 3 ++-
- .worktrees/T1/bin  45.8 MB  1 204 files
1 file changed, 2 insertions(+), 1 deletion(-)
1 path, 1 204 files, -45.8 MB
```

- **Diffs like git.** `UnifiedDiff.Render` prints `--- a/x` and `+++ b/x` headers, hunks
  with three lines of context, `@@ -0,0 +1,2 @@` for an empty side and
  `\ No newline at end of file`, the same bytes `git diff` prints for the same edit script.
  The line diff is DiffPlex's (Myers); the hunks are formatted here.
- **Stat lines.** One ` path | 7 +++----` line per edit, paths and counts aligned. Past 40
  changed lines every bar shrinks by the same ratio, rounded up so a one-line change still
  shows a mark.
- **Removals.** `- path  size  files` per deleted path, then a totals line:
  `2 paths, 48 214 files, -12.1 GB`. Counts group thousands with spaces; sizes use binary
  units with one decimal (`PreviewText.HumanBytes`, `PreviewText.Group`).
- **Colour is the caller's call.** `PreviewText.Paint(text, color)` makes file headers bold,
  hunk headers cyan, additions green and removals red. It does not look at the terminal or
  the environment: the caller applies its per-stream rule (`NO_COLOR`, `CLICOLOR`, tty,
  `TERM=dumb`) and passes the result.
- **JSON** through a source-generated context, AOT-safe:
  `{"edits":[{"path","insertions","deletions"}],"removals":[{"path","bytes","files"}]}`.
  The diff text stays out.
