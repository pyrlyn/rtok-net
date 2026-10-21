namespace Pyrlyn.ChangePreview.Tests;

public sealed class SnapshotTests
{
    private static Preview Sample() =>
        new Preview()
            .Edit(
                "rtok.toml",
                "[hook]\nbudget = 10\nlog = true\n\n[mcp]\nport = 0\n",
                "[hook]\nbudget = 20\nlog = true\n\n[mcp]\nport = 0\ntools = [\"read\"]\n"
            )
            .Edit("notes.md", "one\ntwo", "one\ntwo\nthree\n")
            .Remove(".worktrees/T1/bin", 48_000_000, 1_204)
            .Remove(".worktrees/T2/obj", 900, 3);

    // The escape character itself would be invisible in the snapshot.
    private static string Visible(string text) => text.Replace('\e', '␛');

    [Fact]
    public Task Diff() => Verify(Sample().Render(PreviewMode.Diff));

    [Fact]
    public Task Stat() => Verify(Sample().Render(PreviewMode.Stat));

    [Fact]
    public Task DiffPainted() =>
        Verify(Visible(PreviewText.Paint(Sample().Render(PreviewMode.Diff), color: true)));

    [Fact]
    public Task StatPainted() =>
        Verify(Visible(PreviewText.Paint(Sample().Render(PreviewMode.Stat), color: true)));
}
