using Pyrlyn.ChangePreview;
using Pyrlyn.FsKit;

namespace Rtok.Config;

/// <summary>
/// A planned write of the user config file: its text when planned (empty when missing) and the
/// text the write puts there. The CLI's <c>--dry-run</c> and the operator model print
/// <see cref="Diff"/>; <see cref="Apply"/> writes it.
/// </summary>
/// <param name="File">The user config file.</param>
/// <param name="Before">The file's text when the edit was planned; empty when it was missing.</param>
/// <param name="After">The text the edit writes.</param>
public sealed record ConfigEdit(string File, string Before, string After)
{
    private static readonly AtomicWriteOptions Options = new() { CreateParent = true };

    /// <summary>
    /// The unified diff of the edit, empty when it changes nothing. An absolute path loses its
    /// leading slash after <c>a/</c> and <c>b/</c>, as in <c>git diff --no-index</c>.
    /// </summary>
    public string Diff() => new Preview().Edit(File.TrimStart('/'), Before, After).Render();

    /// <summary>Writes <see cref="After"/>, unless the file changed since the plan.</summary>
    /// <exception cref="ConfigException">The file no longer holds <see cref="Before"/>.</exception>
    public void Apply()
    {
        var now = System.IO.File.Exists(File) ? System.IO.File.ReadAllText(File) : "";
        if (!string.Equals(now, Before, StringComparison.Ordinal))
        {
            throw new ConfigException($"{File} changed since the plan; plan again");
        }
        AtomicFile.Write(File, After, Options);
    }
}
