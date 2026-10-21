using System.Collections;
using System.Runtime.InteropServices;
using System.Text;

namespace Rtok.Hook;

/// <summary>
/// Starts a process the client does not wait for: a new session, no inherited fds, stdio on
/// <c>/dev/null</c> (stdin on a file when given), so the host's pipes close when the client
/// exits. <c>System.Diagnostics.Process</c> would cost the client its start time.
/// </summary>
internal static partial class Spawn
{
    private const int ORdOnly = 0;
    private const int ORdWr = 2;

    public static void Detached(string[] argv, string? stdinFile)
    {
        if (OperatingSystem.IsWindows())
        {
            Windows(argv);
            return;
        }
        Posix(argv, stdinFile);
    }

    private static unsafe void Posix(string[] argv, string? stdinFile)
    {
        // posix_spawn_file_actions_t and posix_spawnattr_t: a pointer on macOS, a struct on glibc.
        var fa = stackalloc byte[512];
        var at = stackalloc byte[512];
        if (posix_spawn_file_actions_init(fa) != 0 || posix_spawnattr_init(at) != 0)
        {
            return;
        }
        _ = posix_spawn_file_actions_addopen(
            fa,
            0,
            stdinFile ?? "/dev/null",
            stdinFile is null ? ORdWr : ORdOnly,
            0
        );
        _ = posix_spawn_file_actions_addopen(fa, 1, "/dev/null", ORdWr, 0);
        _ = posix_spawn_file_actions_addopen(fa, 2, "/dev/null", ORdWr, 0);
        // POSIX_SPAWN_SETSID, and on macOS POSIX_SPAWN_CLOEXEC_DEFAULT: no other fd is inherited
        // (.NET opens its own with O_CLOEXEC on Linux).
        var flags = OperatingSystem.IsMacOS() ? (short)(0x0400 | 0x4000) : (short)0x80;
        _ = posix_spawnattr_setflags(at, flags);
        _ = posix_spawn(out _, argv[0], fa, at, [.. argv, null], Environ());
        _ = posix_spawn_file_actions_destroy(fa);
        _ = posix_spawnattr_destroy(at);
    }

    private static string?[] Environ()
    {
        var env = new List<string?>();
        foreach (DictionaryEntry e in Environment.GetEnvironmentVariables())
        {
            env.Add($"{e.Key}={e.Value}");
        }
        env.Add(null);
        return [.. env];
    }

    // Windows: CreateProcess with no window, a new process group and no inherited handles.
    private static unsafe void Windows(string[] argv)
    {
        const uint detachedProcess = 0x00000008;
        const uint createNewProcessGroup = 0x00000200;
        var line = new StringBuilder();
        foreach (var arg in argv)
        {
            line.Append(line.Length == 0 ? "" : " ")
                .Append('"')
                .Append(arg.Replace("\"", "\\\"", StringComparison.Ordinal))
                .Append('"');
        }
        var si = new StartupInfo { Cb = sizeof(StartupInfo) };
        // CreateProcessW may write into the command line, so it gets its own buffer.
        var buffer = (line.ToString() + "\0").ToCharArray();
        fixed (char* commandLine = buffer)
        {
            if (
                !CreateProcessW(
                    argv[0],
                    commandLine,
                    0,
                    0,
                    false,
                    detachedProcess | createNewProcessGroup,
                    0,
                    null,
                    &si,
                    out var pi
                )
            )
            {
                return;
            }
            CloseHandle(pi.Process);
            CloseHandle(pi.Thread);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Cb;
        public nint Reserved;
        public nint Desktop;
        public nint Title;
        public int X,
            Y,
            XSize,
            YSize,
            XCountChars,
            YCountChars,
            FillAttribute,
            Flags;
        public short ShowWindow,
            Reserved2;
        public nint Reserved2Ptr,
            StdInput,
            StdOutput,
            StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint Process;
        public nint Thread;
        public int ProcessId;
        public int ThreadId;
    }

    [LibraryImport(
        "kernel32",
        EntryPoint = "CreateProcessW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool CreateProcessW(
        string application,
        char* commandLine,
        nint processAttributes,
        nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        nint environment,
        string? currentDirectory,
        StartupInfo* startupInfo,
        out ProcessInformation processInformation
    );

    [LibraryImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static unsafe partial int posix_spawn(
        out int pid,
        string path,
        void* fileActions,
        void* attr,
        string?[] argv,
        string?[] envp
    );

    [LibraryImport("libc")]
    private static unsafe partial int posix_spawn_file_actions_init(void* fa);

    [LibraryImport("libc")]
    private static unsafe partial int posix_spawn_file_actions_destroy(void* fa);

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static unsafe partial int posix_spawn_file_actions_addopen(
        void* fa,
        int fd,
        string path,
        int oflag,
        int mode
    );

    [LibraryImport("libc")]
    private static unsafe partial int posix_spawnattr_init(void* at);

    [LibraryImport("libc")]
    private static unsafe partial int posix_spawnattr_destroy(void* at);

    [LibraryImport("libc")]
    private static unsafe partial int posix_spawnattr_setflags(void* at, short flags);

    // "libc" is libc.dylib on macOS; on glibc it must be the versioned soname (musl finds libc.so).
#pragma warning disable CA1810 // The resolver must be set before the first call.
    static Spawn() =>
        NativeLibrary.SetDllImportResolver(
            typeof(Spawn).Assembly,
            (name, _, _) =>
                string.Equals(name, "libc", StringComparison.Ordinal)
                && OperatingSystem.IsLinux()
                && NativeLibrary.TryLoad("libc.so.6", out var h)
                    ? h
                    : 0
        );
#pragma warning restore CA1810
}
