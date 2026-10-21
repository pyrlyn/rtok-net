using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Pyrlyn.FsKit.Native;

/// <summary>
/// libc calls .NET does not expose: <c>lstat</c> for blocks, device and inode, <c>realpath</c>,
/// macOS <c>getattrlist</c> for clone families, and <c>fsync</c> of a directory.
/// </summary>
internal static unsafe partial class Unix
{
    private const string Libc = "libc";

    public const int EPERM = 1;
    public const int ENOENT = 2;
    public const int EACCES = 13;
    public const int ENOTDIR = 20;

    private static readonly int ELOOP = OperatingSystem.IsMacOS() ? 62 : 40;

    private static readonly Layout? StatLayout = LayoutForThisPlatform();

    // Big enough for every struct stat FsKit reads (144 bytes at most) and both PATH_MAXs.
    private const int StatBuffer = 256;
    private const int PathMax = 4096;

    static Unix()
    {
        NativeLibrary.SetDllImportResolver(typeof(Unix).Assembly, Resolve);
    }

    /// <summary>Whether FsKit knows this platform's <c>struct stat</c>.</summary>
    public static bool Supported => StatLayout is not null;

    /// <summary>
    /// "libc" names glibc's <c>libc.so.6</c> on Linux and libSystem on macOS; musl answers to the
    /// runtime's default probing.
    /// </summary>
    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (!string.Equals(name, Libc, StringComparison.Ordinal))
        {
            return 0;
        }
        if (OperatingSystem.IsMacOS())
        {
            return NativeLibrary.Load("/usr/lib/libSystem.B.dylib");
        }
        return NativeLibrary.TryLoad("libc.so.6", out var handle) ? handle : 0;
    }

    public static bool TryLStat(string path, out Stat stat, out int error)
    {
        var layout = StatLayout ?? throw new PlatformNotSupportedException();
        var buffer = stackalloc byte[StatBuffer];
        if (LStatCall(path, buffer) != 0)
        {
            error = Marshal.GetLastPInvokeError();
            stat = default;
            return false;
        }
        error = 0;
        stat = layout.Read(new ReadOnlySpan<byte>(buffer, StatBuffer));
        return true;
    }

    private static bool lstatMissing;

    private static int LStatCall(string path, byte* buffer)
    {
        if (OperatingSystem.IsMacOS())
        {
            // x64 keeps the 32-bit inode struct under the plain name.
            return RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? LStatInode64(path, buffer)
                : LStat(path, buffer);
        }
        if (!lstatMissing)
        {
            try
            {
                return LStat(path, buffer);
            }
            catch (EntryPointNotFoundException)
            {
                // glibc before 2.33 exports only the versioned __lxstat64.
                lstatMissing = true;
            }
        }
        var version = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 1 : 0;
        return LxStat64(version, path, buffer);
    }

    public static string? TryRealPath(string path, out int error)
    {
        var buffer = stackalloc byte[PathMax];
        var result =
            OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? RealPathExtended(path, buffer)
                : RealPath(path, buffer);
        if (result == null)
        {
            error = Marshal.GetLastPInvokeError();
            return null;
        }
        error = 0;
        return Marshal.PtrToStringUTF8((nint)buffer);
    }

    /// <summary>The APFS clone family of a regular file; false off macOS or off APFS.</summary>
    public static bool TryCloneInfo(string path, out CloneInfo info)
    {
        info = default;
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }
        const uint ReturnedAttrs = 0x80000000;
        const uint PrivateSize = 0x00000008;
        const uint CloneId = 0x00000100;
        const uint CloneRefCount = 0x00001000;
        const uint NoFollow = 0x1;
        const uint Extended = 0x20;

        // struct attrlist: u16 bitmapcount, u16 reserved, then five u32 attribute groups.
        var request = stackalloc uint[6];
        request[0] = 5;
        request[1] = ReturnedAttrs;
        request[5] = PrivateSize | CloneId | CloneRefCount;
        var buffer = stackalloc byte[64];
        if (GetAttrList(path, request, buffer, 64, NoFollow | Extended) != 0)
        {
            return false;
        }
        // u32 length, attribute_set_t (five u32) of what came back, then the values in bit order.
        var span = new ReadOnlySpan<byte>(buffer, 64);
        var returned = BinaryPrimitives.ReadUInt32LittleEndian(span[20..]);
        var at = 24;
        long privateSize = -1;
        if ((returned & PrivateSize) != 0)
        {
            privateSize = BinaryPrimitives.ReadInt64LittleEndian(span[at..]);
            at += 8;
        }
        if ((returned & CloneId) == 0 || privateSize < 0)
        {
            return false;
        }
        var cloneId = BinaryPrimitives.ReadUInt64LittleEndian(span[at..]);
        at += 8;
        // Before macOS 13 there is no reference count; every file then reads as a possible clone.
        var refCount =
            (returned & CloneRefCount) != 0
                ? BinaryPrimitives.ReadUInt32LittleEndian(span[at..])
                : uint.MaxValue;
        info = new CloneInfo(cloneId, refCount, privateSize);
        return true;
    }

    /// <summary>Fsync a directory so a rename inside it survives a crash; best effort.</summary>
    public static void SyncDirectory(string dir)
    {
        var cloexec = OperatingSystem.IsMacOS() ? 0x01000000 : 0x80000;
        var fd = Open(dir, cloexec);
        if (fd < 0)
        {
            return;
        }
        _ = FSync(fd);
        _ = Close(fd);
    }

    public static Exception Exception(int error, string path)
    {
        if (error == ENOENT || error == ENOTDIR)
        {
            return new FileNotFoundException($"Could not find '{path}'.", path);
        }
        if (error == EACCES || error == EPERM)
        {
            return new UnauthorizedAccessException($"Access to the path '{path}' is denied.");
        }
        if (error == ELOOP)
        {
            return new IOException($"{path}: too many levels of symbolic links");
        }
        return new IOException($"{path}: {Marshal.GetPInvokeErrorMessage(error)}");
    }

    /// <summary>Byte offsets of the fields FsKit reads in this platform's <c>struct stat</c>.</summary>
    private sealed record Layout(
        int Dev,
        bool Dev32,
        int Ino,
        int Nlink,
        int NlinkSize,
        int Mode,
        int ModeSize,
        int Size,
        int Blocks
    )
    {
        public Stat Read(ReadOnlySpan<byte> b)
        {
            var dev = Dev32
                ? BinaryPrimitives.ReadUInt32LittleEndian(b[Dev..])
                : BinaryPrimitives.ReadUInt64LittleEndian(b[Dev..]);
            var nlink = NlinkSize switch
            {
                2 => BinaryPrimitives.ReadUInt16LittleEndian(b[Nlink..]),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(b[Nlink..]),
                _ => BinaryPrimitives.ReadUInt64LittleEndian(b[Nlink..]),
            };
            var mode =
                ModeSize == 2
                    ? BinaryPrimitives.ReadUInt16LittleEndian(b[Mode..])
                    : BinaryPrimitives.ReadUInt32LittleEndian(b[Mode..]);
            var kind = (mode & 0xF000) switch
            {
                0x8000 => StatKind.File,
                0x4000 => StatKind.Directory,
                0xA000 => StatKind.Symlink,
                _ => StatKind.Other,
            };
            return new Stat(
                dev,
                BinaryPrimitives.ReadUInt64LittleEndian(b[Ino..]),
                nlink,
                kind,
                BinaryPrimitives.ReadInt64LittleEndian(b[Size..]),
                BinaryPrimitives.ReadInt64LittleEndian(b[Blocks..]) * 512
            );
        }
    }

    private static Layout? LayoutForThisPlatform()
    {
        if (!BitConverter.IsLittleEndian)
        {
            return null;
        }
        var arch = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsMacOS() && arch is Architecture.Arm64 or Architecture.X64)
        {
            // The 64-bit-inode struct stat: i32 dev, u16 mode, u16 nlink, u64 ino, ... size@96, blocks@104.
            return new Layout(0, true, 8, 6, 2, 4, 2, 96, 104);
        }
        if (OperatingSystem.IsLinux())
        {
            return arch switch
            {
                // u64 dev, u64 ino, u64 nlink, u32 mode, ... size@48, blocks@64.
                Architecture.X64 => new Layout(0, false, 8, 16, 8, 24, 4, 48, 64),
                // The generic layout: u64 dev, u64 ino, u32 mode, u32 nlink, ... size@48, blocks@64.
                Architecture.Arm64 or Architecture.RiscV64 or Architecture.LoongArch64 =>
                    new Layout(0, false, 8, 20, 4, 16, 4, 48, 64),
                _ => null,
            };
        }
        return null;
    }

    [LibraryImport(
        Libc,
        EntryPoint = "lstat",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    private static partial int LStat(string path, byte* buffer);

    [LibraryImport(
        Libc,
        EntryPoint = "lstat$INODE64",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    private static partial int LStatInode64(string path, byte* buffer);

    [LibraryImport(
        Libc,
        EntryPoint = "__lxstat64",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    private static partial int LxStat64(int version, string path, byte* buffer);

    [LibraryImport(
        Libc,
        EntryPoint = "realpath",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    private static partial byte* RealPath(string path, byte* resolved);

    [LibraryImport(
        Libc,
        EntryPoint = "realpath$DARWIN_EXTSN",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    private static partial byte* RealPathExtended(string path, byte* resolved);

    [LibraryImport(
        Libc,
        EntryPoint = "getattrlist",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    private static partial int GetAttrList(
        string path,
        void* request,
        void* buffer,
        nuint size,
        uint options
    );

    // open(2) is variadic; FsKit never passes the mode argument, so the fixed part is all there is.
    [LibraryImport(
        Libc,
        EntryPoint = "open",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    private static partial int Open(string path, int flags);

    [LibraryImport(Libc, EntryPoint = "fsync", SetLastError = true)]
    private static partial int FSync(int fd);

    [LibraryImport(Libc, EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int fd);
}
