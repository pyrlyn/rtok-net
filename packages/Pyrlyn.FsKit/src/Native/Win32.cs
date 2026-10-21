using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Pyrlyn.FsKit.Native;

/// <summary>
/// The handle queries .NET does not expose: volume serial, file index, link count, allocation
/// size and the final path of a file.
/// </summary>
internal static unsafe partial class Win32
{
    private const string Kernel32 = "kernel32.dll";

    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorCantResolveFilename = 1921;

    private const uint ShareAll = 0x1 | 0x2 | 0x4;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint AttributeDirectory = 0x10;
    private const uint AttributeReparsePoint = 0x400;
    private const int FileStandardInfoClass = 1;

    public static bool TryLStat(string path, out Stat stat, out int error)
    {
        stat = default;
        using var handle = Open(path, follow: false, out error);
        if (handle is null)
        {
            return false;
        }
        if (
            !GetFileInformationByHandle(handle, out var info)
            || !GetFileInformationByHandleEx(
                handle,
                FileStandardInfoClass,
                out var standard,
                (uint)sizeof(FileStandardInfo)
            )
        )
        {
            error = Marshal.GetLastPInvokeError();
            return false;
        }
        var kind =
            (info.Attributes & AttributeReparsePoint) != 0
                ? (
                    (info.Attributes & AttributeDirectory) != 0 || IsSymlink(path)
                        ? StatKind.Symlink
                        : StatKind.File
                )
            : (info.Attributes & AttributeDirectory) != 0 ? StatKind.Directory
            : StatKind.File;
        stat = new Stat(
            info.VolumeSerialNumber,
            ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow,
            info.NumberOfLinks,
            kind,
            standard.EndOfFile,
            standard.AllocationSize
        );
        return true;
    }

    /// <summary>A reparse point that is not a link (a cloud placeholder, a dedup stub) is the file itself.</summary>
    private static bool IsSymlink(string path) => new FileInfo(path).LinkTarget is not null;

    public static string? TryFinalPath(string path, out int error)
    {
        using var handle = Open(path, follow: true, out error);
        if (handle is null)
        {
            return null;
        }
        var size = 512u;
        while (true)
        {
            var buffer = new char[size];
            uint length;
            fixed (char* p = buffer)
            {
                length = GetFinalPathNameByHandle(handle, p, size, 0);
            }
            if (length == 0)
            {
                error = Marshal.GetLastPInvokeError();
                return null;
            }
            if (length < size)
            {
                return new string(buffer, 0, (int)length);
            }
            size = length + 1;
        }
    }

    private static SafeFileHandle? Open(string path, bool follow, out int error)
    {
        var flags = BackupSemantics | (follow ? 0 : OpenReparsePoint);
        var handle = CreateFile(path, 0, ShareAll, 0, OpenExisting, flags, 0);
        if (handle.IsInvalid)
        {
            error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            return null;
        }
        error = 0;
        return handle;
    }

    public static Exception Exception(int error, string path) =>
        error switch
        {
            ErrorFileNotFound or ErrorPathNotFound => new FileNotFoundException(
                $"Could not find '{path}'.",
                path
            ),
            ErrorAccessDenied => new UnauthorizedAccessException(
                $"Access to the path '{path}' is denied."
            ),
            ErrorCantResolveFilename => new IOException(
                $"{path}: too many levels of symbolic links"
            ),
            _ => new IOException($"{path}: {Marshal.GetPInvokeErrorMessage(error)}"),
        };

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint Attributes;
        public ulong CreationTime;
        public ulong LastAccessTime;
        public ulong LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileStandardInfo
    {
        public long AllocationSize;
        public long EndOfFile;
        public uint NumberOfLinks;
        public byte DeletePending;
        public byte Directory;
    }

    [LibraryImport(
        Kernel32,
        EntryPoint = "CreateFileW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true
    )]
    private static partial SafeFileHandle CreateFile(
        string name,
        uint access,
        uint share,
        nint security,
        uint disposition,
        uint flags,
        nint template
    );

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandle(
        SafeFileHandle handle,
        out ByHandleFileInformation info
    );

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandleEx(
        SafeFileHandle handle,
        int infoClass,
        out FileStandardInfo info,
        uint size
    );

    [LibraryImport(Kernel32, EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
    private static partial uint GetFinalPathNameByHandle(
        SafeFileHandle handle,
        char* buffer,
        uint size,
        uint flags
    );
}
