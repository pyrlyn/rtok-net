using System.Diagnostics;
using System.Globalization;
using Rtok.Core;
using Rtok.Plugin.Sdk;
using Rtok.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>The rule engine: the cut, dedupe, traces, columns, JSON, grouping and logfmt (rtok's rules.rs tests).</summary>
public sealed class RuleEngineTests
{
    private const StringComparison Ord = StringComparison.Ordinal;

    private static CmdSettings Settings(uint failTailLines) =>
        CmdSettings.Load(MemFs.Create(), "/no-such-rules.toml", null, failTailLines);

    private static string Apply(string body, Rule rule, string id) =>
        RuleEngine.Apply(Settings(80), body, 0, rule, id);

    private static string F(string format, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);

    private static List<string> Numbered(string prefix, int from, int to) =>
        [
            .. Enumerable
                .Range(from, to - from)
                .Select(i => prefix + i.ToString(CultureInfo.InvariantCulture)),
        ];

    private static string Body(string prefix, int n) => string.Join('\n', Numbered(prefix, 0, n));

    private static int Count(string s) => Text.Lines(s).Count;

    private static int Occurrences(string s, string needle) => s.Split(needle).Length - 1;

    // Sum of every `… N lines omitted` trailer.
    private static int OmittedSum(string output) =>
        Text.Lines(output)
            .Select(l => Text.StripPrefix(l, "… "))
            .Sum(l =>
                l is not null
                && int.TryParse(
                    l.Split(' ')[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var n
                )
                    ? n
                    : 0
            );

    private static Rule Uncut(Group group) =>
        new()
        {
            MaxLines = 40,
            Head = 10,
            Tail = 10,
            Dedupe = Dedupe.Off,
            CollapseColumns = false,
            Group = group,
        };

    [Fact]
    public void Three_hundred_ok_keeps_error_under_20()
    {
        var body = Numbered("ok ", 0, 300);
        body.Add("error: boom");
        var rule = new Rule
        {
            MaxLines = 20,
            Head = 5,
            Tail = 5,
        };
        var output = Apply(string.Join('\n', body), rule, "abc");
        Assert.True(Count(output) <= 20, output);
        Assert.Contains("error: boom", output, Ord);
    }

    [Theory]
    [InlineData(80u, 80, "line 20\n", "line 99")]
    [InlineData(3u, 3, "line 97\n", "line 99")]
    public void Exit_nonzero_fail_tail_lines(
        uint failTailLines,
        int expectCount,
        string expectStart,
        string expectEnd
    )
    {
        var output = RuleEngine.Apply(
            Settings(failTailLines),
            Body("line ", 100),
            3,
            new Rule(),
            "id"
        );
        Assert.Equal(expectCount, Count(output));
        Assert.StartsWith(expectStart, output, Ord);
        Assert.EndsWith(expectEnd, output, Ord);
        Assert.DoesNotContain("omitted", output, Ord);
    }

    [Theory]
    [InlineData(10, 3u, 3u, 4u, 7)]
    public void Max_cap_reports_true_omitted_count(
        int nLines,
        uint head,
        uint tail,
        uint maxLines,
        int expectOmitted
    )
    {
        var rule = new Rule
        {
            MaxLines = maxLines,
            Head = head,
            Tail = tail,
            Dedupe = Dedupe.Off,
        };
        var output = Apply(Body("line ", nLines), rule, "arc");
        Assert.Contains(F("{0} lines omitted", expectOmitted), output, Ord);
        var content = Text.Lines(output).Count(l => !l.Contains("lines omitted", Ord));
        Assert.Equal(expectOmitted, nLines - content);
    }

    // A trace block behind a spent budget must not count a dropped line twice.
    [Theory]
    [InlineData(4u, 3u, 1u)]
    [InlineData(5u, 2u, 2u)]
    [InlineData(3u, 1u, 1u)]
    [InlineData(6u, 4u, 1u)]
    public void Omitted_counts_add_up_with_a_trace_past_the_budget(
        uint maxLines,
        uint head,
        uint tail
    )
    {
        var lines = Numbered("a", 0, 5);
        lines.Add("error: boom");
        lines.AddRange([
            "Traceback (most recent call last):",
            "  File \"x\", line 1",
            "    foo()",
            "ValueError: x",
        ]);
        lines.AddRange(Numbered("z", 0, 3));
        var rule = new Rule
        {
            MaxLines = maxLines,
            Head = head,
            Tail = tail,
            Dedupe = Dedupe.Off,
        };
        var output = Apply(string.Join('\n', lines), rule, "arc");
        var shown = Text.Lines(output).Count(l => lines.Contains(l, StringComparer.Ordinal));
        Assert.True(shown + OmittedSum(output) == lines.Count, output);
    }

    // An exit-0 body of only stacks is still cut, and the counts add up.
    [Fact]
    public void Trace_lines_past_the_cap_go_through_the_budget()
    {
        var lines = Enumerable
            .Range(0, 2000)
            .SelectMany(i =>
                new[]
                {
                    F("Exception in thread \"t{0}\" java.lang.IllegalStateException", i),
                    F("\tat Svc.run{0}(Svc.java:1)", i),
                    F("\tat Svc.main{0}(Svc.java:2)", i),
                }
            )
            .ToList();
        var rule = new Rule
        {
            MaxLines = 50,
            Head = 10,
            Tail = 10,
            Dedupe = Dedupe.Off,
        };
        var output = Apply(string.Join('\n', lines), rule, "arc");
        var shown = Text.Lines(output).Count(l => !l.StartsWith("… ", Ord));
        Assert.True(
            shown <= Traces.MaxLines + (2 * (int)rule.MaxLines),
            F("{0} lines kept", shown)
        );
        Assert.Contains("lines omitted (expand arc)", output, Ord);
        Assert.Equal(lines.Count, shown + OmittedSum(output));
        // The first trace block is still whole; the last one is cut.
        Assert.Contains("Svc.main0(Svc.java:2)", output, Ord);
        Assert.DoesNotContain("Svc.run1900(", output, Ord);
    }

    [Fact]
    public void Cap_trace_lines_demotes_trace_lines_past_the_cap()
    {
        var kept = Enumerable.Repeat(true, Traces.MaxLines + 10).ToList();
        kept.Insert(5, false);
        var array = kept.ToArray();
        Traces.Cap(array);
        Assert.Equal(Traces.MaxLines, array.Count(k => k));
    }

    // No archive id: the marker names no expand target.
    [Fact]
    public void Omitted_marker_without_an_archive_id_has_no_empty_expand()
    {
        var output = Apply(Body("line ", 200), new Rule(), "");
        Assert.Contains("lines omitted", output, Ord);
        Assert.DoesNotContain("(expand )", output, Ord);
    }

    [Fact]
    public void Duration_clock_and_s_suffix_do_not_merge_distinct_lines()
    {
        Assert.Equal(Dedupes.NormalizeKey("took 1:2:3"), Dedupes.NormalizeKey("took 9:8:7"));
        Assert.False(
            string.Equals(
                Dedupes.NormalizeKey("copied 1src/a.rs"),
                Dedupes.NormalizeKey("copied 2src/a.rs"),
                Ord
            )
        );
        Assert.Equal(Dedupes.NormalizeKey("wait 12ms"), Dedupes.NormalizeKey("wait 3ms"));
    }

    [Fact]
    public void Normalize_line_key_keeps_distinct_error_codes()
    {
        Assert.False(
            string.Equals(
                Dedupes.NormalizeKey("ERR-404 file missing"),
                Dedupes.NormalizeKey("ERR-500 file missing"),
                Ord
            )
        );
        Assert.Equal(
            Dedupes.NormalizeKey("req id=abcd1234abcd1234 done"),
            Dedupes.NormalizeKey("req id=feed9999aaaa1111 done")
        );
    }

    [Fact]
    public void Normalized_dedupe_folds_non_adjacent_matches()
    {
        var output = Dedupes.Normalized([
            "2026-09-18T10:00:01 warn: slow",
            "other",
            "2026-09-18T10:00:02 warn: slow",
        ]);
        Assert.Equal(2, output.Count);
        Assert.Contains("(×2, also lines 3)", output[0], Ord);
    }

    // Placeholder is private in C#: checked through NormalizeKey, which must leave it as is.
    [Fact]
    public void Uuid_at_multibyte_suffix_is_no_match()
    {
        Assert.Equal("1234567é-x", Dedupes.NormalizeKey("1234567é-x"));
    }

    [Fact]
    public void Normalized_dedupe_survives_multibyte_lines()
    {
        var output = Dedupes.Normalized([
            "日本語テスト 1234567é-x done",
            "日本語テスト 1234567é-x done",
            "café 2026-09-18T10:00:01 warn: slow",
            "café 2026-09-18T10:00:02 warn: slow",
            "plain line",
        ]);
        Assert.True(output.Count == 3, string.Join('\n', output));
        Assert.Contains("(×2, also lines 2)", output[0], Ord);
        Assert.Contains("(×2, also lines 4)", output[1], Ord);
    }

    [Fact]
    public void Normalized_dedupe_beats_plain_on_three_k_log_fixture()
    {
        var lines = new List<string>(3000);
        for (var i = 0; i < 1000; i++)
        {
            lines.Add(F("2026-09-18T12:{0:D2}:00Z 127.0.0.1 GET /x 200", i % 60));
            if (i % 2 == 0)
            {
                lines.Add("separator");
            }
        }

        for (var i = 0; i < 1000; i++)
        {
            if (i % 2 == 1)
            {
                lines.Add("separator");
            }

            lines.Add("warning: unused variable `x`");
        }

        var body = string.Join('\n', lines);
        var plain = Apply(body, new Rule { MaxLines = 10_000, Dedupe = Dedupe.Adjacent }, "arc");
        var norm = Apply(body, new Rule { MaxLines = 10_000, Dedupe = Dedupe.Normalized }, "arc");
        Assert.True(
            Text.Bytes(norm) < Text.Bytes(plain),
            F("plain={0} norm={1}", plain.Length, norm.Length)
        );
    }

    // Padding folds inside a line; indentation and tabs stay; a trailing run folds away.
    [Fact]
    public void Collapse_columns_folds_padding_keeps_indent_and_tabs()
    {
        Assert.Equal("NAME STATUS AGE", RuleEngine.CollapseColumns("NAME   STATUS      AGE"));
        Assert.Equal(
            "  indented four spaces",
            RuleEngine.CollapseColumns("  indented    four  spaces")
        );
        Assert.Equal("trailing", RuleEngine.CollapseColumns("trailing   "));
        Assert.Equal("\tgo frame\ttab", RuleEngine.CollapseColumns("\tgo frame\ttab"));
        Assert.Equal("single already", RuleEngine.CollapseColumns("single already"));
    }

    // Columnar output shrinks; beside a traceback it keeps its padding.
    [Fact]
    public void Collapse_shrinks_columnar_output_and_stands_down_for_traces()
    {
        var rows = string.Concat(
            Enumerable
                .Range(0, 30)
                .Select(i => F("pod-{0:D4}     Running     0         3d2h  10.0.0.{0}\n", i))
        );
        var on = new Rule();
        var plain = Apply(rows, new Rule { CollapseColumns = false }, "arc");
        var collapsed = Apply(rows, on, "arc");
        Assert.True(
            Text.Bytes(collapsed) * 2 < Text.Bytes(plain) * 3,
            F("{0} vs {1}", collapsed.Length, plain.Length)
        );
        var withTrace =
            "NAME    STATUS    AGE\n"
            + rows
            + "Traceback (most recent call last):\n  File \"m.py\", line 1\n";
        Assert.Contains("NAME    STATUS    AGE", Apply(withTrace, on, "arc"), Ord);
    }

    // Python: header + indented frames; the bare exception line is a builtin keep.
    private static readonly string[] PythonTrace =
    [
        "Traceback (most recent call last):",
        "  File \"m.py\", line 3, in main",
        "    run()",
        "ValueError: boom",
    ];

    // Rust: header, message, and the backtrace frames.
    private static readonly string[] RustTrace =
    [
        "thread 'main' panicked at src/m.rs:2:5:",
        "explicit panic",
        "stack backtrace:",
        "   0: rtok::main",
        "   1: core::ops::function::FnOnce::call_once",
    ];

    // JS: an Error line and its `    at ` frames.
    private static readonly string[] JsTrace =
    [
        "Error: cannot read properties of undefined",
        "    at main (/x/app.js:10:13)",
        "    at Object.<anonymous> (/x/app.js:3:1)",
    ];

    // Go: the goroutine header and its frames.
    private static readonly string[] GoTrace =
    [
        "goroutine 1 [running]:",
        "main.main()",
        "\t/src/m.go:9 +0x2c",
    ];

    // Java: the exception header and its tab frames.
    private static readonly string[] JavaTrace =
    [
        "Exception in thread \"main\" java.lang.NullPointerException",
        "\tat Main.run(Main.java:8)",
        "\tat Main.main(Main.java:4)",
    ];

    public static TheoryData<string[]> TraceFixtures =>
        new() { PythonTrace, RustTrace, JsTrace, GoTrace, JavaTrace };

    // A mid-output trace survives the default cut whole; an ordinary middle line is cut.
    [Theory]
    [MemberData(nameof(TraceFixtures))]
    public void Stack_traces_survive_the_head_tail_cut_per_language(string[] trace)
    {
        var lines = Numbered("ok ", 0, 15);
        lines.AddRange(trace);
        lines.AddRange(Numbered("ok ", 15, 45));
        var output = Apply(string.Join('\n', lines), new Rule(), "arc1");
        foreach (var frame in trace)
        {
            Assert.Contains(frame, output, Ord);
        }

        Assert.Contains("lines omitted (expand arc1)", output, Ord);
        Assert.DoesNotContain("ok 30", output, Ord);
    }

    private static string JsonItem(int i)
    {
        var n = i.ToString(CultureInfo.InvariantCulture);
        return "{\"id\":"
            + n
            + ",\"name\":\"n"
            + n
            + "\",\"empty\":\"\",\"nil\":null,\"tags\":[],\"meta\":{}}";
    }

    [Fact]
    public void Json_compaction_drops_empties_keeps_keys_and_caps_arrays()
    {
        var items = string.Join(',', Enumerable.Range(0, 25).Select(JsonItem));
        var body = "{\"kind\":\"List\",\"unused\":null,\"items\":[" + items + "]}";
        var output = Apply(body, new Rule(), "arc");
        Assert.Contains("kind: \"List\"", output, Ord);
        Assert.Contains("items:", output, Ord);
        Assert.DoesNotContain("unused", output, Ord);
        Assert.DoesNotContain("empty", output, Ord);
        Assert.DoesNotContain("nil", output, Ord);
        Assert.Contains("… +5 more", output, Ord);
        Assert.Contains("n0", output, Ord);
        Assert.DoesNotContain("n24", output, Ord);
        var cut = Apply("{\"blob\":\"" + new string('x', 250) + "\"}", new Rule(), "arc");
        Assert.Contains("(250)", cut, Ord);
        Assert.Contains("…", cut, Ord);
    }

    [Fact]
    public void Json_mixed_array_keeps_the_per_element_lines()
    {
        var output = Apply(
            "[{\"id\":1,\"a\":\"x\",\"b\":2},3,\"s\",{\"id\":2,\"a\":\"y\",\"b\":4}]",
            new Rule(),
            "arc"
        );
        Assert.Contains("a: \"x\"", output, Ord);
        Assert.DoesNotContain("]{", output, Ord);
    }

    [Fact]
    public void Json_unparseable_body_is_untouched()
    {
        const string body = "not json\n{\n  broken\n";
        var rule = new Rule
        {
            MaxLines = 80,
            CollapseColumns = false,
            Dedupe = Dedupe.Off,
        };
        Assert.Equal(body.TrimEnd(), Apply(body, rule, "arc"));
    }

    [Fact]
    public void Single_line_json_with_many_items_does_not_panic()
    {
        var body = "[" + string.Join(", ", Enumerable.Range(0, 30)) + "]";
        var rule = new Rule
        {
            MaxLines = 5,
            Head = 2,
            Tail = 2,
        };
        Assert.Contains("… +10 more", Apply(body, rule, "id"), Ord);
    }

    [Fact]
    public void Group_dir_beats_the_same_rule_without_group()
    {
        var body = string.Join(
            '\n',
            Enumerable
                .Range(0, 30)
                .SelectMany(i => new[] { F("src/a{0}.rs", i), F("tests/t{0}.rs", i) })
        );
        var off = Apply(body, Uncut(Group.Off), "id");
        var on = Apply(body, Uncut(Group.Dir), "id");
        Assert.True(on.Length < off.Length, on);
        Assert.Contains("src/ (30 files): a0.rs, a1.rs, a2.rs …", on, Ord);
        Assert.Contains("tests/ (30 files): t0.rs, t1.rs, t2.rs …", on, Ord);
        Assert.DoesNotContain("lines omitted", on, Ord);
    }

    // A short find keeps every dir/name; only a longer list folds by directory.
    [Fact]
    public void Group_dir_keeps_full_paths_up_to_fifty_hits()
    {
        static List<string> Paths(int n) =>
            Numbered("./src/deep/dir/a", 0, n).ConvertAll(p => p + ".rs");

        Assert.Equal(Paths(50), Grouping.Dir(Paths(50)));
        var mixed = Paths(49);
        mixed.Add("find: ./x: Permission denied");
        mixed.Add("find: ./y: Permission denied");
        Assert.Equal(mixed, Grouping.Dir([.. mixed]));
        var folded = Grouping.Dir(Paths(51));
        Assert.True(folded.Count == 1, string.Join('\n', folded));
        Assert.Contains("(51 files)", folded[0], Ord);
    }

    // `find .` over a big tree: grouping must stay linear.
    [Fact]
    public void Group_dir_scales_to_a_find_over_a_large_tree()
    {
        const int dirs = 20_000;
        var body = string.Join(
            '\n',
            Enumerable.Range(0, 200_000).Select(i => F("./d{0}/sub/f{1}.rs", i % dirs, i))
        );
        var clock = Stopwatch.StartNew();
        var output = Apply(body, Uncut(Group.Dir), "id");
        var ms = clock.ElapsedMilliseconds;
#if DEBUG
        const long budget = 5000;
#else
        const long budget = 1000;
#endif
        Assert.True(ms < budget, F("{0} ms", ms));
        Assert.Contains("d0/sub/ (10 files): f0.rs, f20000.rs, f40000.rs …", output, Ord);
    }

    [Fact]
    public void Group_diag_beats_the_same_rule_without_group()
    {
        var lines = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            lines.Add(
                F(
                    "src/f{0}.ts({0},1): error TS2322: Type 'string' is not assignable to type 'number'.",
                    i
                )
            );
            lines.Add("error[E0308]: mismatched types");
            lines.Add(F("  {0}:1  error  Unexpected var  no-var", i));
        }

        var body = string.Join('\n', lines);
        var off = Apply(body, Uncut(Group.Off), "id");
        var on = Apply(body, Uncut(Group.Diag), "id");
        Assert.True(on.Length < off.Length, on);
        Assert.Contains(
            "TS2322 ×20: Type 'string' is not assignable to type 'number'. (src/f0.ts:0, src/f1.ts:1, src/f2.ts:2, …)",
            on,
            Ord
        );
        Assert.Contains("E0308 ×20: mismatched types", on, Ord);
        Assert.Contains("no-var ×20: Unexpected var (0:1, 1:1, 2:1, …)", on, Ord);
    }

    [Fact]
    public void Group_diag_reads_ruff_mypy_and_golangci_lines()
    {
        var lines = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            lines.Add(F("src/f{0}.py:{0}:8: F401 [*] `os` imported but unused", i));
            lines.Add(F("src/g{0}.py:{0}: error: Argument 1 has incompatible type  [arg-type]", i));
            lines.Add(F("pkg/h{0}.go:{0}:2: Error return value is not checked (errcheck)", i));
            lines.Add(
                F(
                    "app/m{0}.rb:{0}:4: C: [Correctable] Style/StringLiterals: Prefer single quotes.",
                    i
                )
            );
        }

        var body = string.Join('\n', lines);
        var off = Apply(body, Uncut(Group.Off), "id");
        var on = Apply(body, Uncut(Group.Diag), "id");
        Assert.True(on.Length < off.Length, on);
        Assert.Contains("F401 ×20: `os` imported but unused (src/f0.py:0:8", on, Ord);
        Assert.Contains("arg-type ×20: Argument 1 has incompatible type (src/g0.py:0", on, Ord);
        Assert.Contains("errcheck ×20: Error return value is not checked (pkg/h0.go:0:2", on, Ord);
        Assert.Contains("Style/StringLiterals ×20: Prefer single quotes. (app/m0.rb:0:4", on, Ord);
        // A `key: value` line and a bare path:line are not diagnostics (ParseLocated is private).
        Assert.Null(Diagnostics.Parse("note: see above"));
        Assert.Null(Diagnostics.Parse("src/a.py:3: just text"));
    }

    // Apply groups once, so grouped lines must be fixed points.
    [Fact]
    public void Grouping_is_a_fixed_point_on_its_own_output()
    {
        var paths = Enumerable
            .Range(0, 30)
            .SelectMany(i => new[] { F("./src/a{0}.rs", i), F("tests/t{0}.rs", i), "x.md" })
            .ToList();
        var once = Grouping.Dir(paths);
        Assert.Equal(once, Grouping.Dir([.. once]));
        var diags = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            diags.Add(F("src/f{0}.ts({0},1): error TS2322: Type 'a' is not 'b'.", i));
            diags.Add("error[E0308]: mismatched types");
            diags.Add(F("  {0}:1  error  Unexpected var  no-var", i));
            diags.Add(F("FAILED t{0}.py::t - ValueError: bad {0}", i));
            diags.Add("E   KeyError: 'k'");
            diags.Add(F("plain line {0}", i));
        }

        var diagOnce = Grouping.Diag(diags);
        Assert.Equal(diagOnce, Grouping.Diag([.. diagOnce]));
    }

    [Fact]
    public void Group_diag_line_stays_pinned_through_the_cut()
    {
        var lines = Numbered("ok ", 0, 15);
        lines.Add(
            "src/app.ts(10,5): error TS2322: Type 'string' is not assignable to type 'number'."
        );
        lines.AddRange(Numbered("ok ", 15, 45));
        var output = Apply(string.Join('\n', lines), Uncut(Group.Diag), "arc");
        Assert.Contains("TS2322 ×1:", output, Ord);
        Assert.Contains("lines omitted (expand arc)", output, Ord);
    }

    // 300 logfmt lines (one ERROR, one WARN) cut to head/tail.
    private static string LogfmtBody() =>
        string.Join(
            '\n',
            Enumerable
                .Range(0, 300)
                .Select(i =>
                    F(
                        "ts=2026-10-16T10:{0:D2}:{1:D2}Z level={2} svc=api msg=\"request done\" method=GET status=200 dur_ms={3} req=r{4}",
                        i / 60,
                        i % 60,
                        i switch
                        {
                            150 => "error",
                            200 => "warn",
                            _ => "info",
                        },
                        10 + (i % 7),
                        i
                    )
                )
        );

    [Fact]
    public void Collapsed_logfmt_run_lists_constants_once_and_varying_keys()
    {
        var body = LogfmtBody();
        var output = Apply(body, new Rule(), "deadbeef");
        var first = Text.Lines(output)[0];
        Assert.StartsWith("[every record:", first, Ord);
        foreach (
            var want in new[] { "svc=api", "status=200", "method=GET", "msg=\"request done\"" }
        )
        {
            Assert.Contains(want, first, Ord);
        }

        // The shared pairs are stated once: header + error + warn.
        Assert.True(Occurrences(output, "svc=api") == 3, output);
        var marker = Text.Lines(output).Find(l => l.Contains("lines omitted", Ord));
        Assert.NotNull(marker);
        Assert.Contains("(expand deadbeef)", marker, Ord);
        foreach (var want in new[] { "varying:", "ts=", "dur_ms=", "req=r10..r149" })
        {
            Assert.Contains(want, marker, Ord);
        }

        // The error and WARN lines survive verbatim, with the head and tail.
        var bodyLines = Text.Lines(body);
        var outLines = Text.Lines(output);
        Assert.Contains(outLines, l => string.Equals(l, bodyLines[150], Ord));
        Assert.Contains(outLines, l => string.Equals(l, bodyLines[200], Ord));
        Assert.Contains("req=r0", output, Ord);
        Assert.Contains("req=r299", output, Ord);
        static int Tokens(string s) =>
            TokenEstimator.Estimate(s, TextClass.Code, EstimatorRates.Default);
        Assert.True(Tokens(output) <= Tokens(body), F("{0} > {1}", Tokens(output), Tokens(body)));
    }

    [Fact]
    public void Short_or_unstructured_omitted_runs_get_no_field_summary()
    {
        var output = Apply(Body("line ", 200), new Rule(), "id");
        Assert.Contains("… 180 lines omitted (expand id)", output, Ord);
        Assert.DoesNotContain("every record", output, Ord);
        Assert.DoesNotContain("varying:", output, Ord);
    }
}
