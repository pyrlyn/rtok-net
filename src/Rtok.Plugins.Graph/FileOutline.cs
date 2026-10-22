using System.Text;
using Rtok.Plugins.Read;

namespace Rtok.Plugins.Graph;

/// <summary>The <c>outline</c> tool's text: <c>read</c>'s map mode of one file under the
/// root.</summary>
internal static class FileOutline
{
    /// <summary>The map of <paramref name="path"/>, absolute or relative to
    /// <paramref name="root"/>.</summary>
    /// <exception cref="ArgumentException">The path leaves the root.</exception>
    /// <exception cref="FileNotFoundException">No such file.</exception>
    public static string Map(string root, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var full = Path.GetFullPath(path, Path.GetFullPath(root));
        var relative = Path.GetRelativePath(Path.GetFullPath(root), full);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new ArgumentException($"{path}: outside the project root", nameof(path));
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileNotFoundException($"{path}: no such file", path, e);
        }

        return Outline.Render(full, Encoding.UTF8.GetString(bytes), "map");
    }

    /// <summary>The lines of <paramref name="text"/> whose first word after any <c>pub…</c>
    /// marker is <paramref name="kind"/>; the whole text when no kind is asked.</summary>
    public static string OfKind(string text, string path, string kind)
    {
        if (kind.Length == 0)
        {
            return text;
        }

        var kept = TextLines
            .Split(text)
            .Where(l =>
                string.Equals(
                    l.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(w => !w.StartsWith("pub", StringComparison.Ordinal)),
                    kind,
                    StringComparison.Ordinal
                )
            )
            .ToList();
        return kept.Count == 0 ? $"no {kind} in {path}" : string.Join('\n', kept);
    }
}
