using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct TSQueryCapture
{
    public TSNode Node;
    public uint Index;
}
