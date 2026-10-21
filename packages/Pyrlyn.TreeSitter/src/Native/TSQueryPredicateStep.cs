using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct TSQueryPredicateStep
{
    public uint Type;
    public uint ValueId;
}
