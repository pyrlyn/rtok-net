using System.Globalization;
using System.Text;
using Rtok.Plugin.Sdk.Testing;
using Rtok.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>Each rule pass on a fixture, as the agent sees it.</summary>
public sealed class RulePassTests
{
    private static readonly CmdSettings Settings = CmdSettings.Load(
        MemFs.Create(),
        "/no-such-rules.toml",
        null,
        80
    );

    private static string Apply(IEnumerable<string> lines, Rule rule, int exit = 0) =>
        RuleEngine.Apply(Settings, string.Join('\n', lines), exit, rule, "a1b2c3d4");

    private static IEnumerable<string> Numbered(string prefix, int n) =>
        Enumerable.Range(0, n).Select(i => prefix + i.ToString(CultureInfo.InvariantCulture));

    [Fact]
    public Task Dedupe_folds_a_run_to_one_line_with_its_count() =>
        Verify(
            Apply(
                [
                    "   Compiling serde v1.0.200",
                    .. Enumerable.Repeat("warning: unused import: `std::fmt`", 6),
                    "   Compiling rtok v0.1.0",
                    .. Enumerable.Repeat("    Blocking waiting for file lock", 3),
                    "    Finished dev [unoptimized] target(s) in 4.2s",
                ],
                new Rule()
            )
        );

    [Fact]
    public Task Collapse_columns_folds_padding_but_not_indentation() =>
        Verify(
            Apply(
                [
                    "CONTAINER ID   IMAGE          COMMAND                  STATUS         NAMES",
                    "3f1c2a9b7d10   postgres:16    \"docker-entrypoint.s…\"   Up 2 hours     db",
                    "a8e44c01b2f3   redis:7        \"redis-server\"           Up 2 hours     cache",
                    "    indented   stays          indented",
                ],
                new Rule()
            )
        );

    [Fact]
    public Task Group_dir_lists_each_directory_once() =>
        Verify(
            Apply(
                [
                    .. Numbered("src/plugins/cmd/file", 40).Select(p => p + ".cs"),
                    .. Numbered("tests/cmd/case", 20).Select(p => p + ".cs"),
                    "README.md",
                ],
                new Rule { Group = Group.Dir, CollapseColumns = false }
            )
        );

    [Fact]
    public Task Group_diag_folds_one_message_across_files() =>
        Verify(
            Apply(
                [
                    .. Enumerable
                        .Range(0, 6)
                        .Select(i =>
                            F(
                                "src/f{0}.ts({1},5): error TS2322: Type 'string' is not assignable to type 'number'.",
                                i,
                                i * 10
                            )
                        ),
                    "src/lib.ts(3,1): error TS1005: ';' expected.",
                    .. Enumerable
                        .Range(0, 3)
                        .Select(i => F("src/g{0}.py:{0}:8: F401 `os` imported but unused", i)),
                ],
                new Rule { Group = Group.Diag, CollapseColumns = false }
            )
        );

    [Fact]
    public Task Built_in_keep_patterns_survive_the_cut() =>
        Verify(
            Apply(
                [
                    .. Numbered("ok step ", 40),
                    "error[E0308]: mismatched types",
                    .. Numbered("ok step ", 30),
                    "warning: deprecated",
                    "thread 'main' panicked at src/main.rs:4:5",
                    "test cache::evicts ... FAILED",
                    "level=warn msg=\"slow query\"",
                    .. Numbered("ok tail ", 30),
                ],
                new Rule
                {
                    MaxLines = 12,
                    Head = 3,
                    Tail = 3,
                }
            )
        );

    [Fact]
    public Task A_failed_run_keeps_its_last_80_lines_untouched() =>
        Verify(
            Apply(
                [
                    .. Numbered("   padded    line ", 99),
                    .. Enumerable.Repeat("same", 3),
                    "error: test failed, to rerun pass `--lib`",
                ],
                new Rule(),
                exit: 101
            )
        );

    [Fact]
    public Task A_stack_trace_is_never_cut() =>
        Verify(
            Apply(
                [
                    .. Numbered("collecting ", 30),
                    "Traceback (most recent call last):",
                    "  File \"/app/main.py\", line 12, in <module>",
                    "    run()",
                    "  File \"/app/main.py\", line 8, in run",
                    "    step()",
                    "  File \"/app/steps.py\", line 3, in step",
                    "    raise ValueError(\"bad input\")",
                    "ValueError: bad input",
                    .. Numbered("cleanup ", 30),
                ],
                new Rule
                {
                    MaxLines = 10,
                    Head = 2,
                    Tail = 2,
                }
            )
        );

    // A dropped line has no marker of its own: the expand trailer counts every original line.
    [Fact]
    public async Task Dropped_lines_are_counted_in_the_trailer()
    {
        var fs = MemFs.Create();
        fs.File.WriteAllText(
            "/rules.toml",
            "[noisy]\nmax_lines = 10\nhead = 4\ntail = 4\ndrop = [\"debug:\"]\n"
        );
        var emitter = new Emitter(
            CmdSettings.Load(fs, "/rules.toml", null, 80),
            trailer: true,
            trailerMinLines: 40
        );
        var body = Enumerable
            .Range(0, 30)
            .SelectMany(i => (string[])[F("step {0}", i), F("debug: heartbeat {0}", i)]);
        using var output = new MemoryStream();
        emitter.Emit(
            new MemoryHost(),
            ["noisy"],
            Encoding.UTF8.GetBytes(string.Join('\n', body)),
            0,
            null,
            output
        );
        await Verify(Encoding.UTF8.GetString(output.ToArray()));
    }

    private static string F(string format, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
