using Microsoft.Win32.SafeHandles;

namespace Pyrlyn.TreeSitter.Native;

internal sealed class ParserHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public ParserHandle()
        : base(ownsHandle: true) => SetHandle(NativeMethods.ParserNew());

    protected override bool ReleaseHandle()
    {
        NativeMethods.ParserDelete(handle);
        return true;
    }
}
