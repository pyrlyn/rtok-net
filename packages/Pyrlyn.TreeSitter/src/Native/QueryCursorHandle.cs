using Microsoft.Win32.SafeHandles;

namespace Pyrlyn.TreeSitter.Native;

internal sealed class QueryCursorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public QueryCursorHandle()
        : base(ownsHandle: true) => SetHandle(NativeMethods.QueryCursorNew());

    protected override bool ReleaseHandle()
    {
        NativeMethods.QueryCursorDelete(handle);
        return true;
    }
}
