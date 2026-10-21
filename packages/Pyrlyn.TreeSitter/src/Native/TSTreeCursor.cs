using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct TSTreeCursor
{
    public nint Tree;
    public nint Id;
    public uint Context0;
    public uint Context1;
    public uint Context2;
}
