# T58. Done

`.jscpd.json` now ignores `**/Migrations/**`. The only such folder is
`src/Rtok.Store.Model/Migrations/` (two generated migrations, their designers and the model
snapshot), all written by `dotnet ef`.

Checked: `npx --yes jscpd@5.4.2 --config .jscpd.json .` exits 0 with 0.60 % duplicated lines
(40 clones over 652 files), down from 6.0 %. The threshold stays at 2 % for hand-written code.
markdownlint and CSharpier are green. No C# changed, so there was no build or test run.
