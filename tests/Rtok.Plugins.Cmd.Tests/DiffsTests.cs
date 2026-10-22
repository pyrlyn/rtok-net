using System.Globalization;
using System.Text;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>diffs.rs</c> tests: the two tiers of git diff / git show.</summary>
public sealed class DiffsTests
{
    private static string File(string name, string body) =>
        $"diff --git a/{name} b/{name}\nindex 1111111..2222222 100644\n--- a/{name}\n+++ b/{name}\n{body}";

    private static string Ctx(int n, string tag) =>
        string.Concat(Enumerable.Range(0, n).Select(i => $" {tag}{i}\n"));

    [Fact]
    public void Small_diff_keeps_changes_with_one_line_of_context() =>
        Assert.Equal(
            "+++ b/src/a.rs\n@@ -1,12 +1,12 @@ fn main\n ⋯ 4 unchanged\n a4\n-old\n+new\n b0\n ⋯ 5 unchanged",
            Diffs.Tiered(
                File(
                    "src/a.rs",
                    $"@@ -1,12 +1,12 @@ fn main\n{Ctx(5, "a")}-old\n+new\n{Ctx(6, "b")}"
                )
            )
        );

    [Fact]
    public void Interior_context_between_two_changes_folds_the_middle() =>
        Assert.Contains(
            " m0\n ⋯ 3 unchanged\n m4\n-p\n+q",
            Diffs.Tiered(File("f.rs", $"@@ -1,7 +1,7 @@\n-x\n+y\n{Ctx(5, "m")}-p\n+q\n")),
            StringComparison.Ordinal
        );

    // ` a`, `--- a/x` (a deleted line) and `+++ b/x` (an added one) are all hunk body.
    [Fact]
    public void A_deleted_line_that_looks_like_a_file_header_stays_a_hunk_line() =>
        Assert.Equal(
            "+++ b/f.txt\n@@ -1,2 +1,2 @@\n a\n--- a/x\n+++ b/x\n+++ b/g.txt\n@@ -1 +1 @@\n-p\n+q",
            Diffs.Tiered(
                File("f.txt", "@@ -1,2 +1,2 @@\n a\n--- a/x\n+++ b/x\n")
                    + File("g.txt", "@@ -1 +1 @@\n-p\n+q\n")
            )
        );

    internal static string Big()
    {
        var raw = new StringBuilder();
        for (var i = 0; i < 8; i++)
        {
            var body = new StringBuilder($"@@ -10,40 +10,40 @@ fn func_{i}\n").Append(Ctx(3, "c"));
            for (var k = 0; k < i * 2 + 1; k++)
            {
                body.Append(CultureInfo.InvariantCulture, $"-old{k}\n+new{k}\n");
            }

            body.Append(Ctx(40 - 3 - i * 2 - 1, "t"));
            raw.Append(File($"crates/m{i}/src/lib.rs", body.ToString()));
        }

        return raw.ToString();
    }

    [Fact]
    public void Big_diff_lists_every_path_with_counts_and_hunk_headers()
    {
        var raw = Big();
        Assert.True(Text.Lines(raw).Count > 120);
        var got = Diffs.Tiered(raw);
        Assert.NotNull(got);
        Assert.StartsWith("8 files, +", got, StringComparison.Ordinal);
        for (var i = 0; i < 8; i++)
        {
            Assert.Contains($"crates/m{i}/src/lib.rs +", got, StringComparison.Ordinal);
            Assert.Contains($"@@ -10,40 +10,40 @@ fn func_{i}", got, StringComparison.Ordinal);
        }

        // Only the smallest files keep their changed lines.
        Assert.Contains("-old0\n+new0", got, StringComparison.Ordinal);
        Assert.DoesNotContain("old13", got, StringComparison.Ordinal);
        Assert.True(Text.Bytes(got) * 3 < Text.Bytes(raw));
    }

    [Fact]
    public void New_deleted_renamed_and_binary_files_say_so()
    {
        var raw =
            "commit abc\nAuthor: a\n\n    msg\n\n"
            + "diff --git a/n.rs b/n.rs\nnew file mode 100644\nindex 0000000..1111111\n--- /dev/null\n+++ b/n.rs\n@@ -0,0 +1,2 @@\n+a\n+b\n"
            + "diff --git a/o.rs b/p.rs\nsimilarity index 90%\nrename from o.rs\nrename to p.rs\n"
            + "diff --git a/i.png b/i.png\nindex 1..2 100644\nBinary files a/i.png and b/i.png differ\n"
            + Big().Replace("crates/m", "z/m", StringComparison.Ordinal);
        var got = Diffs.Tiered(raw);
        Assert.NotNull(got);
        Assert.StartsWith("commit abc\nAuthor: a\n\n    msg\n\n", got, StringComparison.Ordinal);
        Assert.Contains("n.rs +2 -0 (new)", got, StringComparison.Ordinal);
        Assert.Contains("o.rs -> p.rs +0 -0", got, StringComparison.Ordinal);
        Assert.Contains("i.png +0 -0 (binary)", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Combined_or_malformed_diffs_stand_down()
    {
        Assert.Null(Diffs.Tiered("diff --cc f\n@@@ -1 -1 +1 @@@\n"));
        Assert.Null(Diffs.Tiered("nothing here\n"));
        Assert.Null(Diffs.Tiered(File("f", "@@ -1,2 +1,2 @@\n a\ngarbage\n")));
    }

    // A diff over 120 lines prints the per-file table and keeps 100 % of the paths.
    [Fact]
    public Task Big_diff_per_file_table() => Verify(Diffs.Tiered(Big()));
}
