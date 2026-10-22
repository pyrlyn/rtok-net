using CsCheck;
using Rtok.Testing;

namespace Rtok.Measure.Tests;

/// <summary>A transcript with lines cut short or swapped for garbage: the rest still counts.</summary>
public sealed class GarbageLinesTests
{
    private static readonly string[] Fixture = File.ReadAllLines(
        RepoPaths.Fixture("stats", "claude", "proj", "sess-1.jsonl")
    );

    [Fact]
    public void Every_intact_line_still_yields_its_records() =>
        Gen.Select(Gen.Int[0, 2].Array[Fixture.Length], Gen.String, Gen.Int[0, 400])
            .Sample(
                (modes, garbage, cut) =>
                {
                    var lines = Fixture
                        .Select(
                            (l, i) =>
                                modes[i] switch
                                {
                                    1 => l[..Math.Min(cut, l.Length - 1)],
                                    2 => garbage,
                                    _ => l,
                                }
                        )
                        .ToArray();

                    var t = Transcript.Parse(string.Join('\n', lines));

                    var kept = Fixture.Where((_, i) => modes[i] == 0);
                    var ids = t.ToolUses.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
                    var results = t
                        .ToolResults.Select(r => r.ToolUseId)
                        .ToHashSet(StringComparer.Ordinal);
                    var whole = Transcript.Parse(string.Join('\n', kept));
                    return whole.ToolUses.TrueForAll(u => ids.Contains(u.Id))
                        && whole.ToolResults.TrueForAll(r => results.Contains(r.ToolUseId))
                        && t.Usages.TrueForAll(u =>
                            u.Input >= 0 && u.Output >= 0 && u.CacheRead >= 0 && u.CacheCreate >= 0
                        );
                },
                iter: 300
            );
}
