using System.Text;
using System.Text.Json.Serialization;

namespace Pyrlyn.ChangePreview;

/// <summary>
/// What a command would change, collected before it changes anything: file rewrites and
/// path deletions. <see cref="Render"/> prints it the way <c>git diff</c> and <c>git diff --stat</c> do.
/// </summary>
public sealed class Preview
{
    private const int Bar = 40;

    private readonly List<FileEdit> _edits = [];
    private readonly List<Removal> _removals = [];

    /// <summary>The rewrites, in the order they were recorded.</summary>
    public IReadOnlyList<FileEdit> Edits => _edits;

    /// <summary>The deletions, in the order they were recorded.</summary>
    public IReadOnlyList<Removal> Removals => _removals;

    /// <summary>True when the command would change nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => Edits.Count == 0 && Removals.Count == 0;

    /// <summary>Records a rewrite of <paramref name="path"/>; an edit that changes nothing is dropped.</summary>
    /// <param name="path">The path as the preview prints it.</param>
    /// <param name="before">The text now.</param>
    /// <param name="after">The text the command would write.</param>
    /// <returns>This preview.</returns>
    public Preview Edit(string path, string before, string after)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return this;
        }
        var lines = UnifiedDiff.Lines(before, after);
        _edits.Add(
            new FileEdit(path, lines.Count(l => l.Kind == '+'), lines.Count(l => l.Kind == '-'))
            {
                Diff = UnifiedDiff.Format(path, lines),
            }
        );
        return this;
    }

    /// <summary>Records a deletion of <paramref name="path"/>, which holds <paramref name="files"/> files taking <paramref name="bytes"/> bytes.</summary>
    /// <param name="path">The path as the preview prints it.</param>
    /// <param name="bytes">The bytes it takes.</param>
    /// <param name="files">The files under it.</param>
    /// <returns>This preview.</returns>
    public Preview Remove(string path, long bytes, long files)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(files);
        _removals.Add(new Removal(path, bytes, files));
        return this;
    }

    /// <summary>The sums the closing lines print.</summary>
    /// <returns>The totals.</returns>
    public PreviewTotals Totals() =>
        new(
            Edits.Count,
            Edits.Sum(e => e.Insertions),
            Edits.Sum(e => e.Deletions),
            Removals.Count,
            Removals.Sum(r => r.Files),
            Removals.Sum(r => r.Bytes)
        );

    /// <summary>
    /// The preview as plain text: the edits (diffs or stat lines), the removal lines, then one
    /// totals line for edits and one for removals. Empty when nothing would change.
    /// <see cref="PreviewText.Paint"/> colours it for a terminal.
    /// </summary>
    /// <param name="mode">Diffs or stat lines.</param>
    /// <returns>The text, every line ending in <c>\n</c>.</returns>
    public string Render(PreviewMode mode = PreviewMode.Diff)
    {
        var sb = new StringBuilder();
        if (mode == PreviewMode.Stat)
        {
            StatLines(sb);
        }
        else
        {
            _edits.ForEach(e => sb.Append(e.Diff));
        }
        RemovalLines(sb);
        var t = Totals();
        if (t.FilesChanged > 0)
        {
            sb.Append(PreviewText.EditTotals(t)).Append('\n');
        }
        if (t.PathsRemoved > 0)
        {
            sb.Append(PreviewText.RemovalTotals(t)).Append('\n');
        }
        return sb.ToString();
    }

    private void StatLines(StringBuilder sb)
    {
        var width = Edits.Max(e => (int?)e.Path.Length) ?? 0;
        var count = Edits.Max(e => (int?)Changed(e).ToString(PreviewText.Culture).Length) ?? 0;
        var most = Edits.Max(e => (int?)Changed(e)) ?? 0;
        // A 2000-line rewrite must not print a 2000-character bar; past the cap every bar
        // shrinks by the same ratio, rounded up so a one-line change still shows a mark.
        int Scale(int n) => most <= Bar ? n : (int)(((long)n * Bar + most - 1) / most);
        foreach (var e in Edits)
        {
            sb.Append(' ')
                .Append(e.Path.PadRight(width))
                .Append(" | ")
                .Append(Changed(e).ToString(PreviewText.Culture).PadLeft(count))
                .Append(' ')
                .Append('+', Scale(e.Insertions))
                .Append('-', Scale(e.Deletions))
                .Append('\n');
        }
    }

    private void RemovalLines(StringBuilder sb)
    {
        var width = Removals.Max(r => (int?)r.Path.Length) ?? 0;
        var size = Removals.Max(r => (int?)PreviewText.HumanBytes(r.Bytes).Length) ?? 0;
        foreach (var r in Removals)
        {
            sb.Append("- ")
                .Append(r.Path.PadRight(width))
                .Append("  ")
                .Append(PreviewText.HumanBytes(r.Bytes).PadLeft(size))
                .Append("  ")
                .Append(PreviewText.Plural(r.Files, "file"))
                .Append('\n');
        }
    }

    private static int Changed(FileEdit e) => e.Insertions + e.Deletions;
}
