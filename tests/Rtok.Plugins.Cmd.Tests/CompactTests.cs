using System.Globalization;
using System.Text;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>compact.rs</c> tests: cargo, git status, git diff headers, ls -l.</summary>
public sealed class CompactTests
{
    private const string TestFail =
        "   Compiling c v0.1.0\n    Finished `test` profile [unoptimized + debuginfo] target(s) in 0.00s\n     Running unittests src/lib.rs (target/debug/deps/c-1)\n\nrunning 3 tests\ntest a::ok ... ok\ntest a::bad ... FAILED\ntest a::ok2 ... ok\n\nfailures:\n\n---- a::bad stdout ----\n\nthread 'a::bad' panicked at src/lib.rs:9:9:\nassertion `left == right` failed: nope\n  left: 2\n right: 3\nnote: run with `RUST_BACKTRACE=1` environment variable to display a backtrace\n\n\nfailures:\n    a::bad\n\ntest result: FAILED. 2 passed; 1 failed; 0 ignored; 0 measured; 0 filtered out; finished in 0.00s\n";

    [Fact]
    public void Cargo_test_keeps_the_panic_body_and_one_totals_line() =>
        Assert.Equal(
            "thread 'a::bad' panicked at src/lib.rs:9:9:\nassertion `left == right` failed: nope\n  left: 2\n right: 3\nFAILED: a::bad\ncargo test: 2 passed, 1 failed (1 suite)",
            Compact.CargoTest(TestFail)
        );

    [Fact]
    public void Cargo_test_keeps_cargos_closing_verdict() =>
        Assert.EndsWith(
            "cargo test: 2 passed, 1 failed (1 suite)\nerror: test failed, to rerun pass `-p c --lib`",
            Compact.CargoTest(TestFail + "error: test failed, to rerun pass `-p c --lib`\n")
        );

    [Fact]
    public void Cargo_test_sums_every_suite() =>
        Assert.Equal(
            "cargo test: 2 passed, 1 ignored (2 suites)",
            Compact.CargoTest(
                "running 2 tests\ntest x ... ok\ntest y ... ok\n\ntest result: ok. 2 passed; 0 failed; 1 ignored; 0 measured; 0 filtered out; finished in 0.00s\n\n   Doc-tests c\n\nrunning 0 tests\n\ntest result: ok. 0 passed; 0 failed; 0 ignored; 0 measured; 0 filtered out; finished in 0.00s\n"
            )
        );

    [Fact]
    public void Cargo_test_compile_error_is_a_build_log() =>
        Assert.Equal(
            "error[E0308]: mismatched types\n --> src/lib.rs:3:5\nerror: could not compile `c` (lib test) due to 1 previous error",
            Compact.CargoTest(
                "   Compiling c v0.1.0\nerror[E0308]: mismatched types\n --> src/lib.rs:3:5\n  |\nerror: could not compile `c` (lib test) due to 1 previous error\n"
            )
        );

    [Fact]
    public void Cargo_build_success_is_one_word_and_warning_counts_go() =>
        Assert.Equal(
            "warning: unused variable: `x`\n --> src/lib.rs:1:5\nFinished dev",
            Compact.CargoBuild(
                "   Compiling c v0.1.0\nwarning: unused variable: `x`\n --> src/lib.rs:1:5\n  |\nwarning: `c` (lib) generated 1 warning\n    Finished `dev` profile [unoptimized + debuginfo] target(s) in 1.20s\n"
            )
        );

    // rustc prints the `#[warn(lint)]` note only on the first block of a lint, so the later
    // ones join it through their headline shape; every location survives (rtok T684).
    [Fact]
    public void Cargo_build_groups_warnings_by_lint_and_keeps_every_location() =>
        Assert.Equal(
            "unused_imports ×3: unused import: `a` (src/a.rs:1:5, src/b.rs:2:5, src/c.rs:3:5)\nwarning: redundant clone\n --> src/d.rs:4:1\nFinished dev",
            Compact.CargoBuild(
                "   Compiling c v0.1.0\n"
                    + "warning: unused import: `a`\n --> src/a.rs:1:5\n  |\n  = note: `#[warn(unused_imports)]` on by default\n\n"
                    + "warning: unused import: `b`\n --> src/b.rs:2:5\n  |\n\n"
                    + "warning: unused import: `c`\n --> src/c.rs:3:5\n  |\n\n"
                    + "warning: redundant clone\n --> src/d.rs:4:1\n  |\n  = help: for further information visit https://rust-lang.github.io/rust-clippy/master/index.html#redundant_clone\n\n"
                    + "warning: `c` (lib) generated 4 warnings\n    Finished `dev` profile [unoptimized + debuginfo] target(s) in 1.20s\n"
            )
        );

    [Fact]
    public void Cargo_build_groups_shape_twins_without_any_lint_note() =>
        Assert.Equal(
            "unused variable: `x` ×2 (src/a.rs:1:1, src/b.rs:2:2)\nerror[E0308]: mismatched types\n --> src/c.rs:3:3",
            Compact.CargoBuild(
                "warning: unused variable: `x`\n --> src/a.rs:1:1\nwarning: unused variable: `y`\n --> src/b.rs:2:2\nerror[E0308]: mismatched types\n --> src/c.rs:3:3\n"
            )
        );

    [Fact]
    public void Git_status_long_format_reads_as_porcelain() =>
        Assert.Equal(
            "On branch main\nA  a.rs\n M b.rs\n R c.rs -> d.rs\n?? e.txt dir/",
            Compact.GitStatus(
                "On branch main\nChanges to be committed:\n  (use \"git restore --staged <file>...\" to unstage)\n\tnew file:   a.rs\n\nChanges not staged for commit:\n  (use \"git add <file>...\" to update what will be committed)\n\tmodified:   b.rs\n\trenamed:    c.rs -> d.rs\n\nUntracked files:\n  (use \"git add <file>...\" to include in what will be committed)\n\te.txt\n\tdir/\n\nno changes added to commit (use \"git add\" and/or \"git commit -a\")\n"
            )
        );

    [Fact]
    public void Git_status_clean_stays_as_is()
    {
        const string clean = "On branch main\nnothing to commit, working tree clean\n";
        Assert.Equal(clean.TrimEnd(), Compact.GitStatus(clean));
    }

    [Fact]
    public void Git_status_unknown_entry_stands_down() =>
        Assert.Null(
            Compact.GitStatus("On branch main\nChanges not staged for commit:\n\tweird:   a\n")
        );

    [Fact]
    public void Git_diff_keeps_hunks_and_one_header_per_file() =>
        Assert.Equal(
            "+++ b/x\n@@ -1 +1 @@\n-a\n+b\nnew file mode 100644\n--- /dev/null\n+++ b/n\n@@ -0,0 +1 @@\n+c\ndiff --git a/bin b/bin\nBinary files a/bin and b/bin differ",
            Compact.GitDiff(
                "diff --git a/x b/x\nindex 1..2 100644\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-a\n+b\ndiff --git a/n b/n\nnew file mode 100644\nindex 0..3\n--- /dev/null\n+++ b/n\n@@ -0,0 +1 @@\n+c\ndiff --git a/bin b/bin\nindex 4..5 100644\nBinary files a/bin and b/bin differ\n"
            )
        );

    private static string LongStatus(string branch, string rest) =>
        $"On branch main\n{branch}\n\nChanges not staged for commit:\n  (use \"git add <file>...\" to update what will be committed)\n{rest}\n";

    [Fact]
    public void Git_status_groups_paths_by_directory_and_folds_the_branch_line() =>
        Assert.Equal(
            "On branch main ahead 3 (origin/main)\n M src/{a.rs,b.rs} docs/c.md top.toml\n D src/gone.rs",
            Compact.GitStatus(
                LongStatus(
                    "Your branch is ahead of 'origin/main' by 3 commits.\n  (use \"git push\" to publish your local commits)",
                    "\tmodified:   src/a.rs\n\tmodified:   src/b.rs\n\tmodified:   docs/c.md\n\tdeleted:    src/gone.rs\n\tmodified:   top.toml\n"
                )
            )
        );

    [Theory]
    [InlineData("Your branch is up to date with 'origin/main'.", "On branch main")]
    [InlineData(
        "Your branch is behind 'origin/main' by 2 commits, and can be fast-forwarded.\n  (use \"git pull\" to update your local branch)",
        "On branch main behind 2 (origin/main)"
    )]
    [InlineData(
        "Your branch and 'origin/main' have diverged,\nand have 2 and 5 different commits each, respectively.\n  (use \"git pull\" if you want to integrate the remote branch with yours)",
        "On branch main diverged +2 -5 (origin/main)"
    )]
    public void Git_status_branch_line_for_up_to_date_behind_and_diverged(
        string sentence,
        string branch
    ) =>
        Assert.Equal(
            $"{branch}\n M a.rs",
            Compact.GitStatus(LongStatus(sentence, "\tmodified:   a.rs\n"))
        );

    [Fact]
    public void Git_status_keeps_every_path_and_isolates_odd_names()
    {
        var got = Compact.GitStatus(
            "On branch main\nChanges to be committed:\n\trenamed:    src/old.rs -> src/new.rs\n\tnew file:   src/a b.rs\n\tnew file:   src/c,d.rs\n\tnew file:   src/e.rs\n\nUntracked files:\n\tbuild/\n\tx.log\n\ty.log\n"
        );
        Assert.NotNull(got);
        foreach (
            var path in new[]
            {
                "src/old.rs -> src/new.rs",
                "src/a b.rs",
                "src/c,d.rs",
                "src/e.rs",
                "build/",
                "x.log",
                "y.log",
            }
        )
        {
            Assert.Contains(path, got, StringComparison.Ordinal);
        }

        Assert.Contains("?? build/ x.log y.log", got, StringComparison.Ordinal);
        Assert.Contains("A  src/e.rs", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Ls_l_lists_dirs_then_files_with_sizes() =>
        Assert.Equal(
            "dirs: src/\nCargo.toml 3.3K\nrun me.sh* 120B\nlink -> src",
            Compact.LsL(
                "total 24\ndrwxr-xr-x  5 u g  160 Oct 14 23:50 .\ndrwxr-xr-x  9 u g  288 Oct 13 19:44 ..\ndrwxr-xr-x  2 u g   64 Oct 14 23:50 src\n-rw-r--r--  1 u g 3364 Oct 14 23:49 Cargo.toml\n-rwxr-xr-x  1 u g  120 Oct 14 23:49 run me.sh\nlrwxrwxrwx  1 u g    8 Oct 14 23:49 link -> src\n"
            )
        );

    [Fact]
    public void Ls_l_stands_down_on_other_shapes()
    {
        Assert.Null(Compact.LsL("a.txt\nb.txt\n"));
        Assert.True(Compact.LsLong(["ls", "-la"]));
        Assert.False(Compact.LsLong(["ls", "-a"]));
        Assert.False(Compact.LsLong(["ls", "--color"]));
    }

    private static string LongLine(string name, int size, string perms) =>
        string.Create(CultureInfo.InvariantCulture, $"{perms}  1 u g {size} Oct 14 23:50 {name}\n");

    [Fact]
    public void Ls_l_groups_by_extension_above_thirty_files_and_keeps_every_name()
    {
        const string rw = "-rw-r--r--";
        var out_ = new StringBuilder("total 99\n");
        for (var i = 0; i < 20; i++)
        {
            out_.Append(LongLine($"m{i}.rs", 1200 + i, rw));
        }

        for (var i = 0; i < 8; i++)
        {
            out_.Append(LongLine($"t{i}.toml", 300, rw));
        }

        out_.Append(LongLine("README.md", 2048, rw))
            .Append(LongLine("Makefile", 900, rw))
            .Append(LongLine("run.sh", 120, "-rwxr-xr-x"))
            .Append(LongLine("x.sh", 10, "-rwxr-xr-x"));
        var got = Compact.LsL(out_.ToString());
        Assert.NotNull(got);
        var lines = got.Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.Equal("Makefile 900B", lines[0]);
        Assert.StartsWith("*.rs (20): m0 1.2K, m1 1.2K", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("m19 1.2K", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("*.toml (8): t0 300B, t1 300B", lines[2], StringComparison.Ordinal);
        Assert.Equal("README.md 2.0K", lines[3]);
        Assert.Equal("*.sh (2): run* 120B, x* 10B", lines[4]);
    }

    [Fact]
    public void Ls_l_leaves_thirty_files_or_fewer_ungrouped()
    {
        var got = Compact.LsL(
            string.Concat(
                Enumerable.Range(0, 30).Select(i => LongLine($"f{i}.rs", 10, "-rw-r--r--"))
            )
        );
        Assert.NotNull(got);
        Assert.Equal(30, got.Split('\n').Length);
        Assert.StartsWith("f0.rs 10B\nf1.rs 10B", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Ls_la_dotfiles_share_one_line()
    {
        const string rw = "-rw-r--r--";
        Assert.Equal(
            "main.rs 4.9K\ndotfiles: .gitignore 40B, .env 12B, .editorconfig 300B, .npmrc 7B",
            Compact.LsL(
                LongLine(".gitignore", 40, rw)
                    + LongLine(".env", 12, rw)
                    + LongLine(".editorconfig", 300, rw)
                    + LongLine("main.rs", 5000, rw)
                    + LongLine(".npmrc", 7, rw)
            )
        );
        Assert.Equal(
            ".env 12B\n.npmrc 7B",
            Compact.LsL(LongLine(".env", 12, rw) + LongLine(".npmrc", 7, rw))
        );
    }

    // Rust's `{:.1}` rounds the exact quotient half to even: 1280 B is 1.25K, printed 1.2K.
    [Theory]
    [InlineData(1023, "1023B")]
    [InlineData(1280, "1.2K")]
    [InlineData(1331, "1.3K")]
    [InlineData(1536, "1.5K")]
    [InlineData(1048576, "1.0M")]
    public void Sizes_round_like_rust(int size, string want) =>
        Assert.Equal($"f {want}", Compact.LsL(LongLine("f", size, "-rw-r--r--")));
}
