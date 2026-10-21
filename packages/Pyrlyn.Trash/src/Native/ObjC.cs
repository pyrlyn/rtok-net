using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pyrlyn.Trash.Native;

/// <summary>The Objective-C runtime calls MacTrash needs; Foundation is loaded for its classes.</summary>
[SupportedOSPlatform("macos")]
internal static unsafe partial class ObjC
{
    private const string Lib = "/usr/lib/libobjc.A.dylib";

    private static readonly nint Foundation = NativeLibrary.Load(
        "/System/Library/Frameworks/Foundation.framework/Foundation"
    );

    /// <summary><c>objc_msgSend</c>, cast to the exact signature at each call (arm64 needs that).</summary>
    public static readonly nint MsgSend = NativeLibrary.GetExport(
        NativeLibrary.Load(Lib),
        "objc_msgSend"
    );

    public static nint Class(string name) =>
        Foundation != 0 ? GetClass(name) : throw new PlatformNotSupportedException("no Foundation");

    public static nint Selector(string name) => RegisterName(name);

    public static nint Send(nint receiver, nint selector) =>
        ((delegate* unmanaged<nint, nint, nint>)MsgSend)(receiver, selector);

    public static nint Send(nint receiver, nint selector, nint arg) =>
        ((delegate* unmanaged<nint, nint, nint, nint>)MsgSend)(receiver, selector, arg);

    [LibraryImport(Lib, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClass(string name);

    [LibraryImport(
        Lib,
        EntryPoint = "sel_registerName",
        StringMarshalling = StringMarshalling.Utf8
    )]
    private static partial nint RegisterName(string name);

    [LibraryImport(Lib, EntryPoint = "objc_autoreleasePoolPush")]
    public static partial nint AutoreleasePoolPush();

    [LibraryImport(Lib, EntryPoint = "objc_autoreleasePoolPop")]
    public static partial void AutoreleasePoolPop(nint pool);
}
