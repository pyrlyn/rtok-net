using Microsoft.Win32.SafeHandles;

namespace Pyrlyn.TreeSitter.Native;

internal sealed class QueryHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public QueryHandle(nint query)
        : base(ownsHandle: true) => SetHandle(query);

    protected override bool ReleaseHandle()
    {
        NativeMethods.QueryDelete(handle);
        return true;
    }
}
