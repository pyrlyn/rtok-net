// hook-client <socket> <fallback> <rtok> [args...]
// Connects to the resident first. Connected: stdin goes over the socket as one length-prefixed
// frame and the reply is printed. Not connected, by <fallback>:
//   noop  print the no-op reply and spawn "<rtok> [args...]" detached (candidate a);
//   queue same, but first write stdin to <socket dir>/queue/ for the resident to replay;
//   exec  execv "<rtok> [args...]" with stdin still unread (candidate b).
// Probe modes (T4 only):
//   --probe-t0 <ticks> <log>             append (now - ticks) in microseconds: exec to Main
//   --probe-launch <n> <log> <exe> [args] spawn exe n times, passing --probe-t0; print p50/p95
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;

if (args is ["--probe-t0", var t0, var t0Log])
{
    var us = Stopwatch.GetElapsedTime(long.Parse(t0)).TotalMicroseconds;
    File.AppendAllText(t0Log, $"{us:F0}\n");
    return 0;
}
if (args is ["--probe-launch", var count, var log, ..])
    return Posix.Launch(int.Parse(count), log, args[3..]);

const int MaxInput = 8 << 20;
const int WaitMs = 50;
var noop = "{}"u8;

using var stdout = Console.OpenStandardOutput();
try
{
    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    try
    {
        socket.Connect(new UnixDomainSocketEndPoint(args[0]));
    }
    catch (SocketException)
    {
        socket.Dispose();
        Fallback(args);
        stdout.Write(noop);
        return 0;
    }
    socket.ReceiveTimeout = WaitMs;
    socket.SendTimeout = WaitMs;
    var body = ReadStdin();
    if (body is null)
    {
        stdout.Write(noop);
        return 0;
    }
    var head = new byte[4];
    BinaryPrimitives.WriteInt32LittleEndian(head, body.Length);
    socket.Send(head);
    socket.Send(body);
    ReadExactly(socket, head);
    var reply = new byte[BinaryPrimitives.ReadInt32LittleEndian(head)];
    ReadExactly(socket, reply);
    stdout.Write(reply);
    socket.Dispose();
}
catch (Exception)
{
    stdout.Write(noop);
}
return 0;

static byte[]? ReadStdin()
{
    using var stdin = Console.OpenStandardInput();
    var buf = new byte[64 << 10];
    var len = 0;
    while (true)
    {
        if (len == buf.Length)
        {
            if (buf.Length >= MaxInput)
                return null;
            Array.Resize(ref buf, Math.Min(buf.Length * 2, MaxInput));
        }
        var n = stdin.Read(buf, len, buf.Length - len);
        if (n == 0)
            return buf[..len];
        len += n;
    }
}

static void ReadExactly(Socket s, byte[] buf)
{
    var off = 0;
    while (off < buf.Length)
    {
        var n = s.Receive(buf, off, buf.Length - off, SocketFlags.None);
        if (n == 0)
            throw new IOException("closed");
        off += n;
    }
}

static void Fallback(string[] args)
{
    var argv = args[2..];
    if (args[1] == "exec")
        Posix.Exec(argv);
    if (args[1] == "queue" && ReadStdin() is { } body)
    {
        var dir = Path.Combine(Path.GetDirectoryName(args[0])!, "queue");
        Directory.CreateDirectory(
            dir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
        File.WriteAllBytes(Path.Combine(dir, $"{Guid.CreateVersion7():N}.json"), body);
    }
    Posix.SpawnDetached(argv);
}

static partial class Posix
{
    const int O_RDWR = 2;

    public static void Exec(string[] argv)
    {
        var p = Argv(argv);
        execv(argv[0], p);
        // execv returned: it failed. The caller prints the no-op reply.
    }

    // posix_spawn with stdio on /dev/null and a new session, so the host's pipes are not held.
    public static unsafe void SpawnDetached(string[] argv)
    {
        var osx = OperatingSystem.IsMacOS();
        // posix_spawn_file_actions_t / posix_spawnattr_t: a pointer on macOS, a struct on glibc.
        var fa = stackalloc byte[512];
        var at = stackalloc byte[512];
        posix_spawn_file_actions_init(fa);
        posix_spawnattr_init(at);
        for (var fd = 0; fd < 3; fd++)
            posix_spawn_file_actions_addopen(fa, fd, "/dev/null", O_RDWR, 0);
        // POSIX_SPAWN_SETSID (+ POSIX_SPAWN_CLOEXEC_DEFAULT on macOS: no other fd is inherited).
        short flags = osx ? (short)(0x0400 | 0x4000) : (short)0x80;
        posix_spawnattr_setflags(at, flags);
        posix_spawn(out _, argv[0], fa, at, Argv(argv), environ());
        posix_spawn_file_actions_destroy(fa);
        posix_spawnattr_destroy(at);
    }

    static string?[] Argv(string[] argv) => [.. argv, null];

    // Spawn argv n times with "--probe-t0 <now> <log>" and wait for each; print exec-to-Main
    // (from the child's log) and spawn-to-exit percentiles in milliseconds.
    public static unsafe int Launch(int n, string log, string[] argv)
    {
        File.Delete(log);
        var total = new List<double>();
        for (var i = 0; i < n + 10; i++)
        {
            var t0 = Stopwatch.GetTimestamp();
            string[] a =
            [
                .. argv,
                "--probe-t0",
                t0.ToString(System.Globalization.CultureInfo.InvariantCulture),
                log,
            ];
            if (posix_spawn(out var pid, a[0], null, null, Argv(a), environ()) != 0)
                return 1;
            waitpid(pid, out _, 0);
            if (i >= 10)
                total.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }
        var main = File.ReadAllLines(log).Skip(10).Select(l => double.Parse(l) / 1000).ToList();
        Console.Out.WriteLine(
            $"{Pct(main, 50):F2} {Pct(main, 95):F2} {Pct(total, 50):F2} {Pct(total, 95):F2}"
        );
        return 0;
    }

    static double Pct(List<double> v, int p)
    {
        v.Sort();
        return v[(v.Count - 1) * p / 100];
    }

    [LibraryImport("libc")]
    private static partial int waitpid(int pid, out int status, int options);

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int execv(string path, string?[] argv);

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static unsafe partial int posix_spawn(
        out int pid,
        string path,
        void* fileActions,
        void* attr,
        string?[] argv,
        nint envp
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

    // "libc" is libc.dylib on macOS; on Linux it must be the versioned glibc soname.
    static Posix() =>
        NativeLibrary.SetDllImportResolver(
            typeof(Posix).Assembly,
            (name, _, _) =>
                name == "libc" && OperatingSystem.IsLinux() ? NativeLibrary.Load("libc.so.6") : 0
        );

    // The environment block: _NSGetEnviron on macOS, the environ symbol on Linux.
    static unsafe nint environ() =>
        OperatingSystem.IsMacOS()
            ? *_NSGetEnviron()
            : *(nint*)NativeLibrary.GetExport(NativeLibrary.Load("libc.so.6"), "environ");

    [LibraryImport("libc")]
    private static unsafe partial nint* _NSGetEnviron();
}
