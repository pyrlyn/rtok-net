using System.Buffers.Binary;
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
        if (TakeSpawnTurn(endpoint))
        {
            Spawn.Detached([rtok, "hook", "--serve"], stdinFile: null);
        }
    }

    // Whether this client starts the resident: the last start (UTC ticks in spawn.lock) is over
    // SpawnInterval ago. FileShare.None is an flock on Unix, so of parallel clients one decides
    // and the others, failing to open it, leave the start to it.
    private static bool TakeSpawnTurn(HookEndpoint endpoint)
    {
        try
        {
            using var gate = new FileStream(
                endpoint.SpawnLockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None
            );
            Span<byte> last = stackalloc byte[sizeof(long)];
            if (
                gate.ReadAtLeast(last, last.Length, throwOnEndOfStream: false) == last.Length
                && DateTime.UtcNow.Ticks - BinaryPrimitives.ReadInt64LittleEndian(last)
                    < SpawnInterval.Ticks
            )
            {
                return false;
            }
            BinaryPrimitives.WriteInt64LittleEndian(last, DateTime.UtcNow.Ticks);
            gate.Position = 0;
            gate.Write(last);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
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
