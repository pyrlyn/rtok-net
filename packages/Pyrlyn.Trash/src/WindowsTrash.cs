using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Pyrlyn.Trash.Native;

namespace Pyrlyn.Trash;

/// <summary>
/// <c>IFileOperation.DeleteItem</c> with <c>FOFX_RECYCLEONDELETE</c> and no UI, the call
/// Explorer makes. The shell wants a single-threaded apartment, so it runs on a thread of its own.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsTrash
{
    // FOF_SILENT | FOF_NOCONFIRMATION | FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR | FOF_NOERRORUI,
    // FOFX_RECYCLEONDELETE and FOFX_EARLYFAILURE.
    private const uint Flags = 0x0004 | 0x0010 | 0x0040 | 0x0200 | 0x0400 | 0x00080000 | 0x00100000;
    private const uint InProcServer = 0x1; // CLSCTX_INPROC_SERVER

    private static readonly Guid FileOperationClass = new(
        0x3ad05575,
        0x8857,
        0x4850,
        0x92,
        0x77,
        0x11,
        0xb8,
        0x5b,
        0xdb,
        0x8e,
        0x09
    );
    private static readonly Guid FileOperationId = new(
        0x947aab5f,
        0x0a5c,
        0x4c13,
        0xb4,
        0xd6,
        0x4b,
        0xf7,
        0x83,
        0x6f,
        0xc9,
        0xf8
    );
    private static readonly Guid ShellItemId = new(
        0x43826d1e,
        0xe718,
        0x42ee,
        0xbc,
        0x55,
        0xa1,
        0xe2,
        0x61,
        0xc3,
        0x7b,
        0xfe
    );
    private static readonly StrategyBasedComWrappers Wrappers = new();

    public static string? Move(string full)
    {
        string? result = null;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = MoveOnThisThread(full);
            }
            catch (Exception e)
            {
                failure = ExceptionDispatchInfo.Capture(e);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result;
    }

    private static string? MoveOnThisThread(string full)
    {
        var operation = Create<IFileOperation>(
            Shell32.CoCreateInstance(
                FileOperationClass,
                0,
                InProcServer,
                FileOperationId,
                out var op
            ),
            op,
            full
        );
        var item = Create<IShellItem>(
            Shell32.SHCreateItemFromParsingName(full, 0, ShellItemId, out var si),
            si,
            full
        );
        var sink = new RecycleSink();
        try
        {
            operation.SetOperationFlags(Flags);
            operation.DeleteItem(item, sink);
            operation.PerformOperations();
        }
        catch (COMException e)
        {
            throw Failed(full, sink, e.HResult, e);
        }
        if (
            sink.Refused
            || sink.Result < 0
            || operation.GetAnyOperationsAborted() != 0
            || Path.Exists(full)
        )
        {
            throw Failed(full, sink, sink.Result, null);
        }
        return sink.Recycled;
    }

    private static T Create<T>(int hresult, nint pointer, string full)
        where T : class
    {
        if (hresult < 0)
        {
            throw new TrashException($"{full}: {Marshal.GetPInvokeErrorMessage(hresult)}", hresult);
        }
        try
        {
            return (T)
                Wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    private static TrashException Failed(
        string full,
        RecycleSink sink,
        int hresult,
        Exception? inner
    )
    {
        var message = sink.Refused
            ? $"{full}: the Recycle Bin cannot take it; not deleted"
            : $"{full}: {Marshal.GetPInvokeErrorMessage(hresult)}";
        return inner is null
            ? new TrashException(message, hresult)
            : new TrashException(message, inner);
    }
}
