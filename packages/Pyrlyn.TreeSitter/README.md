# Pyrlyn.TreeSitter

tree-sitter bindings with grammars linked statically per RID, plus tags extraction and bundled
tags queries. AOT-clean; native calls go through `LibraryImport`.

## Install

```bash
dotnet add package Pyrlyn.TreeSitter
```

## Use

```csharp
using Pyrlyn.TreeSitter;
using Pyrlyn.TreeSitter.Tags;

// Parse, walk, query.
using var parser = new Parser(Language.Rust);
using var tree = parser.Parse(source)!; // byte[] of UTF-8
foreach (var node in tree.Root.Descendants) { /* node.Kind, node.StartPoint, node.Text(source) */ }

using var query = new Query(Language.Rust, "((identifier) @id (#match? @id \"^new\"))");
using var cursor = new QueryCursor();
foreach (var match in cursor.Matches(query, tree.Root, source)) { /* match.Captures */ }

// Definitions and references, as tree-sitter-tags reports them.
var grammar = Grammar.Find("rs")!; // id or file extension
using var tags = new TagsContext();
var result = tags.GenerateTags(grammar.Tags, source);
foreach (var tag in result.Tags)
{
    var kind = grammar.Tags.SyntaxTypeName(tag.SyntaxTypeId); // "function", "call", ...
    var name = source.AsSpan(tag.NameRange.Start, tag.NameRange.Length);
}
```

## Grammars

c, c_sharp, dart, go, java, javascript, kotlin, php, python, ruby, rust, swift, tsx and
typescript. The C sources are the crates.io tarballs that rtok's `Cargo.lock` pins, listed with
their sha256 in `native/sources.txt`; `native/build-native.sh` downloads, checks and compiles
them into one static library (NativeAOT) and one shared library (JIT) per RID.

`Grammar.All` composes each tags query the way rtok's outline does: the grammar's upstream
`tags.scm` (and `locals.scm`), then rtok's own patterns (`Queries/<grammar>/extra.scm`,
`import.scm`; kotlin and c_sharp have no upstream tags query, so theirs is rtok's).

## Native library

The package carries `runtimes/<rid>/native/` for osx-arm64, osx-x64, linux-x64, linux-arm64,
win-x64 and win-arm64, and `build/Pyrlyn.TreeSitter.targets`, which links the static library
into a NativeAOT publish. Inside this repository a project that references the package project
imports that targets file itself; the build runs `build-native.sh` for the target RID (cached
in `PYRLYN_TREESITTER_CACHE`, default `~/.cache/pyrlyn-treesitter`).

## Differences from tree-sitter-tags

`TagsContext` ports tree-sitter-tags 0.25.10 (locals, docs with `select-adjacent!` and
`strip!`, line ranges cut at 180 bytes, UTF-16 columns). One change: a name claimed by an
`@ignore` capture is never emitted, also when the queue is drained at the end.
