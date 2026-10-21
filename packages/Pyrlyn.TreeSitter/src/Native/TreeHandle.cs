using Microsoft.Win32.SafeHandles;

namespace Pyrlyn.TreeSitter.Native;

internal sealed class TreeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public TreeHandle(nint tree)
        : base(ownsHandle: true) => SetHandle(tree);

    protected override bool ReleaseHandle()
    {
        NativeMethods.TreeDelete(handle);
        return true;
    }
}
