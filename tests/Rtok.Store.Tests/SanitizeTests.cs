using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CsCheck;

namespace Rtok.Store.Tests;

public sealed class SanitizeTests
{
    public static TheoryData<string, string> TextCases =>
        new()
        {
            { "\u001b[31mred\u001b[0m", "red" },
            { "a\u001b[2K\u001b[1Gb", "ab" },
            { "\u001b]8;;https://x\u0007link\u001b]8;;\u0007", "link" },
            { "\u001b]8;;https://x\u001b\\link\u001b]8;;\u001b\\", "link" },
            { "a\u001b=b", "ab" },
            { "\u001bé", "é" },
            { "a\u001b[", "a" },
            { "a\u001b[ёж", "a" },
            { "a\u0007b\u0000c\u0008d\u007fe", "abcde" },
            { "a\tb", "a\tb" },
            { "a\r\nb\r\n", "a\nb\n" },
            { "a\rb", "a\nb" },
            { "a\u200bb\u2060c\ufeff", "abc" },
            { "👩\u200d💻", "👩\u200d💻" },
            { "a  \nb\t\n", "a\nb\n" },
            { "    fn x() {\n\tlet y;\n}", "    fn x() {\n\tlet y;\n}" },
            { "a\n\n\n\n\nb", "a\n\nb" },
            { "a\n  \n \t\n\nb", "a\n\nb" },
            { "a\n\nb", "a\n\nb" },
            {
                "<system-reminder>\nYou are in a worktree.\n</system-reminder>\n\n\nfix the test",
                "fix the test"
            },
            {
                "<task-notification>\n<task-id>b1</task-id>\n<status>completed</status>\n</task-notification>",
                ""
            },
            { "<ci-monitor-event>pr 7 green</ci-monitor-event>\nok", "ok" },
            { "<local-command-caveat>Caveat: x</local-command-caveat>\nls", "ls" },
            { "a <system-reminder>x</system-reminder> b", "a  b" },
            {
                "<system-reminder>1</system-reminder>\nmid\n<system-reminder>2</system-reminder>\nend",
                "mid\nend"
            },
            { "<system-reminder>x</task-notification>", "<system-reminder>x</task-notification>" },
            { "<system-reminder> x", "<system-reminder> x" },
            { "<task-id>b1</task-id>", "<task-id>b1</task-id>" },
            { "<system-\u001b[0mreminder>hidden</system-reminder>\nvisible", "visible" },
            { "<system-\u200breminder>hidden</system-reminder>\nvisible", "visible" },
            { "<system-reminder>hidden</system\ufeff-reminder>\nvisible", "visible" },
            {
                "<system-<task-notification>x</task-notification>reminder>hidden</system-reminder>\nvisible",
                "visible"
            },
            {
                "<system-reminder>a<system-reminder>b</system-<task-notification>x</task-notification>reminder>\nvisible",
                "visible"
            },
            { "<system-\u200breminder> x", "<system-reminder> x" },
        };

    [Theory]
    [MemberData(nameof(TextCases))]
    public void Text_drops_each_kind_of_noise(string input, string want)
    {
        var got = Sanitize.Text(input);
        Assert.Equal(want, got);
        Assert.Equal(got, Sanitize.Text(got));
    }

    public static TheoryData<string, string> TerminalNoiseCases =>
        new()
        {
            { "\u001b[31mred\u001b[0m \u001b]8;;u\u0007l\u001b]8;;\u0007", "red l" },
            { "a\u0007b\u0000c\u200bd\ufeffe\u2060", "abcde" },
            { "a  \r\n\r\n\n\n\tb \t\n", "a  \r\n\r\n\n\n\tb \t\n" },
            {
                "<system-reminder>\n\u001b[1mx\u001b[0m\n</system-reminder>\n\n\n",
                "<system-reminder>\nx\n</system-reminder>\n\n\n"
            },
            { "👩\u200d💻", "👩\u200d💻" },
        };

    [Theory]
    [MemberData(nameof(TerminalNoiseCases))]
    public void Terminal_noise_touches_nothing_else(string input, string want)
    {
        var got = Sanitize.TerminalNoise(input);
        Assert.Equal(want, got);
        Assert.Equal(got, Sanitize.TerminalNoise(got));
    }

    [Fact]
    public void Clean_input_comes_back_as_the_same_instance()
    {
        var text = "plain text\n\nwith one blank line and\ttabs";
        var noise = "a  \r\nb\t";
        Assert.Same(text, Sanitize.Text(text));
        Assert.Same(noise, Sanitize.TerminalNoise(noise));
    }

    [Fact]
    public void Json_strings_are_cleaned_and_everything_else_kept()
    {
        var raw =
            """{"tool_input":{"command":"ls  \r\n"},"n":1.5,"ok":true,"none":null,"list":["\u001b[1mA\u001b[0m",2],"key  \u0007":"v"}"""u8.ToArray();
        var got = JsonNode.Parse(Sanitize.Body(raw));
        var want = JsonNode.Parse(
            """{"tool_input":{"command":"ls\n"},"n":1.5,"ok":true,"none":null,"list":["A",2],"key  \u0007":"v"}"""
        );
        Assert.True(JsonNode.DeepEquals(want, got));
    }

    [Fact]
    public void A_clean_json_body_keeps_its_bytes()
    {
        var raw = """{ "z": "a", "a": [1, 2] }"""u8.ToArray();
        Assert.Same(raw, Sanitize.Body(raw));
    }

    [Fact]
    public void A_bare_json_string_is_cleaned()
    {
        Assert.Equal("\"ok\""u8.ToArray(), Sanitize.Body("\"\\u001b[1mok\""u8.ToArray()));
    }

    [Fact]
    public void Plain_text_bodies_are_cleaned_as_text()
    {
        Assert.Equal("ok\n"u8.ToArray(), Sanitize.Body("\u001b[32mok\u001b[0m  \r\n"u8.ToArray()));
    }

    [Fact]
    public void Invalid_utf8_is_kept_as_is()
    {
        byte[] raw = [0xff, 0x1b, (byte)'[', (byte)'3', (byte)'1', (byte)'m', 0xfe];
        Assert.Same(raw, Sanitize.Body(raw));
    }

    [Fact]
    public void A_real_hook_prompt_loses_its_wrapper()
    {
        var raw =
            """{"session_id":"s1","hook_event_name":"UserPromptSubmit","prompt":"<system-reminder>\nYou are operating in a git worktree.\n</system-reminder>\n\n\nIn rtok, run the tests  "}"""u8.ToArray();
        var got = JsonNode.Parse(Sanitize.Body(raw))!;
        Assert.Equal("In rtok, run the tests", (string?)got["prompt"]);
        Assert.Equal("s1", (string?)got["session_id"]);
        Assert.Equal("UserPromptSubmit", (string?)got["hook_event_name"]);
    }

    [Fact]
    public void Json_bodies_reach_the_canonical_form_in_one_pass()
    {
        var once = Sanitize.Body(
            """{"prompt":"<system-\u001b[0mreminder>hidden</system-reminder>\nvisible"}"""u8.ToArray()
        );
        Assert.Equal("""{"prompt":"visible"}""", Encoding.UTF8.GetString(once));
        Assert.Same(once, Sanitize.Body(once));
    }

    [Fact]
    public void A_joined_opener_does_not_pair_with_a_later_closer()
    {
        var s =
            "<system-<ci-monitor-event></ci-monitor-event>reminder><system-reminder></system-reminder>";
        Assert.Equal("<system-reminder>", ByFixedPoint(s));
        Assert.Equal("<system-reminder>", Sanitize.Text(s));
    }

    private static readonly string[] Fragments =
    [
        "<system-",
        "reminder>",
        "</system-",
        "<task-notification>",
        "</task-notification>",
        "<system-reminder>",
        "</system-reminder>",
        "\u200b",
        "\u001b[0m",
        "\n",
        "x",
        " ",
        "<ci-monitor-event>",
        "</ci-monitor-event>",
    ];

    [Fact]
    public void Wrappers_agree_with_the_regex_fixed_point() =>
        Gen.OneOfConst(Fragments)
            .Array[0, 24]
            .Select(parts => string.Concat(parts))
            .Sample(s =>
            {
                var got = Sanitize.Text(s);
                Assert.Equal(ByFixedPoint(s), got);
                Assert.Equal(got, Sanitize.Text(got));
            });

    [Fact]
    public void Cleaning_is_idempotent_and_leaves_nothing_it_removes() =>
        Gen.OneOf(
                Gen.Char.Select(c => c.ToString()),
                Gen.OneOfConst(
                    "\u001b",
                    "\u001b[0m",
                    "\u001b[1;31m",
                    "\u200b",
                    "\ufeff",
                    "\r\n",
                    "  \n",
                    "\u0007"
                ),
                Gen.OneOfConst("<system-reminder>", "</system-reminder>")
            )
            .Array[0, 40]
            .Select(parts => string.Concat(parts))
            .Sample(s =>
            {
                var once = Sanitize.Text(s);
                Assert.Equal(once, Sanitize.Text(once));
                foreach (var left in (string[])["\u001b", "\r", "\n\n\n", " \n"])
                {
                    Assert.DoesNotContain(left, once, StringComparison.Ordinal);
                }
            });

    /// <summary>The old shape of the cleaner: characters, then wrapper blocks removed by a lazy
    /// regex to a fixed point, then whitespace. The single-pass scan must agree with it.</summary>
    private static string ByFixedPoint(string s)
    {
        var wrapper = new Regex(
            "(?:"
                + string.Join(
                    '|',
                    (
                        (string[])
                            [
                                "system-reminder",
                                "task-notification",
                                "ci-monitor-event",
                                "local-command-caveat",
                            ]
                    ).Select(t => $"<{t}>.*?</{t}>")
                )
                + ")\n*",
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)
        );
        var current = Sanitize.Characters(s, lineEnds: true);
        while (true)
        {
            var next = wrapper.Replace(current, "");
            if (string.Equals(next, current, StringComparison.Ordinal))
            {
                return Sanitize.Whitespace(current);
            }
            current = next;
        }
    }
}
