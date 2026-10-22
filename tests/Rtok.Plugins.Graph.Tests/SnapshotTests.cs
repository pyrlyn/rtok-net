using System.Text;
using static VerifyXunit.Verifier;

namespace Rtok.Plugins.Graph.Tests;

// Every tool's answer on one labelled fixture, byte for byte.
public sealed class SnapshotTests
{
    [Fact]
    public async Task Five_tools_on_a_labelled_fixture()
    {
        using var h = new GraphHarness();
        h.Write(
            "src/shape.rs",
            "/// A point in the plane.\npub struct Point {\n    x: i32,\n}\n\n/// The area of a shape.\npub fn area(p: &Point) -> i32 {\n    scale(p.x)\n}\n\nfn scale(v: i32) -> i32 {\n    v * 2\n}\n"
        );
        h.Write(
            "src/main.rs",
            "use crate::shape::area;\n\nfn main() {\n    report();\n}\n\nfn report() {\n    let p = Point { x: 1 };\n    area(&p);\n}\n"
        );
        var root = h.Project.Path;
        var answers = new StringBuilder();
        void Add(string call, string answer) =>
            answers.Append("## ").Append(call).Append('\n').Append(answer).Append("\n\n");

        Add("symbol area", GraphTools.Symbol(h.Cx, root, "area"));
        Add(
            "symbol Point kind=class",
            GraphTools.Symbol(h.Cx, root, "Point", new GraphFilter(Kind: "class"))
        );
        Add("callers scale", GraphTools.Callers(h.Cx, root, "scale"));
        Add("impact scale depth=4", GraphTools.Impact(h.Cx, root, "scale", 4));
        Add("impact main to=scale", GraphTools.Impact(h.Cx, root, "main", 4, to: "scale"));
        Add("outline src/shape.rs", GraphTools.Outline(h.Cx, root, "src/shape.rs"));
        Add(
            "explore how does report reach scale",
            GraphTools.Explore(h.Cx, root, "how does report reach scale")
        );
        await Verify(answers.ToString());
    }
}
