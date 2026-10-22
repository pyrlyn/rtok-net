using System.Globalization;
using System.Text;
using Pyrlyn.ChangePreview;

namespace Rtok.Doctor;

/// <summary>
/// <c>rtok doctor --fix</c>: removes broken hooks and the extra copies of duplicates from the
/// files they live in, each edit minimal and backed up first.
/// </summary>
public static class DoctorFix
{
    /// <summary>What <paramref name="request"/> would remove, and the token that confirms it.</summary>
    /// <param name="options">The machine.</param>
    /// <param name="request">The kinds, kept copies and toggles.</param>
    /// <returns>The plan.</returns>
    public static FixPlan Plan(DoctorOptions options, FixRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        return new FixSession(new DoctorHome(options), request).ToPlan();
    }

    /// <summary>
    /// Applies <paramref name="request"/> when its plan still has the <paramref name="confirmed"/>
    /// token; otherwise writes nothing and hands back the fresh plan.
    /// </summary>
    /// <param name="options">The machine.</param>
    /// <param name="request">The request the confirmed plan was made from.</param>
    /// <param name="confirmed">The token of the plan the user saw.</param>
    /// <returns>The report.</returns>
    public static FixResult Apply(DoctorOptions options, FixRequest request, string confirmed)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        var session = new FixSession(new DoctorHome(options), request);
        if (!string.Equals(session.Token(), confirmed, StringComparison.Ordinal))
        {
            return new FixResult(
                "The files changed since the preview, so nothing was written. Review the new preview and confirm again.\n",
                1,
                session.ToPlan()
            );
        }
        var (text, code) = Execute(session, write: true, stat: false);
        return new FixResult(text, code, null);
    }

    /// <summary>The CLI run: plans the default selection and, with <paramref name="apply"/>, writes it.</summary>
    /// <param name="options">The machine.</param>
    /// <param name="kinds">The kinds <c>--only</c> named; null for all.</param>
    /// <param name="apply">Write (<c>--yes</c>); else a dry run that prints the diffs.</param>
    /// <param name="stat">Print stat lines instead of diffs.</param>
    /// <returns>The report and the exit code.</returns>
    public static (string Text, int Code) Run(
        DoctorOptions options,
        IReadOnlyList<string>? kinds,
        bool apply,
        bool stat
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var session = new FixSession(new DoctorHome(options), new FixRequest { Kinds = kinds });
        return Execute(session, apply, stat);
    }

    private static (string Text, int Code) Execute(FixSession session, bool write, bool stat)
    {
        var refused = session.Refused();
        if (session.Files.Count == 0 && refused.Count == 0)
        {
            return ("nothing to remove\n", 0);
        }
        var sb = new StringBuilder();
        if (!write && session.Files.Count > 0)
        {
            sb.Append("dry run: nothing is written. Add --yes to remove the entries below.\n");
        }
        var setup = session.Home.Config.Setup;
        var writer =
            session.Home.Options.Write
            ?? DoctorWrites.Disk(setup.Backup, (int)Math.Min(setup.BackupFiles, int.MaxValue));
        var preview = new Preview();
        var done = 0;
        var code = 0;
        foreach (var file in session.Files)
        {
            sb.Append(file.Source).Append('\n');
            foreach (var p in file.Entries)
            {
                Line(
                    sb,
                    $"  {(write ? "remove" : "would remove")} {FixSession.Label(p)} `{p.Command}`: {p.Detail}"
                );
            }
            if (file.Error is { } why)
            {
                Line(sb, $"  skipped: {why}");
                code = 1;
                continue;
            }
            preview.Edit(file.Source, file.Before, file.After);
            if (!write)
            {
                if (!stat)
                {
                    sb.Append(file.Diff);
                }
                continue;
            }
            var outcome = Write(session.Home, file, writer);
            if (outcome.Failed is { } failed)
            {
                Line(sb, $"  {failed}");
                code = 1;
                continue;
            }
            if (outcome.Backup is { } bak)
            {
                Line(sb, $"  backup {bak}");
            }
            Line(sb, "  written");
            done += file.Entries.Count;
        }
        Totals(sb, preview, stat);
        Summary(sb, session, refused, write ? done : null);
        return (sb.ToString(), code);
    }

    /// <summary>The refusals, then how many entries went (or would go) and how many are left.</summary>
    private static void Summary(
        StringBuilder sb,
        FixSession session,
        List<string> refused,
        int? done
    )
    {
        foreach (var r in refused)
        {
            sb.Append(r).Append('\n');
        }
        if (done is { } n)
        {
            var left = Findings
                .Scan(session.Home)
                .Problems.Count(p => Findings.Wanted(p, session.Kinds));
            Line(sb, $"{n} {Entries(n)} removed, {left} left");
            return;
        }
        var planned = session.Files.Where(f => f.Error is null).Sum(f => f.Entries.Count);
        Line(sb, $"{planned} {Entries(planned)} would be removed");
    }

    /// <summary>Writes one file unless it changed since the check; the backup, or why it was not written.</summary>
    private static (string? Backup, string? Failed) Write(
        DoctorHome home,
        FileFix file,
        FixWrite writer
    )
    {
        string? now;
        try
        {
            now = home.Read(file.Source);
        }
        catch (IOException e)
        {
            return (null, $"failed: {e.Message}");
        }
        if (!string.Equals(now, file.Before, StringComparison.Ordinal))
        {
            return (null, "skipped: changed since the check");
        }
        try
        {
            return (writer(file.Source, file.After), null);
        }
        catch (IOException e)
        {
            return (null, $"failed: {e.Message}");
        }
    }

    /// <summary>The totals line, or every stat line and the totals with <c>--stat</c>.</summary>
    private static void Totals(StringBuilder sb, Preview preview, bool stat)
    {
        if (preview.IsEmpty)
        {
            return;
        }
        var text = preview.Render(PreviewMode.Stat);
        if (stat)
        {
            sb.Append(text);
            return;
        }
        var lines = text.TrimEnd('\n').Split('\n');
        sb.Append(lines[^1]).Append('\n');
    }

    private static string Entries(int n) => n == 1 ? "entry" : "entries";

    private static void Line(StringBuilder sb, string text) =>
        sb.Append(CultureInfo.InvariantCulture, $"{text}\n");
}
