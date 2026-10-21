using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct TSPoint
{
    public uint Row;
    public uint Column;
}
