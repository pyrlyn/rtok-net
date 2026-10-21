# T12.4. Done

New package `packages/Pyrlyn.Ignore` (0.1.0, AOT-clean, references `Pyrlyn.FsKit`):

- `IgnorePattern`: one gitignore(5) line: comments, `\#`/`\!` escapes, git's trailing-space
  trimming (an escaped space stays), `!` negation, a trailing `/` for directories only,
  anchoring by any inner or leading `/`, `**` (leading, trailing, middle), `*`, `?`, brackets
  with ranges, `!`/`^` negation and POSIX classes. Nothing but `**` crosses a `/`. Literal and
  `*.ext` fast paths, the rest compiled to `RegexOptions.NonBacktracking`. A malformed bracket
  never matches, as in git.
- `IgnoreFile`: one file's rules, relative to its directory; the last rule naming a path wins;
  a BOM and CRLF are handled.
- `RepoWalker` with `WalkOptions`: depth-first in ordinal name order, `.git` skipped, symlinks
  listed and never followed. Precedence follows ripgrep's `ignore` crate: overrides >
  `.ignore` > `.gitignore` (deepest first) > `.git/info/exclude` > the global excludes file
  (`core.excludesFile` from the XDG config, `~/.gitconfig` and the repo config, last one wins;
  default `$XDG_CONFIG_HOME/git/ignore`). Ignore files above the walk root up to the repo root
  apply; a nested repository starts a new git scope; git rules need a repository unless
  `RequireGit = false`. Linked worktrees below the root (a `.git` file whose gitdir has
  `commondir`) are skipped, submodules are not. `Include` brings back paths the ignore files
  skip, `Exclude` beats it, both in gitignore syntax. `MaxDepth`, `Hidden`, `IgnoreCase`.
  `IsWalked(path, isDir)` answers for one path without walking. The filesystem root and
  `$HOME` are refused.

Checked:
- `GitCorpusTests` builds fixture repos with real `git init` (fake `HOME`, no system config)
  and compares the walked files with `git ls-files --others --cached --exclude-standard`: root
  rules, nested files and precedence, `info/exclude` and the default global file,
  `core.excludesFile` from the global and the repo config, a walk below the root, symlinks, and
  8 seeded random rule sets on random trees. No differences. A one-off run of 300 seeds also
  matched git exactly.
- `IgnorePatternTests` (cases from gitignore(5) and wildmatch) and `RepoWalkerTests` (refusal,
  order, depth, `RequireGit`, `.ignore` over `.gitignore`, nested scope, fake and real linked
  worktrees, hidden, include/exclude, `IsWalked` agreeing with `Walk`): 90 tests in the package.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and
  `dotnet csharpier check .` green.

The test project sets `RtokProcessRunner=true` to run git.

Commit: `T12.4: Package Pyrlyn.Ignore: gitignore-correct walking`.
