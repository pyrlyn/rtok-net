# T12.8 done

New package `packages/Pyrlyn.OperationIcons` (0.1.0), a port of the `operation-icons` crate rtok and ketch share (rtok T436.3):

- `VerbIcons.Shared`: the twelve shared rows (🧹 uninstall/remov/prun, 📦 install, ⏫ upgrad/updat, ⏬ download/fetch, 🔗 link, ⏪ roll, 🔍 search, 🩺 doctor), matched as substrings of the lowercased verb, first row wins, so `uninstall` beats `install`.
- `VerbIcons.Icon(verb)` and `Icon(rows, verb)` for a program's own rows (rtok's index, worktree, compress, expand, bench, start and stop rows stay with rtok, as in the Rust split); null means "use the line kind's icon".
- `Width`, `Padding` and `Gutter`: every icon is padded to `IconWidth` = 2 columns plus a space, and the width is measured with Wcwidth (a narrow symbol plus U+FE0F counts two, as unicode-width does), never assumed.

New dependency: Wcwidth 4.0.1 (terminal column widths; the BCL has none).

Commit: `T12.8: Package Pyrlyn.OperationIcons: verb icons and gutter`.

Checked: 34 tests, the crate's tests plus a width test for every icon in the table (each measures 2 columns, pads 0, and its gutter measures 3) and width cases for narrow, wide, combining, control and U+FE0F text. `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (303) and `dotnet csharpier check .` green.
