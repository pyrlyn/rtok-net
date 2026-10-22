namespace Rtok.Plugins.Read.Tests;

public sealed class OutlineTests
{
    // Short sources padded past the size where an outline is worth it, as rtok's tests do.
    private static string Pad(string src)
    {
        var s = src.EndsWith('\n') ? src : src + "\n";
        for (var i = TextLines.Split(s).Count; i < 20; i++)
        {
            s += $"// pad {i}\n";
        }

        return s;
    }

    [Theory]
    [InlineData("a.rs", "fn main() {}\nfn helper() {}\n", "fn main")]
    [InlineData("a.ts", "function greet(n: string) { return n; }\n", "function greet")]
    [InlineData("a.js", "function add(x) { return x; }\n", "function add")]
    [InlineData("a.py", "def run():\n    return 1\n", "def run")]
    [InlineData("a.dart", "void start() {}\n", "void start")]
    [InlineData("a.c", "int add(int x) { return x; }\n", "int add")]
    [InlineData("a.go", "func Sum() int { return 0 }\n", "func Sum")]
    [InlineData("a.java", "class App { void start() {} }\n", "class App")]
    [InlineData("a.kt", "class App { fun start() {} }\n", "class App")]
    [InlineData("a.swift", "class App { func start() {} }\n", "class App")]
    [InlineData("a.cs", "class App { void Start() {} }\n", "class App")]
    [InlineData("a.rb", "def run; end\n", "def run")]
    [InlineData("a.php", "<?php\nfunction run() {}\n", "function run")]
    public void Map_and_signatures_name_the_definition_in_every_language(
        string path,
        string src,
        string needle
    )
    {
        src = Pad(src);
        Assert.Contains(needle, Outline.Render(path, src, "map"), StringComparison.Ordinal);
        var name = needle.Split(' ')[1];
        Assert.Contains(
            TextLines.Split(Outline.Render(path, src, "signatures")),
            l => l.Contains(name, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void An_unknown_language_falls_back_to_numbered_lines()
    {
        var text = Outline.Render("a.txt", "hello\nworld\n", "map");
        Assert.Equal("1:hello\n2:world\n(note: unknown language, showing lines 1-60)", text);
    }

    [Fact]
    public void A_markdown_map_skips_fenced_headings()
    {
        const string src =
            "# One\nFirst.\n```sh\n# not a heading\n```\n## Two\nSecond.\n### Three\nThird.\n";
        var hs = Markdown.Headings(src);
        Assert.Equal(3, hs.Count);
        Assert.Equal("First.", hs[0].Body);
        Assert.Equal("## Two", hs[1].Text);
        Assert.Equal("### Three", hs[2].Text);
        Assert.Equal(
            "h1 One 1\n  First.\nh2 Two 6\n  Second.\nh3 Three 8\n  Third.",
            Outline.Render("doc.md", src, "map")
        );
        Assert.Equal(3, TextLines.Split(Outline.Render("doc.mdx", src, "signatures")).Count);
        var digest = Markdown.Digest(src);
        Assert.Contains("## Two\n  Second.", digest, StringComparison.Ordinal);
        Assert.DoesNotContain("not a heading", digest, StringComparison.Ordinal);
    }

    [Fact]
    public void An_llms_txt_map_lists_links_and_skips_fences()
    {
        const string src =
            "# Docs\nIntro.\n## Pages\n- [Real](https://example.com/a): a note\n```sh\n- [Fake](https://example.com/no)\n```\n- [Other](docs/b.md)\n";
        var links = Markdown.LlmsLinks(src);
        Assert.Equal(
            [
                new LlmsLink("Real", "https://example.com/a", "a note"),
                new LlmsLink("Other", "docs/b.md", ""),
            ],
            links
        );
        var map = Outline.Render("docs/llms.txt", src, "map");
        Assert.Contains("h1 Docs 1", map, StringComparison.Ordinal);
        Assert.Contains("link Real https://example.com/a — a note", map, StringComparison.Ordinal);
        Assert.Contains("link Other docs/b.md", map, StringComparison.Ordinal);
        Assert.DoesNotContain("Fake", map, StringComparison.Ordinal);
        Assert.Contains(
            "link Other docs/b.md",
            Outline.Render("llms-full.txt", src, "map"),
            StringComparison.Ordinal
        );
        var md = Outline.Render("doc.md", src, "map");
        Assert.DoesNotContain("link ", md, StringComparison.Ordinal);
        Assert.Contains("h1 Docs 1", md, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a.rs", "/// gone\nfn keep() {}\n", "fn keep")]
    [InlineData("a.ts", "// gone\nfunction keep() { return 1; }\n", "function keep")]
    [InlineData("a.js", "// gone\nfunction keep() { return 1; }\n", "function keep")]
    [InlineData("a.py", "# gone\ndef keep():\n    return 1\n", "def keep")]
    [InlineData("a.dart", "// gone\nvoid keep() {}\n", "void keep")]
    [InlineData("a.c", "/* gone */\nint keep(void) { return 1; }\n", "int keep")]
    [InlineData("a.go", "// gone\nfunc Keep() int { return 1 }\n", "func Keep")]
    public void Stripped_drops_comments_and_keeps_lines(string path, string src, string keep)
    {
        src = Pad(src);
        var text = Outline.Stripped(path, src);
        Assert.NotNull(text);
        Assert.DoesNotContain("gone", text, StringComparison.Ordinal);
        Assert.Contains(keep, text, StringComparison.Ordinal);
        Assert.Equal(TextLines.Split(src).Count, TextLines.Split(text).Count);
    }

    [Fact]
    public void Stripped_of_an_unknown_language_is_null() =>
        Assert.Null(Outline.Stripped("a.txt", "// gone\nkeep\n"));

    [Theory]
    [InlineData("a.rs", "use crate::foo::Bar;\nfn main() {}\n", "Bar")]
    [InlineData(
        "a.ts",
        "import { foo } from './mod/helper';\nexport function greet() {}\n",
        "helper"
    )]
    [InlineData("a.js", "const x = require('pkg/util');\nfunction add(x) { return x; }\n", "util")]
    [InlineData("a.py", "from pkg.mod import helper\ndef run():\n    return 1\n", "helper")]
    [InlineData("a.go", "package p\nimport \"github.com/x/y\"\nfunc Sum() int { return 0 }\n", "y")]
    [InlineData("a.dart", "import 'package:foo/bar.dart';\nvoid start() {}\n", "bar")]
    public void Imports_are_rows_named_by_their_last_segment(string path, string src, string name)
    {
        src = Pad(src);
        var hits = Outline.Tags(path, src);
        Assert.Contains(
            hits,
            h =>
                h.Kind is "import"
                && !h.IsDef
                && string.Equals(h.Name, name, StringComparison.Ordinal)
        );
        var map = Outline.Render(path, src, "map");
        Assert.StartsWith("imports: ", map, StringComparison.Ordinal);
        Assert.Contains(name, map, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rust_use_keeps_its_full_path()
    {
        var hit = Assert.Single(
            Outline.Tags("a.rs", Pad("use crate::foo::Bar;\nfn main() {}\n")),
            h => h.Kind is "import" && h.Name is "Bar"
        );
        Assert.Equal("crate::foo::Bar", hit.ImportPath);
    }

    [Fact]
    public void A_rust_use_group_is_one_row_per_item_with_its_prefix()
    {
        var imports = Outline
            .Tags("a.rs", Pad("use crate::foo::{Bar, baz::Qux as Alias};\nfn main() {}\n"))
            .Where(h => h.Kind is "import" && !h.IsDef)
            .Select(h => (h.Name, h.ImportPath))
            .ToList();
        Assert.Contains(("Bar", "crate::foo::Bar"), imports);
        Assert.Contains(("Qux", "crate::foo::baz::Qux"), imports);
    }

    [Fact]
    public void A_tag_keeps_the_byte_range_of_its_node()
    {
        const string src = "fn keep() {\n    1\n}\n";
        var def = Assert.Single(Outline.Tags("a.rs", src), h => h.IsDef && h.Name is "keep");
        var slice = src[def.StartByte..def.EndByte];
        Assert.StartsWith("fn keep", slice, StringComparison.Ordinal);
        Assert.Contains('1', slice);
        Assert.Equal(1, def.Line);
        Assert.Equal(3, def.EndLine);
    }

    [Fact]
    public void The_extension_map_picks_a_grammar_for_an_unknown_suffix()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["rsx"] = "rust" };
        Assert.False(Outline.Supported("a.rsx"));
        Assert.True(Outline.Supported("a.rsx", map));
        Assert.Contains(
            Outline.Tags("a.rsx", "fn mapped() {}\n"u8.ToArray(), map),
            h => h.IsDef && h.Name is "mapped"
        );
        Assert.False(Outline.Supported(".bashrc"));
    }

    [Theory]
    [InlineData("a::b::{c, d::e as f}", "a::b::c,a::b::d::e")]
    [InlineData("use a::b;", "use a::b")]
    [InlineData("'./x/y.ts'", "./x/y.ts")]
    [InlineData("a::*", "a")]
    [InlineData("{self, x}", "x")]
    public void Import_clauses_expand_to_one_path_per_item(string raw, string paths) =>
        Assert.Equal(paths, string.Join(',', ImportSpec.Expand(raw).Select(r => r.Path)));

    [Theory]
    [InlineData("package:foo/bar.dart", "bar")]
    [InlineData("pkg.mod", "mod")]
    [InlineData("a::b::", "b")]
    [InlineData("'x'", "x")]
    public void The_last_segment_drops_module_suffixes(string raw, string name) =>
        Assert.Equal(name, ImportSpec.LastSegment(raw));

    [Fact]
    public void Lines_split_like_rtok()
    {
        Assert.Equal(["a", "b"], TextLines.Split("a\r\nb\n"));
        Assert.Equal(["a", "", "b\r"], TextLines.Split("a\n\nb\r"));
        Assert.Empty(TextLines.Split(""));
    }
}
