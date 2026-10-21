using Pyrlyn.FsKit;

namespace Rtok.Cli.Shell;

/// <summary>
/// <c>rtok completions --install | --uninstall</c>: the script goes to the shell's standard
/// per-user place. PowerShell also gets one dot-source line in <c>$PROFILE</c>; only that line
/// is added or removed, the rest of the profile stays byte for byte.
/// </summary>
internal static class CompletionInstaller
{
    private static readonly AtomicWriteOptions Options = new() { CreateParent = true };

    /// <summary>One report line: the verb (<c>wrote</c>, <c>unchanged</c>, <c>removed</c>, <c>absent</c>, <c>updated</c>) and the path, or a note.</summary>
    /// <param name="Verb">The verb, or null for a note.</param>
    /// <param name="Text">The path or the note.</param>
    internal readonly record struct Report(string? Verb, string Text)
    {
        /// <inheritdoc/>
        public override string ToString() => Verb is null ? Text : $"{Verb} {Text}";
    }

    /// <summary>Writes the script, and the PowerShell profile line; one report per file, then the note.</summary>
    public static IReadOnlyList<Report> Install(
        string shell,
        string script,
        CompletionPlaces places
    )
    {
        var (path, note) = places.Script(shell);
        var reports = new List<Report> { WriteIfChanged(path, script) };
        if (shell is "powershell")
        {
            var profile = places.PowerShellProfile;
            reports.Add(
                ProfileWith(Read(profile), SourceLine(path)) is { } updated
                    ? WriteIfChanged(profile, updated)
                    : new Report("unchanged", profile)
            );
        }
        if (note is not null)
        {
            reports.Add(new Report(null, note));
        }
        return reports;
    }

    /// <summary>Removes exactly what <see cref="Install"/> wrote.</summary>
    public static IReadOnlyList<Report> Uninstall(string shell, CompletionPlaces places)
    {
        var (path, _) = places.Script(shell);
        var reports = new List<Report>();
        if (File.Exists(path))
        {
            File.Delete(path);
            reports.Add(new Report("removed", path));
        }
        else
        {
            reports.Add(new Report("absent", path));
        }
        if (shell is "powershell")
        {
            var profile = places.PowerShellProfile;
            if (ProfileWithout(Read(profile), SourceLine(path)) is { } kept)
            {
                AtomicFile.Write(profile, kept, Options);
                reports.Add(new Report("updated", profile));
            }
        }
        return reports;
    }

    /// <summary>The profile with the source line appended; null when it is already there.</summary>
    internal static string? ProfileWith(string old, string line)
    {
        if (Lines(old).Contains(line, StringComparer.Ordinal))
        {
            return null;
        }
        var separator = old.Length == 0 || old.EndsWith('\n') ? "" : "\n";
        return $"{old}{separator}{line}\n";
    }

    /// <summary>The profile without the source line; null when it is not there.</summary>
    internal static string? ProfileWithout(string old, string line)
    {
        if (!Lines(old).Contains(line, StringComparer.Ordinal))
        {
            return null;
        }
        var kept = new System.Text.StringBuilder();
        var start = 0;
        while (start < old.Length)
        {
            var end = old.IndexOf('\n', start);
            end = end < 0 ? old.Length : end + 1;
            var piece = old[start..end];
            if (!string.Equals(piece.TrimEnd('\r', '\n'), line, StringComparison.Ordinal))
            {
                kept.Append(piece);
            }
            start = end;
        }
        return kept.ToString();
    }

    private static string SourceLine(string script) => $". \"{script}\"  # rtok completions";

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n').Select(l => l.TrimEnd('\r'));

    private static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

    private static Report WriteIfChanged(string path, string text)
    {
        if (
            File.Exists(path)
            && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal)
        )
        {
            return new Report("unchanged", path);
        }
        AtomicFile.Write(path, text, Options);
        return new Report("wrote", path);
    }
}
