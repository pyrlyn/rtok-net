using System.Globalization;
using System.Text;

namespace Rtok.Hook.Contracts;

/// <summary>
/// Where one home's resident lives. Everything sits in the run directory <c>&lt;home&gt;/run</c>,
/// which is kept at mode 0700 (rtok S7): the socket, the resident's lock, the spawn rate limit
/// and the queue of requests the client wrote while no resident answered. On Windows the
/// address is a named pipe named by the home.
/// </summary>
public sealed class HookEndpoint
{
    // sun_path holds 104 bytes on macOS and 108 on Linux; leave room for the temporary name the
    // resident binds before it renames the socket into place.
    private const int MaxSocketPath = 96;

    private HookEndpoint(string home, string address)
    {
        Home = home;
        Address = address;
    }

    /// <summary>The home this endpoint belongs to.</summary>
    public string Home { get; }

    /// <summary>The Unix socket path, or the pipe name on Windows.</summary>
    public string Address { get; }

    /// <summary>The run directory, mode 0700.</summary>
    public string RunDir => Path.Join(Home, "run");

    /// <summary>Held exclusively by the one resident; it holds the resident's pid.</summary>
    public string LockPath => Path.Join(RunDir, "hook.lock");

    /// <summary>Touched when a client spawns the resident; a fresh one stops another spawn.</summary>
    public string SpawnLockPath => Path.Join(RunDir, "spawn.lock");

    /// <summary>Requests (<c>*.req</c>) the resident replays for their writes.</summary>
    public string QueueDir => Path.Join(RunDir, "queue");

    /// <summary>
    /// The endpoint of <paramref name="home"/>, or <see langword="null"/> when its socket path is
    /// too long for <c>sun_path</c>.
    /// </summary>
    public static HookEndpoint? For(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        if (OperatingSystem.IsWindows())
        {
            var id = HookFingerprint.Fnv(Encoding.UTF8.GetBytes(home));
            return new(home, "rtok-hook-" + id.ToString("x16", CultureInfo.InvariantCulture));
        }
        var socket = Path.Join(home, "run", "hook.sock");
        return Encoding.UTF8.GetByteCount(socket) <= MaxSocketPath ? new(home, socket) : null;
    }

    /// <summary>
    /// Creates <paramref name="dir"/> (the run directory or one inside it) at mode 0700, and
    /// forces that mode on one that exists: the umask never widens it (rtok S7).
    /// </summary>
    /// <exception cref="IOException"><paramref name="dir"/> is a symlink.</exception>
    public static void EnsurePrivateDir(string dir)
    {
        ArgumentNullException.ThrowIfNull(dir);
        var info = new DirectoryInfo(dir);
        if (info.LinkTarget is not null)
        {
            throw new IOException($"{dir} is a symlink");
        }
        if (OperatingSystem.IsWindows())
        {
            info.Create();
            return;
        }
        const UnixFileMode owner =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        if (!info.Exists)
        {
            Directory.CreateDirectory(dir, owner);
        }
        if (File.GetUnixFileMode(dir) != owner)
        {
            File.SetUnixFileMode(dir, owner);
        }
    }

    /// <summary>This process's endpoint, or <see langword="null"/> when its socket path is too long.</summary>
    public static HookEndpoint? Current() => For(RtokHome.Resolve());
}
