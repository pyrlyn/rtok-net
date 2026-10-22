using Rtok.Process;

namespace Rtok.Hooks;

/// <summary>
/// A SessionEnd the locked store turned away, handed to a detached <c>rtok hook SessionEnd</c>
/// that waits for the lock (rtok <c>defer_session_end</c>). A detached child gets no stdin, so
/// the payload goes through a spool file in <c>&lt;home&gt;/run</c>, named in
/// <see cref="Variable"/>; the child reads and deletes it.
/// </summary>
public static class HookDeferral
{
    /// <summary>Set in the deferred child: the spool file it reads its payload from.</summary>
    public const string Variable = "RTOK_HOOK_DEFERRED";

    private const string Prefix = "session-end-";

    /// <summary>Spools <paramref name="payload"/> and starts the child; never throws.</summary>
    /// <param name="home">rtok's home; the child runs with it as <c>RTOK_HOME</c>.</param>
    /// <param name="executable">The <c>rtok</c> to run.</param>
    /// <param name="payload">The Claude-shaped SessionEnd payload.</param>
    /// <returns>True when the child started.</returns>
    public static bool Defer(string home, string? executable, ReadOnlySpan<byte> payload)
    {
        if (string.IsNullOrEmpty(executable))
        {
            return false;
        }
        var path = Path.Combine(home, "run", $"{Prefix}{Guid.NewGuid():N}.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var create = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
            };
            if (!OperatingSystem.IsWindows())
            {
                create.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }
            using (var file = new FileStream(path, create))
            {
                file.Write(payload);
            }
            // The payload is already Claude-shaped, so the child needs no --host.
            using var child = ProgressRunner
                .Quiet(executable, ["hook", "SessionEnd"])
                .Environment(Variable, path)
                .Environment("RTOK_HOME", home)
                .Output(OutputMode.Detached)
                .Spawn();
            return true;
        }
#pragma warning disable CA1031 // Fail open: the hook answers {} whatever happens here.
        catch (Exception)
#pragma warning restore CA1031
        {
            TryDelete(path);
            return false;
        }
    }

    /// <summary>The payload spooled for this process, deleted once read; null when this is no
    /// deferred child. Only a spool file in <c>&lt;home&gt;/run</c> is read, so the variable
    /// cannot point the hook at any other file.</summary>
    /// <param name="home">rtok's home.</param>
    /// <param name="value">The value of <see cref="Variable"/>.</param>
    public static byte[]? Take(string home, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        var run = Path.GetFullPath(Path.Combine(home, "run"));
        var path = Path.GetFullPath(value);
        if (
            !string.Equals(Path.GetDirectoryName(path), run, StringComparison.Ordinal)
            || !Path.GetFileName(path).StartsWith(Prefix, StringComparison.Ordinal)
            || !File.Exists(path)
        )
        {
            return null;
        }
        try
        {
            return File.ReadAllBytes(path);
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
