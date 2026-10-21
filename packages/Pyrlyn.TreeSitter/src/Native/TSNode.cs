using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct TSNode
{
    public uint Context0;
    public uint Context1;
    public uint Context2;
    public uint Context3;
    public nint Id;
    public nint Tree;
}
