using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Pyrlyn.Trash.Native.ObjC;

namespace Pyrlyn.Trash;

/// <summary><c>[[NSFileManager defaultManager] trashItemAtURL:resultingItemURL:error:]</c>, the call Finder makes.</summary>
[SupportedOSPlatform("macos")]
internal static class MacTrash
{
    public static unsafe string? Move(string full)
    {
        var pool = AutoreleasePoolPush();
        try
        {
            var url = Send(Class("NSURL"), Selector("fileURLWithPath:"), NSString(full));
            var manager = Send(Class("NSFileManager"), Selector("defaultManager"));
            nint resulting = 0;
            nint error = 0;
            var trash = (delegate* unmanaged<nint, nint, nint, nint*, nint*, byte>)MsgSend;
            if (
                trash(
                    manager,
                    Selector("trashItemAtURL:resultingItemURL:error:"),
                    url,
                    &resulting,
                    &error
                ) == 0
            )
            {
                var reason =
                    error == 0
                        ? "unknown error"
                        : String(Send(error, Selector("localizedDescription")));
                throw new TrashException($"{full}: {reason}");
            }
            return resulting == 0 ? null : String(Send(resulting, Selector("path")));
        }
        finally
        {
            AutoreleasePoolPop(pool);
        }
    }

    private static unsafe nint NSString(string s)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(s);
        try
        {
            return Send(Class("NSString"), Selector("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    private static string? String(nint nsString) =>
        nsString == 0 ? null : Marshal.PtrToStringUTF8(Send(nsString, Selector("UTF8String")));
}
