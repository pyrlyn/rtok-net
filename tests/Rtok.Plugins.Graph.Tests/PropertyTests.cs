using System.Globalization;
using System.Text;
using CsCheck;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Plugins.Graph.Tests;

// Properties of the answers: the cap is lossless, impact stays in its budget and its depth.
public sealed class PropertyTests
{
    // One alphabet per answer: the cap's character budget assumes an even token rate.
    private static readonly Gen<string[]> Answer = Gen.OneOf(
        Gen.String[Gen.Char["a-z ();{}"], 0, 60].Array[1, 300],
        Gen.String[Gen.Char['漢', '漢'], 0, 30].Array[1, 300]
    );

    [Fact]
    public void A_capped_answer_fits_and_expand_gives_back_every_byte() =>
        Gen.Select(Answer, Gen.Int[20, 400])
            .Sample(t =>
            {
                var (lines, max) = t;
                var text = string.Join('\n', lines) + "\n";
                var host = new MemoryHost(
                    "{\"plugins\":{\"graph\":{\"max_tokens\":"
                        + max.ToString(CultureInfo.InvariantCulture)
                        + "}}}"
                );

                var output = Cap.Apply(new Ctx(host), text);

                if (host.Estimate(text, TextClass.Code) <= max)
                {
                    Assert.Equal(text, output);
                    Assert.Empty(host.Recorded());
                    return;
                }

                var row = Assert.Single(host.Recorded());
                Assert.Equal(Encoding.UTF8.GetBytes(text), host.GetArchive(row.RefId!));
                Assert.EndsWith(
                    ArchiveIds.Abbreviate(row.RefId!),
                    output,
                    StringComparison.Ordinal
                );
                var head = output[..(output.LastIndexOf('\n') + 1)];
                Assert.StartsWith(head, text, StringComparison.Ordinal);
                // The first line always shows, so only a budget past one line must hold.
                var first = text[..(text.IndexOf('\n', StringComparison.Ordinal) + 1)];
                if (host.Estimate(first, TextClass.Code) + 40 <= max)
                {
                    Assert.True(host.Estimate(output, TextClass.Code) <= max, output);
                }

                Assert.NotEqual("", head, StringComparer.Ordinal);
            });

    [Fact]
    public void Impact_stays_within_its_budget_and_the_depth_cap() =>
        Gen.Int[2, 30]
            .SelectMany(n =>
                Gen.Select(Gen.Int[0, n - 1], Gen.Int[0, n - 1]).Array[0, 3 * n].Select(e => (n, e))
            )
            .Select(Gen.Int[1, 9], (g, depth) => (g.n, g.e, depth))
            .Sample(
                t =>
                {
                    var (n, edges, depth) = t;
                    using var h = new GraphHarness("""{"impact_tokens":150}""");
                    var src = new StringBuilder();
                    for (var i = 0; i < n; i++)
                    {
                        src.Append(CultureInfo.InvariantCulture, $"fn f{i}() {{\n");
                        foreach (var (_, to) in edges.Where(e => e.Item1 == i))
                        {
                            src.Append(CultureInfo.InvariantCulture, $"    f{to}();\n");
                        }

                        src.Append("}\n");
                    }

                    h.Write("g.rs", src.ToString());

                    var budgeted = GraphTools.Impact(h.Cx, h.Project.Path, "f0", depth);
                    var all = GraphTools.Impact(
                        h.Cx,
                        h.Project.Path,
                        "f0",
                        depth,
                        new GraphFilter(All: true)
                    );

                    Assert.True(h.Host.Estimate(budgeted, TextClass.Code) <= 150, budgeted);
                    foreach (
                        var line in all.Split('\n')
                            .Where(l => l.Contains("  ", StringComparison.Ordinal))
                    )
                    {
                        var d = int.Parse(
                            line[..line.IndexOf(' ', StringComparison.Ordinal)],
                            CultureInfo.InvariantCulture
                        );
                        Assert.InRange(d, 1, Math.Min(depth, 4));
                    }
                },
                iter: 25
            );
}
