# Pyrlyn.Ignore

Walk a directory the way git sees it, without running git.

- `.gitignore` at every level, `.ignore` (git or not), the repository's `info/exclude` and the
  global excludes file (`core.excludesFile` from the global or repository config, else
  `$XDG_CONFIG_HOME/git/ignore`).
- git's precedence: the deepest file that names a path decides, and within a file the last
  rule. `.ignore` beats `.gitignore`, which beats `info/exclude`, which beats the global file.
- git's pattern rules (gitignore(5), wildmatch): comments, `\` escapes, trailing spaces,
  `!` negation, `dir/` for directories only, anchoring by a leading or middle `/`, `*`, `?`,
  brackets and POSIX classes that never cross `/`, and `**` as a whole leading, trailing or
  middle component.
- An ignored directory is not entered, so nothing below it comes back, as in git. A nested
  repository starts a new git scope. A linked git worktree below the root is skipped;
  submodules and nested clones are walked. `.git` is never walked; symlinks are yielded, not
  followed.
- Overrides: `Exclude` (gitignore syntax, relative to the root) beats everything; `Include`
  is additive and walks the subtrees it names with the ignore files off.
- The filesystem root and the home directory are refused as walk roots.

A corpus test compares the walk with `git ls-files --others --cached --exclude-standard` on
fixture repos and on seeded random rule sets.

## Install

```bash
dotnet add package Pyrlyn.Ignore
```

## Use

```csharp
using Pyrlyn.Ignore;

var walker = new RepoWalker("/src/app", new WalkOptions { Exclude = ["*.min.js"] });
foreach (var entry in walker.Walk())
{
    if (!entry.IsDirectory)
    {
        Console.WriteLine(entry.RelativePath);
    }
}

// A watcher event: would the walk have reached this path?
bool walked = walker.IsWalked("/src/app/dist/app.js", isDirectory: false);

// One rule, or one file of rules, on its own.
var rules = IgnoreFile.Parse("/src/app", "*.log\n!keep.log\n");
IgnoreMatch m = rules.Match("/src/app/x/keep.log", isDirectory: false); // Whitelist
```
