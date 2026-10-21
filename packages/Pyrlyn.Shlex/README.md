# Pyrlyn.Shlex

POSIX shell word splitting and quoting: quotes, backslashes, line continuations and comments, with unterminated quotes reported.

## Install

```bash
dotnet add package Pyrlyn.Shlex
```

## Use

```csharp
using Pyrlyn.Shlex;

ShellWords.Split("git log --format=\"%h %s\" -- 'a b'");
// ["git", "log", "--format=%h %s", "--", "a b"]

ShellWords.TrySplit("echo 'open", out var words, out var error);
// false, error: "unterminated single quote at 5"

ShellWords.Join(["echo", "it's here"]);
// echo 'it'"'"'s here'
```

Splitting follows POSIX `sh` without expansion: blanks and newlines separate words, single
quotes are literal, double quotes let a backslash escape only `$`, `` ` ``, `"`, `\` and a
newline, backslash-newline continues the line, and `#` at the start of a word starts a comment.
Operators such as `|` and `;` are ordinary characters. An unterminated quote or a trailing
backslash is an error, never a guess.
