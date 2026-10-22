using System.Globalization;
using System.Text.Json.Nodes;
using Rtok.Testing;
using static Rtok.Measure.Tests.Lines;

namespace Rtok.Measure.Tests;

public sealed class StatsTests
{
    private static JsonObject Input(params (string Key, JsonNode? Value)[] fields) => Obj(fields);

    [Fact]
    public void Ctt_and_tool_totals_on_a_mini_session()
    {
        using var dir = new TempDir();
        // Three turns: the result lands on turn 1 of 3, so ctt = tokens * 2; 13 bytes is 4 tokens.
        Raw(
            dir,
            "s.jsonl",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"Bash","input":{"command":"cd x && sed -n p"}}],"usage":{"input_tokens":10,"output_tokens":1}}}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"hello world!!"}]}}""",
            """{"type":"assistant","message":{"content":[{"type":"text","text":"done"}],"usage":{"input_tokens":20,"cache_read_input_tokens":80,"output_tokens":2}}}"""
        );
        // Twenty 1-byte Reads and one of 1000 bytes: nearest-rank p95 is the 20th of 21, so 1.
        var p = new List<JsonNode>();
        for (var i = 0; i < 20; i++)
        {
            p.Add(Use($"r{i}", "Read", Input(("file_path", "a"))));
            p.Add(Result($"r{i}", "x"));
        }
        p.Add(Use("big", "Read", Input(("file_path", "b"))));
        p.Add(Result("big", new string('y', 1000)));
        Write(dir, "p.jsonl", p);

        var r = Collect(dir);

        Assert.Equal(2, r.Sessions);
        var bash = r.Tools.Rows["Bash"];
        Assert.Equal((1L, 4L, 8L), (bash.Count, bash.EstTokens, bash.Ctt));
        Assert.Equal(1, r.BashFamilies.Rows["sed"].Count);
        Assert.Equal(80, r.UsageCacheRead);
        var read = r.Tools.Rows["Read"];
        Assert.Equal((21L, 1000L, 1L), (read.Count, read.Max, read.P95));
    }

    [Fact]
    public void P95_is_nearest_rank_not_max()
    {
        var rows = new SizeRows();
        for (var i = 0; i < 100; i++)
        {
            rows.Add("Read", 1, 1, 0);
        }
        rows.Add("Read", 1000, 250, 0);
        rows.Finish();
        var row = rows.Rows["Read"];
        Assert.Equal((1000L, 1L), (row.Max, row.P95));
    }

    [Fact]
    public void P95_survives_the_cap_without_rng()
    {
        var v = new List<long>();
        for (var i = 0; i < SizeRows.P95Cap + 100; i++)
        {
            SizeRows.PushCapped(v, i);
        }
        Assert.True(v.Count <= SizeRows.P95Cap);
        var once = SizeRows.NearestP95([.. v]);
        Assert.Equal(once, SizeRows.NearestP95([.. v]));
        Assert.True(once <= SizeRows.P95Cap + 100);
    }

    [Fact]
    public void Thinking_blocks_count_once_per_message()
    {
        using var dir = new TempDir();
        Raw(
            dir,
            "t.jsonl",
            """{"type":"assistant","message":{"id":"m1","content":[{"type":"thinking","thinking":"reason 123"}],"usage":{"input_tokens":10,"output_tokens":1}}}""",
            """{"type":"assistant","message":{"id":"m2","content":[{"type":"redacted_thinking","data":"x"}],"usage":{"input_tokens":11,"output_tokens":1}}}""",
            """{"type":"assistant","message":{"id":"m1","content":[{"type":"text","text":"answer"}],"usage":{"input_tokens":10,"output_tokens":1}}}"""
        );
        var r = Collect(dir);
        Assert.Equal((2L, 11L), (r.Thinking.Blocks, r.Thinking.Bytes));
    }

    [Fact]
    public void Dictionary_row_prints_shares_of_billed_input()
    {
        var r = new StatsReport();
        Assert.DoesNotContain("dictionary", StatsTable.Render(r), StringComparison.Ordinal);
        Assert.DoesNotContain("dictionary", StatsJson.Render(r), StringComparison.Ordinal);
        r.Dictionary.Requests = 2;
        r.Dictionary.InputBytes = 4000;
        r.Dictionary.RequestTokens = 2500;
        r.Dictionary.Paths.Repeated = 400;
        r.Dictionary.Paths.Saved = 100;
        var table = StatsTable.Render(r);
        Assert.Contains(
            "dictionary requests 2  content bytes 4000  billed input est. bytes 10000",
            table,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "paths repeated 400 (4.00% of billed)  cache-safe saving 100 (1.00% of billed, 2.50% of content)",
            table,
            StringComparison.Ordinal
        );
    }

    /// <summary>A PNG screenshot in a tool_result and a JPEG pasted into a prompt, both before
    /// the second of two requests: each is resident once.</summary>
    [Fact]
    public void Image_blocks_count_with_size_and_tokens()
    {
        using var dir = new TempDir();
        static JsonObject Img(string data, string mt) =>
            new()
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = mt,
                    ["data"] = data,
                },
            };
        var first = Use("s1", "screenshot", [], "a1");
        first["message"]!["usage"] = new JsonObject
        {
            ["input_tokens"] = 1000,
            ["output_tokens"] = 1,
        };
        var pasted = new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject
            {
                ["content"] = new JsonArray(
                    Img(Jpeg(200, 200), "image/jpeg"),
                    new JsonObject { ["type"] = "text", ["text"] = "look" }
                ),
            },
        };
        Write(
            dir,
            "t.jsonl",
            [
                first,
                Result("s1", new JsonArray(Img(Png(1920, 1080), "image/png"))),
                pasted,
                JsonNode.Parse(
                    """{"type":"assistant","message":{"id":"a2","content":[{"type":"text","text":"ok"}],"usage":{"input_tokens":9000,"output_tokens":1}}}"""
                )!,
            ]
        );
        var d = Collect(dir).Images;
        Assert.Equal((2L, 2691L + 64, 0L), (d.Blocks, d.Tokens, d.NoSize));
        Assert.Equal(2691 + 64, d.Resident);
        Assert.Equal(2691, d.BySource["screenshot"].Tokens);
        Assert.Equal(1, d.BySource["user"].Blocks);
        var table = StatsTable.Render(Collect(dir));
        Assert.Contains("images blocks 2", table, StringComparison.Ordinal);
        Assert.Contains("  images user  blocks 1", table, StringComparison.Ordinal);
    }

    /// <summary>Skill bodies ride as <c>isMeta</c> records keyed by <c>sourceToolUseID</c>;
    /// resident multiplies the body by the requests after it.</summary>
    [Fact]
    public void Skills_fold_injected_bodies_and_count_resident_requests()
    {
        using var dir = new TempDir();
        const string Tiny = "line one\nline two\nline three\n";
        var huge = string.Concat(Enumerable.Range(0, 3000).Select(i => $"body line {i}\n"));
        static string Meta(string id, string text) =>
            new JsonObject
            {
                ["type"] = "user",
                ["isMeta"] = true,
                ["sourceToolUseID"] = id,
                ["message"] = new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray(
                        new JsonObject { ["type"] = "text", ["text"] = text }
                    ),
                },
            }.ToJsonString();
        Raw(
            dir,
            "sk.jsonl",
            """{"type":"assistant","message":{"id":"m1","content":[{"type":"tool_use","id":"tu-s1","name":"Skill","input":{"skill":"tiny-skill","args":""}}]},"usage":{"input_tokens":10,"output_tokens":1}}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"tu-s1","content":"ideas.md"}]}}""",
            Meta("tu-s1", Tiny),
            """{"type":"assistant","message":{"id":"m2","content":[]},"usage":{"input_tokens":100,"output_tokens":2}}""",
            """{"type":"assistant","message":{"id":"m3","content":[{"type":"tool_use","id":"tu-s2","name":"Skill","input":{"skill":"huge-skill","args":""}}]},"usage":{"input_tokens":110,"output_tokens":2}}""",
            Meta("tu-s2", huge)
        );
        var r = Collect(dir);
        var skills = r.Skills!;
        var tiny = skills["tiny-skill"];
        Assert.Equal((1L, Tiny.Length, Tiny.Length), (tiny.Count, tiny.Bytes, tiny.Mean));
        Assert.Equal(tiny.Max, tiny.P95);
        Assert.Equal(Tiny.Length * 2, tiny.Resident); // the m2 and m3 requests carried it
        var big = skills["huge-skill"];
        Assert.Equal((1L, huge.Length, 0L), (big.Count, big.Bytes, big.Resident));
        Assert.Equal("ideas.md".Length, r.Tools.Rows["Skill"].TotalBytes);
        var table = StatsTable.Render(r);
        Assert.Contains("skill", table, StringComparison.Ordinal);
        Assert.Contains("resident", table, StringComparison.Ordinal);
        Assert.Contains("\"skills\"", StatsJson.Render(r), StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_row_sums_old_and_new_strings_over_edit_and_multiedit()
    {
        using var dir = new TempDir();
        var edit = Input(
            ("file_path", "/a.rs"),
            ("old_string", "0123456789"),
            ("new_string", "abc")
        );
        var multi = Input(
            ("file_path", "/b.rs"),
            (
                "edits",
                new JsonArray(
                    Input(("old_string", "abcd"), ("new_string", "x")),
                    Input(("old_string", "abcdef"), ("new_string", "xy"))
                )
            )
        );
        var bash = Input(("command", "ls"));
        var denom =
            edit.ToJsonString().Length + multi.ToJsonString().Length + bash.ToJsonString().Length;
        var lines = new[] { ("t1", "Edit", edit), ("t2", "MultiEdit", multi), ("t3", "Bash", bash) }
            .Select(c =>
            {
                var l = Use(c.Item1, c.Item2, c.Item3);
                l["message"]!["usage"] = new JsonObject
                {
                    ["input_tokens"] = 1,
                    ["output_tokens"] = 10,
                };
                return (JsonNode)l;
            })
            .ToList();
        Write(dir, "e.jsonl", lines);

        var r = Collect(dir);

        var e = r.Edits;
        Assert.Equal((3L, 20L, 6L), (e.Calls, e.OldBytes, e.NewBytes));
        Assert.Equal(denom, e.ToolInputBytes);
        var table = StatsTable.Render(r);
        Assert.Contains(
            "edit calls 3  old_string 20 B  new_string 6 B",
            table,
            StringComparison.Ordinal
        );
        Assert.Contains(
            string.Create(CultureInfo.InvariantCulture, $"old/tool_input {100.0 * 20 / denom:F1}%"),
            table,
            StringComparison.Ordinal
        );
        // 20 B is 5 estimated tokens of 30 output tokens.
        Assert.Contains("old/output_tokens (est) 16.7%", table, StringComparison.Ordinal);
    }

    /// <summary>Of five Reads only the two whole, large, outline-able ones count; the one an
    /// Edit follows needed its body.</summary>
    [Fact]
    public void Read_whole_counts_large_unranged_reads_of_outlined_files()
    {
        using var dir = new TempDir();
        var big = new string('x', 40_000);
        Write(
            dir,
            "w.jsonl",
            [
                Use("t1", "Read", Input(("file_path", "src/a.rs"))),
                Result("t1", big),
                Use("t2", "Read", Input(("file_path", "src/b.rs"))),
                Result("t2", big),
                Use(
                    "t3",
                    "Edit",
                    Input(("file_path", "src/b.rs"), ("old_string", "x"), ("new_string", "y"))
                ),
                Use("t4", "Read", Input(("file_path", "src/c.rs"), ("limit", 50))),
                Result("t4", big),
                Use("t5", "Read", Input(("file_path", "notes.txt"))),
                Result("t5", big),
                Use("t6", "Read", Input(("file_path", "src/small.rs"))),
                Result("t6", "fn main() {}"),
            ]
        );
        var signals = new StatsSignals
        {
            HasOutline = p => p.EndsWith(".rs", StringComparison.Ordinal),
        };

        var r = Collect(dir, signals: signals);

        var d = r.ReadWhole;
        Assert.Equal((2L, 80_000L, 1L, 40_000L), (d.Calls, d.Bytes, d.Edited, d.EditedBytes));
        Assert.Equal(160_012, d.ReadBytes);
        Assert.Contains(
            "read whole calls 2  bytes 80000  50.0% of Read  50.0% of results  not edited 1 bytes 25.0% of results  edited after 1",
            StatsTable.Render(r),
            StringComparison.Ordinal
        );
    }

    /// <summary>Denied Reads count with their reasons' size, classed by what followed within
    /// three calls: an rtok read of the path, a native Read of it, or a ToolSearch.</summary>
    [Fact]
    public void Read_deny_counts_denies_and_what_followed()
    {
        using var dir = new TempDir();
        const string Deny = "PreToolUse:Read hook error: DENIED use mcp__rtok__read";
        Write(
            dir,
            "deny.jsonl",
            [
                Use("t1", "Read", Input(("file_path", "src/a.rs"))),
                Result("t1", Deny),
                Use("t2", "mcp__rtok__read", Input(("path", "/proj/src/a.rs"))),
                Result("t2", "body"),
                Use("t3", "Read", Input(("file_path", "src/b.rs"))),
                Result("t3", Deny),
                Use("t4", "Read", Input(("file_path", "src/b.rs"), ("limit", 50))),
                Result("t4", "body"),
                Use("t5", "Read", Input(("file_path", "src/c.rs"))),
                Result("t5", Deny),
                Use("t6", "ToolSearch", Input(("query", "select:mcp__rtok__read"))),
                Use("t7", "Bash", Input(("command", "ls"))),
                Use("t8", "Bash", Input(("command", "ls"))),
                Use("t9", "mcp__rtok__read", Input(("path", "src/c.rs"))),
                Result("t9", "body"),
                Use("t10", "Read", Input(("file_path", "src/d.rs"))),
                Result("t10", "fine"),
            ]
        );
        var signals = new StatsSignals
        {
            IsReadDeny = c => c.Contains("DENIED", StringComparison.Ordinal),
        };

        var r = Collect(dir, signals: signals);

        var d = r.ReadDeny;
        var tokens = Calls.EstTokens(Deny.Length);
        Assert.Equal(
            (3L, 3 * tokens, 1L, 1L, 1L),
            (d.Calls, d.Tokens, d.ThenMcp, d.ThenNative, d.ThenToolSearch)
        );
        Assert.Contains(
            $"read deny calls 3  tokens {3 * tokens}  then rtok read 1  then native Read 1  then ToolSearch 1",
            StatsTable.Render(r),
            StringComparison.Ordinal
        );

        using var empty = new TempDir();
        Write(empty, "empty.jsonl", []);
        var none = Collect(empty, signals: signals);
        Assert.DoesNotContain("read deny", StatsTable.Render(none), StringComparison.Ordinal);
        Assert.DoesNotContain("read_deny", StatsJson.Render(none), StringComparison.Ordinal);
    }

    [Fact]
    public void Read_delta_counts_a_reread_after_an_edit_not_an_unchanged_one()
    {
        using var dir = new TempDir();
        Write(
            dir,
            "d.jsonl",
            [
                Use("t1", "Read", Input(("file_path", "src/a.rs"))),
                Result("t1", "0123456789"),
                Use(
                    "t2",
                    "Edit",
                    Input(("file_path", "/proj/src/a.rs"), ("old_string", "x"), ("new_string", "y"))
                ),
                Use("t3", "Read", Input(("file_path", "src/a.rs"))),
                Result("t3", "01234567890123456789"),
                Use("t4", "Read", Input(("file_path", "src/a.rs"))),
                Result("t4", "same"),
                Use("t5", "Read", Input(("file_path", "/b.rs"))),
                Result("t5", "bbbb"),
            ]
        );
        var r = Collect(dir);
        var d = r.ReadDelta;
        Assert.Equal((1L, 20L, 38L), (d.Calls, d.Bytes, d.ReadBytes));
        Assert.Contains(
            "read delta calls 1  bytes 20  of Read bytes 38  52.6%",
            StatsTable.Render(r),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Repeat_counts_identical_bodies_from_different_tools()
    {
        using var dir = new TempDir();
        Write(
            dir,
            "r.jsonl",
            [
                Use("t1", "Bash", Input(("command", "cat a"))),
                Result("t1", "01234567890123456789"),
                Use("t2", "Read", Input(("file_path", "a"))),
                Result("t2", "01234567890123456789"),
                Use("t3", "Bash", Input(("command", "head -1000 a"))),
                Result("t3", "different-output"),
            ]
        );
        var r = Collect(dir);
        var d = r.Repeat;
        Assert.Equal((1L, 20L, 56L), (d.Calls, d.Bytes, d.ResultBytes));
        Assert.Contains(
            "repeat calls 1  bytes 20  of result bytes 56  35.7%",
            StatsTable.Render(r),
            StringComparison.Ordinal
        );
    }

    /// <summary>One repeat native Read per class: session A carries fired, changed, ranged and
    /// declined; B (no marker anywhere) hook_absent; C and its sub-agent subagent.</summary>
    [Fact]
    public void Repeat_reads_classify_each_kind_once()
    {
        using var dir = new TempDir();
        static IEnumerable<JsonNode> Pair(string id, JsonObject input, string body) =>
            [Use(id, "Read", input), Result(id, body)];
        static JsonObject Path(string p) => Input(("file_path", p));
        const string FiredBody = "[archived aa11: 1 lines · 1 tokens · expand(aa11)]";
        Write(
            dir,
            "sessA.jsonl",
            [
                .. Pair("t1", Path("fA.rs"), "base"),
                .. Pair("t2", Path("fA.rs"), FiredBody),
                .. Pair("t3", Path("gA.rs"), "base-g"),
                Use("t4", "Edit", Path("gA.rs")),
                .. Pair("t5", Path("gA.rs"), "chg"),
                .. Pair("t6", Input(("file_path", "hA.rs"), ("limit", 100)), "bh"),
                .. Pair("t7", Input(("file_path", "hA.rs"), ("limit", 5)), "rng"),
                .. Pair("t8", Path("iA.rs"), "base-i"),
                .. Pair("t9", Path("iA.rs"), "dec"),
            ]
        );
        Write(
            dir,
            "sessB.jsonl",
            [.. Pair("u1", Path("kB.rs"), "base-k"), .. Pair("u2", Path("kB.rs"), "abs")]
        );
        Write(dir, "sessC.jsonl", [.. Pair("v1", Path("pC.rs"), "par"), Use("v2", "Agent", [])]);
        Write(dir, "sessC/subagents/agent-1.jsonl", Pair("w1", Path("pC.rs"), "sub"));
        dir.Write(
            "sessC/subagents/agent-1.meta.json",
            """{"agentType":"explore","model":"haiku","toolUseId":"v2"}"""
        );

        var r = Collect(dir);

        var d = r.RepeatReads;
        foreach (
            var (got, want) in new[]
            {
                (d.Fired, System.Text.Encoding.UTF8.GetByteCount(FiredBody)),
                (d.Changed, 3),
                (d.Ranged, 3),
                (d.Declined, 3),
                (d.HookAbsent, 3),
                (d.Subagent, 3),
            }
        )
        {
            Assert.Equal((1L, (long)want), (got.Calls, got.Bytes));
        }
        Assert.Contains("repeat reads  total 6", StatsTable.Render(r), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cat a.rs", false)]
    [InlineData("grep foo a.rs", false)]
    [InlineData("warm-cache a.rs", false)] // "rm" must not match inside "warm"
    [InlineData("sed -i '' 's/x/y/' a.rs", true)]
    [InlineData("echo hi > a.rs", true)]
    [InlineData("git checkout -- .", true)] // a blanket writer, the path not even named
    public void Bash_touches_needs_a_write_signal_not_just_a_named_read(string cmd, bool want) =>
        Assert.Equal(want, StatsFolds.BashTouches(cmd, "a.rs"));

    [Fact]
    public void Expand_right_after_counts_expands_of_shown_ids_only()
    {
        using var dir = new TempDir();
        var (a, b) = (new string('a', 64), new string('b', 64));
        var shown = $"x\n… 23 lines omitted (expand {a})";
        Write(
            dir,
            "e.jsonl",
            [
                Use("t1", "Bash", Input(("command", "grep -A3 x f"))),
                Result("t1", shown),
                Use("t2", "Bash", Input(("command", $"rtok expand {a} --lines 1-9"))),
                Result("t2", "0123456789"),
                Use("t3", "mcp__rtok__expand", Input(("id", a))),
                Result("t3", "01234"),
                Use("t4", "mcp__rtok__expand", Input(("id", b))),
                Result("t4", "never shown"),
            ]
        );
        var r = Collect(dir);
        var d = r.ExpandAfter;
        var want = (long)System.Text.Encoding.UTF8.GetByteCount(shown);
        Assert.Equal((2L, 15L, want), (d.Calls, d.Bytes, d.ShownBytes));
        Assert.Contains(
            $"expand right after  calls 2  bytes 15  shown bytes {want}",
            StatsTable.Render(r),
            StringComparison.Ordinal
        );
    }

    private static readonly Replay Short = Defaults with
    {
        KeepTurns = 2,
        MinTokens = 100,
        HeadLines = 1,
        TailLines = 1,
    };

    private static readonly string ThreeLines =
        new string('x', 50) + "\n" + new string('y', 500) + "\n" + new string('z', 50);

    [Fact]
    public void Archive_replay_keeps_young_turns_and_shrinks_old_ones()
    {
        var tokens = Calls.EstTokens(ThreeLines.Length);
        var none = StatsSignals.None;
        Assert.Equal(tokens * 2, StatsFolds.ReplayCtt(ThreeLines, tokens, 2, Short, none));
        Assert.Equal(50, StatsFolds.ReplayCtt(ThreeLines, 10, 5, Short, none)); // below min_tokens
        var after = StatsFolds.ReplayCtt(ThreeLines, tokens, 5, Short, none);
        var pointer = Calls.EstTokens(51 + 51 + 64); // head line, tail line, pointer line
        Assert.Equal(tokens * 2 + pointer * 3, after);
        Assert.True(after < tokens * 5);
    }

    /// <summary>A body of fewer lines than head + tail is kept once, never twice; the archive
    /// plugin's clip (here 200 bytes) then keeps the pointer below the body.</summary>
    [Fact]
    public void Archive_replay_never_doubles_short_bodies()
    {
        var single = new string('y', 600);
        var tokens = Calls.EstTokens(single.Length);
        var clip = new StatsSignals { ShownLength = l => Math.Min(l.Length, 200) };
        Assert.Equal(
            tokens * 2 + Calls.EstTokens(601 + 64) * 3,
            StatsFolds.ReplayCtt(single, tokens, 5, Short, StatsSignals.None)
        );
        Assert.True(StatsFolds.ReplayCtt(single, tokens, 5, Short, clip) <= tokens * 5);
    }

    [Fact]
    public void Compact_boundary_counts_once_per_event()
    {
        using var dir = new TempDir();
        Raw(
            dir,
            "s.jsonl",
            """{"type":"system","subtype":"compact_boundary","content":"Conversation compacted"}""",
            """{"type":"user","isCompactSummary":true,"message":{"content":"summary"}}""",
            """{"type":"assistant","message":{"content":"ok"}}"""
        );
        var r = Collect(dir);
        Assert.Equal((1L, 1L), (r.Sessions, r.Compact));
        Assert.StartsWith(
            "sessions 1  compact 1  checkpoint 0  no_checkpoint 1  lines 3",
            StatsTable.Render(r),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Sessions_past_the_cutoff_are_not_counted()
    {
        using var dir = new TempDir();
        var old = Raw(dir, "old.jsonl", """{"type":"user","message":{"content":"a"}}""");
        Raw(dir, "new.jsonl", """{"type":"user","message":{"content":"b"}}""");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-90));
        Assert.Equal(1, Collect(dir).Sessions);
    }
}
