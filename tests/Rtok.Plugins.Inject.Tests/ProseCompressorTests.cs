using System.Text;
using CsCheck;

namespace Rtok.Plugins.Inject.Tests;

// Ported from rtok's modes::compress tests (T604, T606, T608).
public sealed class ProseCompressorTests
{
    private static readonly ProseIntensity[] Every =
    [
        ProseIntensity.Lite,
        ProseIntensity.Full,
        ProseIntensity.Ultra,
    ];

    [Fact]
    public void FullDropsFillerAndKeepsTheFence()
    {
        var input =
            "Sure! I'd be happy to help. The bug is just really in auth.\n```rs\nfn f() { let the = 1; }\n```\nThen fix it.";
        Assert.Equal(
            "bug is in auth.\n```rs\nfn f() { let the = 1; }\n```\nThen fix it.",
            ProseCompressor.Compress(input, ProseIntensity.Full)
        );
    }

    [Fact]
    public void NegationIsNeverDropped() =>
        Assert.Equal(
            "Do not delete table. Never run drop. This is no joke.",
            ProseCompressor.Compress(
                "Do not delete the table. Never run drop. This is no joke.",
                ProseIntensity.Full
            )
        );

    [Fact]
    public void LiteDropsOnlyPleasantries() =>
        Assert.Equal(
            "The function really works.",
            ProseCompressor.Compress("Sure! The function really works.", ProseIntensity.Lite)
        );

    [Theory]
    [InlineData(
        "Sure! The answer is just `Sure! The really exact value` and the result.",
        "answer is `Sure! The really exact value` and result."
    )]
    [InlineData(
        "Of course! Use ``the really `simple` value`` and the helper.",
        "Use ``the really `simple` value`` and helper."
    )]
    [InlineData(
        "The result is `left   middle\n  right` for the test.",
        "result is `left   middle\n  right` for test."
    )]
    [InlineData("Use `x` then foo_a, and the `y`.", "Use `x` then foo_a, and `y`.")]
    public void InlineBacktickSpansAreKept(string input, string expected) =>
        Assert.Equal(expected, ProseCompressor.Compress(input, ProseIntensity.Full));

    [Fact]
    public void UltraStripsAStandalonePhraseButNeverCutsAWord()
    {
        Assert.Contains(
            "command then",
            ProseCompressor.Compress(
                "Run the command then check the output.",
                ProseIntensity.Ultra
            ),
            StringComparison.Ordinal
        );
        Assert.Equal(
            "Build it run it.",
            ProseCompressor.Compress("Build it and then run it.", ProseIntensity.Ultra)
        );
    }

    [Theory]
    [InlineData("Edit a.txt now", "a.txt")]
    [InlineData("Open a-b next", "a-b")]
    [InlineData("Check a/b here", "a/b")]
    [InlineData("Keep a1 there", "a1")]
    public void AnArticleInsideANameIsKept(string input, string name) =>
        Assert.Contains(
            name,
            ProseCompressor.Compress(input, ProseIntensity.Full),
            StringComparison.Ordinal
        );

    // Glue on the left of a word counts as much as glue on its right: `foo_a` used to come
    // back as `foo_`.
    [Theory]
    [InlineData("foo_a")]
    [InlineData("foo_the")]
    [InlineData("/tmp/a")]
    [InlineData("C:\\tmp\\the")]
    [InlineData("item1a")]
    [InlineData("v2an")]
    [InlineData("x-a")]
    [InlineData("x.the")]
    [InlineData("_a_")]
    [InlineData("naïvea")]
    [InlineData("nai\u0308vea")]
    [InlineData("жa")]
    [InlineData("a世")]
    [InlineData("a_b")]
    [InlineData("1_a_2")]
    public void AWordGluedToANameOnEitherSideIsKept(string name)
    {
        foreach (var intensity in Every)
        {
            Assert.Contains(
                name,
                ProseCompressor.Compress($"Edit {name} now", intensity),
                StringComparison.Ordinal
            );
        }
    }

    [Theory]
    [InlineData("Edit the file", ProseIntensity.Full, "Edit file")]
    [InlineData("Edit the file and a test", ProseIntensity.Ultra, "Edit file and test")]
    [InlineData("foo_a is really the one", ProseIntensity.Full, "foo_a is one")]
    public void AFreeArticleOrFillerGoes(string input, ProseIntensity intensity, string expected) =>
        Assert.Equal(expected, ProseCompressor.Compress(input, intensity));

    // The outer four-backtick fence holds a three-backtick example: the shorter run must not
    // close it, or the words inside the outer block get stripped.
    [Fact]
    public void ALongerFenceSurvivesAShorterInnerRun()
    {
        const string fence =
            "````md\nSure! Example:\n```rs\nlet the = 1;\n```\nThe really exact content\n````";
        foreach (var intensity in Every)
        {
            Assert.Contains(
                fence,
                ProseCompressor.Compress(
                    $"Sure! The bug is really here.\n{fence}\nFix the thing.",
                    intensity
                ),
                StringComparison.Ordinal
            );
        }

        Assert.Equal(
            $"bug is here.\n{fence}\nFix thing.",
            ProseCompressor.Compress(
                $"The bug is really here.\n{fence}\nFix the thing.",
                ProseIntensity.Full
            )
        );
    }

    [Theory]
    [InlineData("```\nthe really\n`````", "```\nthe really\n`````")]
    [InlineData("~~~\nthe really\n~~~", "~~~\nthe really\n~~~")]
    [InlineData("   ```\nthe really\n   ```", "   ```\nthe really\n   ```")]
    [InlineData("```\nthe really\n```   ", "```\nthe really\n```")]
    [InlineData("~~~~ info `ok`\nthe really\n~~~~", "~~~~ info `ok`\nthe really\n~~~~")]
    public void FencesFollowCommonMark(string fence, string kept) =>
        Assert.Equal(
            $"intro\n{kept}\noutro",
            ProseCompressor.Compress($"The intro\n{fence}\nThe outro", ProseIntensity.Full)
        );

    [Theory]
    [InlineData("``` the really")]
    [InlineData("~~~")]
    [InlineData("`` the really ``")]
    [InlineData("````` the")]
    public void ARunThatIsNotACloserStaysCode(string body)
    {
        var fence = $"````\nx\n{body}\nthe really exact\n````";
        Assert.Equal(fence, ProseCompressor.Compress(fence, ProseIntensity.Full));
    }

    [Fact]
    public void FourSpacesOfIndentIsNotAFence() =>
        Assert.Equal(
            "```\nthe really\n    ```",
            ProseCompressor.Compress("    ```\nthe really\n    ```", ProseIntensity.Full)
        );

    [Fact]
    public void AnUnclosedFenceRunsToTheEnd() =>
        Assert.Equal(
            "intro\n```\nthe really\n```x\nthe end",
            ProseCompressor.Compress(
                "The intro\n```\nthe really\n```x\nthe end",
                ProseIntensity.Full
            )
        );

    [Fact]
    public void AMidLineTripleBacktickStaysInline() =>
        Assert.Equal(
            "```the really``` span and rest.",
            ProseCompressor.Compress("The ```the really``` span and the rest.", ProseIntensity.Full)
        );

    [Fact]
    public void ABacktickInfoStringMakesAnInlineSpanNotAFence() =>
        Assert.Equal(
            "```the really``` span\nx",
            ProseCompressor.Compress("```the really``` span\nthe x", ProseIntensity.Full)
        );

    // rtok glued the line after a closing fence onto the closer, which unmade the fence.
    [Fact]
    public void TheLineAfterAClosingFenceStaysOnItsOwnLine() =>
        Assert.Equal(
            "```\na\n```\nb\n\n~~~\nc\n~~~\n",
            ProseCompressor.Compress("```\na\n```  \r\nthe b\n\n~~~\nc\n~~~\n", ProseIntensity.Full)
        );

    [Theory]
    [InlineData("Assure! Sure! The result.", "Assure! Sure! The result.")]
    [InlineData("Sure! Sure thing! Done.", "Done.")]
    [InlineData("Certaınly! Done.", "Certaınly! Done.")]
    [InlineData("SURE! done", "done")]
    public void OnlyWholeLeadingPleasantriesGo(string input, string expected) =>
        Assert.Equal(expected, ProseCompressor.Compress(input, ProseIntensity.Lite));

    [Fact]
    public void ALoneSurrogateDoesNotThrow() =>
        Assert.Equal("\uD800 x", ProseCompressor.Compress("\uD800 the x", ProseIntensity.Full));

    [Fact]
    public void OutputIsNeverLongerAndEveryFencedBlockSurvives()
    {
        var piece = Gen.OneOfConst(
            "```",
            "````",
            "~~~",
            "`",
            "\n",
            "\r\n",
            "   ",
            " ",
            "the ",
            "a",
            "just ",
            "Sure! ",
            "and then ",
            "not ",
            "foo_",
            "x",
            "é",
            "😀",
            "\u3000",
            "rs"
        );
        Gen.Select(piece.Array[0, 40], Gen.OneOfConst(Every))
            .Sample(
                (pieces, intensity) =>
                {
                    var input = string.Concat(pieces);
                    var output = ProseCompressor.Compress(input, intensity);
                    Assert.True(output.Length <= input.Length, $"{input} → {output}");
                    Assert.True(
                        Encoding.UTF8.GetByteCount(output) <= Encoding.UTF8.GetByteCount(input),
                        $"{input} → {output}"
                    );
                    var from = 0;
                    foreach (var block in Fences(input))
                    {
                        var at = output.IndexOf(block, from, StringComparison.Ordinal);
                        Assert.True(at >= 0, $"{block} lost: {input} → {output}");
                        from = at + block.Length;
                    }
                },
                iter: 2000
            );
    }

    private static IEnumerable<string> Fences(string text)
    {
        while (ProseCompressor.FindFence(text) is (var start, var end))
        {
            yield return text[start..end];
            text = text[end..];
        }
    }
}
