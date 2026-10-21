using Rtok.Testing;
using Testably.Abstractions.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>Rules files: strict parsing, user file and drop-in merging, issue reports (rtok's rules.rs tests).</summary>
public sealed class RuleParserTests
{
    private const StringComparison Ord = StringComparison.Ordinal;

    private const string TenLines =
        "line 0\nline 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7\nline 8\nline 9";

    private static void Write(MockFileSystem fs, string path, string text)
    {
        var dir = fs.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            fs.Directory.CreateDirectory(dir);
        }

        fs.File.WriteAllText(path, text);
    }

    // An absolute dir in the mock's temp path: the twin of rtok's disk tests.
    private static string TempDir(MockFileSystem fs, string name)
    {
        var dir = fs.Path.Combine(fs.Path.GetTempPath(), name);
        fs.Directory.CreateDirectory(dir);
        return dir;
    }

    private static int Count(string s) => Text.Lines(s).Count;

    [Fact]
    public void User_rules_file_changes_output_for_match_cmd()
    {
        var fs = MemFs.Create();
        var path = fs.Path.Combine(TempDir(fs, "rtok-rules"), "rules.toml");
        Write(fs, path, "[echo]\nmax_lines = 2\nhead = 1\ntail = 1\ndedupe = false\n");
        var s = CmdSettings.Load(fs, path, null, 80);
        var output = RuleEngine.Apply(s, TenLines, 0, s.Pick("echo"), "id");
        Assert.True(Count(output) == 2, output);
    }

    [Fact]
    public void User_rule_overrides_builtin_match_cmd()
    {
        var fs = MemFs.Create();
        var path = fs.Path.Combine(TempDir(fs, "rtok-rules-ovr"), "rules.toml");
        Write(fs, path, "[grep]\nmax_lines = 5\nhead = 2\ntail = 2\ndedupe = false\n");
        Assert.Equal(5u, CmdSettings.Load(fs, path, null, 80).Pick("grep").MaxLines);
    }

    // b.toml wins over the file and a.toml; a new family is appended.
    [Fact]
    public void Drop_ins_merge_in_name_order_after_the_user_file()
    {
        var fs = MemFs.Create();
        var dir = TempDir(fs, "rtok-rules-d");
        var dropIns = fs.Path.Combine(dir, "rules.d");
        var file = fs.Path.Combine(dir, "rules.toml");
        Write(fs, file, "[grep]\nmax_lines = 5\n");
        Write(fs, fs.Path.Combine(dropIns, "b.toml"), "[grep]\nmax_lines = 7\n");
        Write(
            fs,
            fs.Path.Combine(dropIns, "a.toml"),
            "[grep]\nmax_lines = 6\n[pytest]\nmax_lines = 9\n"
        );
        Write(fs, fs.Path.Combine(dropIns, "skip.txt"), "[grep]\nmax_lines = 1\n");
        var s = CmdSettings.Load(fs, file, dropIns, 80);
        Assert.Equal(7u, s.Pick("grep").MaxLines);
        Assert.Equal(9u, s.Pick("pytest").MaxLines);
        Assert.Equal(80u, s.Pick("cat").MaxLines);
    }

    // A malformed drop-in is skipped; the good files still apply.
    [Fact]
    public void A_broken_drop_in_is_skipped_fail_open()
    {
        var fs = MemFs.Create();
        var dir = TempDir(fs, "rtok-rules-bad");
        var dropIns = fs.Path.Combine(dir, "rules.d");
        Write(fs, fs.Path.Combine(dropIns, "bad.toml"), "[grep\nmax_lines = \n");
        Write(fs, fs.Path.Combine(dropIns, "good.toml"), "[grep]\nmax_lines = 7\n");
        var missing = fs.Path.Combine(dir, "no-such-rules.toml");
        Assert.Equal(7u, CmdSettings.Load(fs, missing, dropIns, 80).Pick("grep").MaxLines);
        // A missing dir is built-ins, not an error.
        var noDir = fs.Path.Combine(dir, "no-such-d");
        Assert.Equal(40u, CmdSettings.Load(fs, missing, noDir, 80).Pick("grep").MaxLines);
    }

    [Fact]
    public void User_rules_file_changes_output_for_match_cmd_from_vfs()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.toml", "[echo]\nmax_lines = 2\nhead = 1\ntail = 1\ndedupe = false\n");
        var s = CmdSettings.Load(fs, "rules.toml", null, 80);
        var output = RuleEngine.Apply(s, TenLines, 0, s.Pick("echo"), "id");
        Assert.True(Count(output) == 2, output);
    }

    [Fact]
    public void User_rule_overrides_builtin_match_cmd_from_vfs()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.toml", "[grep]\nmax_lines = 5\nhead = 2\ntail = 2\ndedupe = false\n");
        Assert.Equal(5u, CmdSettings.Load(fs, "rules.toml", null, 80).Pick("grep").MaxLines);
    }

    [Fact]
    public void Drop_ins_merge_in_name_order_after_the_user_file_from_vfs()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.toml", "[grep]\nmax_lines = 5\n");
        Write(fs, "rules.d/b.toml", "[grep]\nmax_lines = 7\n");
        Write(fs, "rules.d/a.toml", "[grep]\nmax_lines = 6\n[pytest]\nmax_lines = 9\n");
        Write(fs, "rules.d/skip.txt", "[grep]\nmax_lines = 1\n");
        var s = CmdSettings.Load(fs, "rules.toml", "rules.d", 80);
        Assert.Equal(7u, s.Pick("grep").MaxLines);
        Assert.Equal(9u, s.Pick("pytest").MaxLines);
        Assert.Equal(80u, s.Pick("cat").MaxLines);
    }

    [Fact]
    public void A_broken_drop_in_is_skipped_fail_open_from_vfs()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.d/bad.toml", "[grep\nmax_lines = \n");
        Write(fs, "rules.d/good.toml", "[grep]\nmax_lines = 7\n");
        Assert.Equal(
            7u,
            CmdSettings.Load(fs, "no-such-rules.toml", "rules.d", 80).Pick("grep").MaxLines
        );
        Assert.Equal(
            40u,
            CmdSettings.Load(fs, "no-such-rules.toml", "no-such-d", 80).Pick("grep").MaxLines
        );
    }

    // An empty rules file keeps the built-ins; unknown commands use the default rule.
    [Fact]
    public void Empty_rules_file_from_vfs_keeps_builtins()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.toml", "");
        var s = CmdSettings.Load(fs, "rules.toml", null, 80);
        Assert.Equal(40u, s.Pick("grep").MaxLines);
        Assert.Equal(80u, s.Pick("cat").MaxLines);
        Assert.Equal(40u, s.Pick("echo").MaxLines);
    }

    [Fact]
    public void Vfs_rules_under_spaced_profile_path()
    {
        var fs = MemFs.Create();
        const string path = "Users/Ivan Tuhai/.config/rtok/rules.toml";
        Write(fs, path, "[rg]\nmax_lines = 3\nhead = 1\ntail = 1\ndedupe = false\n");
        var s = CmdSettings.Load(fs, path, null, 80);
        Assert.Equal(3u, s.Pick("rg").MaxLines);
        var body = "L0\nL1\nL2\nL3\nL4\nL5\nL6\nL7\nL8\nL9";
        var output = RuleEngine.Apply(s, body, 0, s.Pick("rg"), "id");
        Assert.True(Count(output) == 3, output);
    }

    [Fact]
    public void Vfs_dropin_later_file_overrides_earlier_and_user()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.toml", "[grep]\nmax_lines = 1\n");
        Write(fs, "rules.d/01-a.toml", "[grep]\nmax_lines = 2\n");
        Write(fs, "rules.d/02-b.toml", "[grep]\nmax_lines = 9\n");
        Assert.Equal(9u, CmdSettings.Load(fs, "rules.toml", "rules.d", 80).Pick("grep").MaxLines);
    }

    // Unicode path segments are ordinary paths.
    [Fact]
    public void Vfs_rules_under_unicode_path_string()
    {
        var fs = MemFs.Create();
        const string path = "профіль/настройки/rules.toml";
        Write(fs, path, "[cat]\nmax_lines = 4\nhead = 2\ntail = 2\ndedupe = false\n");
        Assert.Equal(4u, CmdSettings.Load(fs, path, null, 80).Pick("cat").MaxLines);
        const string dropDir = "профіль/настройки/rules.d";
        Write(fs, dropDir + "/zz.toml", "[cat]\nmax_lines = 11\n");
        Assert.Equal(11u, CmdSettings.Load(fs, path, dropDir, 80).Pick("cat").MaxLines);
    }

    // `max_lines = 0` parses; apply clamps the budget to 1, so the output is one trailer.
    [Fact]
    public void Vfs_max_lines_zero_clamps_to_one_on_apply()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.toml", "[echo]\nmax_lines = 0\nhead = 1\ntail = 1\ndedupe = false\n");
        var s = CmdSettings.Load(fs, "rules.toml", null, 80);
        Assert.Equal(0u, s.Pick("echo").MaxLines);
        var output = RuleEngine.Apply(s, "a\nb\nc\n", 0, s.Pick("echo"), "id");
        Assert.True(Count(output) == 1, output);
        Assert.Contains("3 lines omitted", output, Ord);
    }

    // Only *.toml drop-ins merge, in name order.
    [Fact]
    public void Vfs_many_dropins_merge_name_order_skipping_non_toml()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.toml", "[grep]\nmax_lines = 1\n");
        Write(fs, "rules.d/10.toml", "[grep]\nmax_lines = 3\n[jq]\nmax_lines = 2\n");
        Write(fs, "rules.d/20.toml", "[grep]\nmax_lines = 5\n");
        Write(fs, "rules.d/30.toml", "[grep]\nmax_lines = 8\n[fd]\nmax_lines = 6\n");
        Write(fs, "rules.d/note.md", "[grep]\nmax_lines = 99\n");
        Write(fs, "rules.d/00.bak", "[grep]\nmax_lines = 99\n");
        var s = CmdSettings.Load(fs, "rules.toml", "rules.d", 80);
        Assert.Equal(8u, s.Pick("grep").MaxLines);
        Assert.Equal(2u, s.Pick("jq").MaxLines);
        Assert.Equal(6u, s.Pick("fd").MaxLines);
    }

    [Theory]
    [InlineData("[grep]\nmax_lines = \"many\"\n", "max_lines")]
    [InlineData("[grep]\nmax_lines = -1\n", "max_lines")]
    [InlineData("[grep]\ndrop = \"x\"\n", "drop")]
    [InlineData("[grep]\ndrop = [1]\n", "drop")]
    [InlineData("[grep]\ndedupe = \"yes\"\n", "dedupe")]
    [InlineData("[grep]\ngroup = \"nope\"\n", "group")]
    [InlineData("[grep]\nnope = 1\n", "nope")]
    [InlineData("title = \"x\"\n", "title")]
    public void Strict_rejects_syntax_types_and_unknown_fields(string body, string want)
    {
        Assert.NotNull(RuleParser.ParseStrict("[grep]\nmax_lines = 5\n", out _));
        Assert.NotNull(RuleParser.ParseStrict("", out _));
        Assert.Null(RuleParser.ParseStrict("[grep\nmax_lines = \n", out var syntax));
        Assert.Contains("line", syntax, Ord);
        Assert.Null(RuleParser.ParseStrict(body, out var error));
        Assert.Contains(want, error, Ord);
        Assert.NotNull(RuleParser.ParseStrict("[grep]\ndedupe = \"normalized\"\n", out _));
    }

    [Fact]
    public void Issues_in_names_every_malformed_file()
    {
        var fs = MemFs.Create();
        var dir = TempDir(fs, "rtok-rules-iss");
        var dropIns = fs.Path.Combine(dir, "rules.d");
        Write(fs, fs.Path.Combine(dropIns, "bad.toml"), "[grep]\nmax_lines = \"many\"\n");
        Write(fs, fs.Path.Combine(dropIns, "good.toml"), "[echo]\nmax_lines = 2\n");
        var errors = RuleParser.IssuesIn(fs, fs.Path.Combine(dir, "no-such-rules.toml"), dropIns);
        var error = Assert.Single(errors);
        Assert.Contains("bad.toml", error, Ord);
        Assert.Contains("max_lines", error, Ord);
        // Missing file and dir report nothing.
        Assert.Empty(
            RuleParser.IssuesIn(
                fs,
                fs.Path.Combine(dir, "nope.toml"),
                fs.Path.Combine(dir, "nope-d")
            )
        );
    }

    [Fact]
    public void Issues_in_names_every_malformed_file_from_vfs()
    {
        var fs = MemFs.Create();
        Write(fs, "rules.d/bad.toml", "[grep]\nmax_lines = \"many\"\n");
        Write(fs, "rules.d/good.toml", "[echo]\nmax_lines = 2\n");
        var error = Assert.Single(RuleParser.IssuesIn(fs, "no-such-rules.toml", "rules.d"));
        Assert.Contains("bad.toml", error, Ord);
        Assert.Contains("max_lines", error, Ord);
        Assert.Empty(RuleParser.IssuesIn(fs, "nope.toml", "nope-d"));
    }

    [Fact]
    public void Issues_in_from_vfs_names_spaced_path()
    {
        var fs = MemFs.Create();
        Write(
            fs,
            "Users/Ivan Tuhai/.config/rtok/rules.d/bad.toml",
            "[grep]\nmax_lines = \"many\"\n"
        );
        var errors = RuleParser.IssuesIn(
            fs,
            "missing.toml",
            "Users/Ivan Tuhai/.config/rtok/rules.d"
        );
        var error = Assert.Single(errors);
        Assert.Contains("Ivan Tuhai", error, Ord);
        Assert.Contains("max_lines", error, Ord);
    }

    [Fact]
    public void Parse_strict_reads_group_dir_and_diag()
    {
        Assert.Equal(
            Group.Dir,
            RuleParser.ParseStrict("[find]\ngroup = \"dir\"\n", out _)![0].Group
        );
        Assert.Equal(
            Group.Diag,
            RuleParser.ParseStrict("[tsc]\ngroup = \"diag\"\n", out _)![0].Group
        );
        Assert.Equal(Group.Off, RuleParser.ParseStrict("[grep]\nmax_lines = 5\n", out _)![0].Group);
    }

    [Fact]
    public void Parse_strict_reads_json_items_and_string()
    {
        var rule = RuleParser.ParseStrict("[gh]\njson_items = 5\njson_string = 40\n", out _)![0];
        Assert.Equal(5u, rule.JsonItems);
        Assert.Equal(40u, rule.JsonString);
        Assert.Equal(20u, RuleParser.ParseStrict("[gh]\nmax_lines = 5\n", out _)![0].JsonItems);
    }
}
