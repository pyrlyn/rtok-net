using Rtok.Hook.Contracts;

namespace Rtok.Hook;

/// <summary>What the client does after it printed the no-op reply without a resident (N8).</summary>
internal static class Fallback
{
    /// <summary>A client spawns the resident at most once per this interval (<c>spawn.lock</c>).</summary>
    private static readonly TimeSpan SpawnInterval = TimeSpan.FromSeconds(2);

    /// <summary>Queue the request for the resident's replay and start the resident.</summary>
    public static void QueueAndSpawn(HookEndpoint endpoint, byte[] body)
    {
        if (FindRtok() is not { } rtok)
        {
            return;
        }
        HookBacklog.Enqueue(endpoint, body);
        var touched = File.GetLastWriteTimeUtc(endpoint.SpawnLockPath);
        if (DateTime.UtcNow - touched < SpawnInterval)
        {
            return;
        }
        File.WriteAllBytes(endpoint.SpawnLockPath, []);
        Spawn.Detached([rtok, "hook", "--serve"], stdinFile: null);
    }

    /// <summary>
    /// Hand the request to a detached one-shot <c>rtok hook</c>, which inherits this process's
    /// environment and cwd, so its writes land as this client would have them.
    /// </summary>
    public static void RunOnce(HookRequest request)
    {
        // Windows passes no stdin to a detached child yet (T52).
        if (OperatingSystem.IsWindows() || FindRtok() is not { } rtok)
        {
            return;
        }
        List<string> argv = [rtok, "hook"];
        if (request.Event.Length > 0)
        {
            argv.Add(request.Event);
        }
        if (request.Host.Length > 0)
        {
            argv.AddRange(["--host", request.Host]);
        }
        var stdin = Path.Join(Path.GetTempPath(), $"rtok-hook-{Guid.CreateVersion7():N}");
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        };
        try
        {
            using (var file = new FileStream(stdin, options))
            {
                file.Write(request.Stdin.Span);
            }
            // The child has the file open as fd 0 once the spawn returns.
            Spawn.Detached([.. argv], stdin);
        }
        finally
        {
            File.Delete(stdin);
        }
    }

    // The rtok beside this client's real path, where the install keeps the pair (.NET resolves
    // ProcessPath on Unix; a Windows link is resolved here). Never a PATH lookup: another rtok
    // there may be another build.
    private static string? FindRtok()
    {
        if (Environment.ProcessPath is not { } exe)
        {
            return null;
        }
        var name = OperatingSystem.IsWindows() ? "rtok.exe" : "rtok";
        string?[] dirs =
        [
            Path.GetDirectoryName(exe),
            File.ResolveLinkTarget(exe, returnFinalTarget: true) is { } target
                ? Path.GetDirectoryName(target.FullName)
                : null,
        ];
        return dirs.OfType<string>().Select(d => Path.Join(d, name)).FirstOrDefault(File.Exists);
    }
}
