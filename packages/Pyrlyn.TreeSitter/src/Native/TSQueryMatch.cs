using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct TSQueryMatch
{
    public uint Id;
    public ushort PatternIndex;
    public ushort CaptureCount;
    public TSQueryCapture* Captures;
}
