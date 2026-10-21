using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CsCheck;

namespace Pyrlyn.Jsonc.Tests;

/// <summary>
/// Every edit of a random document gives the expected value and changes bytes only inside the
/// gap the edited entry owns: from the value before it (or the opening bracket) to the key after
/// it (or the closing bracket). Comments outside a removed or replaced value all stay.
/// </summary>
public sealed partial class JsoncPropertyTests
{
    private static readonly JsonDocumentOptions Loose = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Fact]
    public void RemoveChangesOnlyTheGapOfItsEntry() =>
        Gen.Select(Gen.Int, Gen.Int.Positive)
            .Sample(
                (seed, pick) =>
                {
                    var sample = new Sample(seed);
                    var slot = sample.Slots[pick % sample.Slots.Count];
                    var result = Parse(sample).Remove(slot.Path);

                    var want = sample.Model.DeepClone();
                    var (parent, last) = Parent(want, slot.Path);
                    if (parent is JsonObject o)
                    {
                        o.Remove(last.Key!);
                    }
                    else
                    {
                        parent.AsArray().RemoveAt(last.Index);
                    }
                    Check(
                        sample,
                        result,
                        want,
                        slot.GapStart,
                        slot.GapEnd,
                        slot.ValueStart,
                        slot.ValueEnd
                    );
                },
                iter: 2000
            );

    [Fact]
    public void SetReplacesOnlyTheValue() =>
        Gen.Select(Gen.Int, Gen.Int.Positive)
            .Sample(
                (seed, pick) =>
                {
                    var sample = new Sample(seed);
                    var slot = sample.Slots[pick % sample.Slots.Count];
                    var value = sample.Tree(1);
                    var doc = Parse(sample);
                    var result = doc.Set(slot.Path, value);

                    var want = sample.Model.DeepClone();
                    var (parent, last) = Parent(want, slot.Path);
                    var before = last.IsIndex ? parent[last.Index] : parent[last.Key!];
                    if (JsonNode.DeepEquals(before, value))
                    {
                        Assert.Same(doc, result);
                        return;
                    }
                    if (last.IsIndex)
                    {
                        parent[last.Index] = value?.DeepClone();
                    }
                    else
                    {
                        parent[last.Key!] = value?.DeepClone();
                    }
                    Check(
                        sample,
                        result,
                        want,
                        slot.ValueStart,
                        slot.ValueEnd,
                        slot.ValueStart,
                        slot.ValueEnd
                    );
                },
                iter: 2000
            );

    [Fact]
    public void SetInsertsAfterTheLastEntry() =>
        Gen.Select(Gen.Int, Gen.Int.Positive)
            .Sample(
                (seed, pick) =>
                {
                    var sample = new Sample(seed);
                    var box = sample.Boxes[pick % sample.Boxes.Count];
                    var value = sample.Tree(1);
                    JsoncSegment segment = box.IsObject ? "new \"one\"" : box.Count;
                    var result = Parse(sample).Set([.. box.Path, segment], value);

                    var want = sample.Model.DeepClone();
                    var target = box.Path.Length == 0 ? want : Child(Parent(want, box.Path));
                    if (target is JsonObject o)
                    {
                        o[segment.Key!] = value?.DeepClone();
                    }
                    else
                    {
                        target!.AsArray().Add(value?.DeepClone());
                    }
                    Check(sample, result, want, box.After, box.Close, 0, 0);
                    Assert.Equal(
                        Normal(sample.Model),
                        Normal(
                            JsonNode.Parse(
                                result.Remove([.. box.Path, segment]).Text,
                                documentOptions: Loose
                            )
                        )
                    );
                },
                iter: 2000
            );

    private static JsoncDocument Parse(Sample sample)
    {
        var doc = JsoncDocument.Parse(sample.Text);
        Assert.Equal(sample.Text, doc.Text);
        return doc;
    }

    private static (JsonNode Parent, JsoncSegment Last) Parent(JsonNode root, JsoncSegment[] path)
    {
        var node = root;
        foreach (var step in path[..^1])
        {
            node = step.IsIndex ? node[step.Index]! : node[step.Key!]!;
        }
        return (node, path[^1]);
    }

    private static JsonNode? Child((JsonNode Parent, JsoncSegment Last) at) =>
        at.Last.IsIndex ? at.Parent[at.Last.Index] : at.Parent[at.Last.Key!];

    private static string Normal(JsonNode? node) => node?.ToJsonString() ?? "null";

    /// <summary>
    /// The result holds <paramref name="want"/>, and the original's bytes outside
    /// <c>[lo, hi)</c> frame it unchanged. Every comment outside <c>[gone, goneEnd)</c> is still there.
    /// </summary>
    private static void Check(
        Sample sample,
        JsoncDocument result,
        JsonNode want,
        int lo,
        int hi,
        int gone,
        int goneEnd
    )
    {
        var original = sample.Text;
        var text = result.Text;
        Assert.Equal(Normal(want), Normal(JsonNode.Parse(text, documentOptions: Loose)));
        Assert.True(
            text.StartsWith(original[..lo], StringComparison.Ordinal)
                && text.EndsWith(original[hi..], StringComparison.Ordinal)
                && text.Length >= lo + original.Length - hi,
            $"edit outside [{lo}, {hi}):\n{original}\n---\n{text}"
        );
        foreach (Match comment in Marker().Matches(original))
        {
            if (comment.Index < gone || comment.Index >= goneEnd)
            {
                Assert.True(
                    text.Contains(comment.Value, StringComparison.Ordinal),
                    $"{comment.Value} lost:\n{original}\n---\n{text}"
                );
            }
        }
        Assert.Equal(text, JsoncDocument.Parse(text).Text);
    }

    [GeneratedRegex(@"c\d+\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Marker();
}
