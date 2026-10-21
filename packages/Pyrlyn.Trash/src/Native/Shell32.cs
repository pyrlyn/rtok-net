using System.Runtime.InteropServices;

namespace Pyrlyn.Trash.Native;

/// <summary>The two flat calls that make the COM objects.</summary>
internal static partial class Shell32
{
    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(
        in Guid rclsid,
        nint pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out nint ppv
    );

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SHCreateItemFromParsingName(
        string pszPath,
        nint pbc,
        in Guid riid,
        out nint ppv
    );
}
