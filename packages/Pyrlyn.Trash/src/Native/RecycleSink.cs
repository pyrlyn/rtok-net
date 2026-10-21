using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Pyrlyn.Trash.Native;

/// <summary>
/// Refuses any delete the shell would not send to the Recycle Bin (too big, no bin on the
/// drive), so nothing is destroyed for good, and keeps where the recycled item went.
/// </summary>
[GeneratedComClass]
internal sealed partial class RecycleSink : IFileOperationProgressSink
{
    private const uint RecycleIfPossible = 0x80; // TSF_DELETE_RECYCLE_IF_POSSIBLE
    private const uint FileSystemPath = 0x80058000; // SIGDN_FILESYSPATH
    private const int Abort = unchecked((int)0x80004004); // E_ABORT

    public bool Refused { get; private set; }

    public int Result { get; private set; }

    public string? Recycled { get; private set; }

    public int PreDeleteItem(uint dwFlags, nint psiItem)
    {
        if ((dwFlags & RecycleIfPossible) != 0)
        {
            return 0;
        }
        Refused = true;
        return Abort;
    }

    public void PostDeleteItem(
        uint dwFlags,
        nint psiItem,
        int hrDelete,
        IShellItem? psiNewlyCreated
    )
    {
        Result = hrDelete;
        if (hrDelete >= 0 && psiNewlyCreated is not null)
        {
            var name = psiNewlyCreated.GetDisplayName(FileSystemPath);
            Recycled = Marshal.PtrToStringUni(name);
            Marshal.FreeCoTaskMem(name);
        }
    }

    public void StartOperations() { }

    public void FinishOperations(int hrResult) { }

    public void PreRenameItem(uint dwFlags, nint psiItem, nint pszNewName) { }

    public void PostRenameItem(
        uint dwFlags,
        nint psiItem,
        nint pszNewName,
        int hrRename,
        nint psiNewlyCreated
    ) { }

    public void PreMoveItem(
        uint dwFlags,
        nint psiItem,
        nint psiDestinationFolder,
        nint pszNewName
    ) { }

    public void PostMoveItem(
        uint dwFlags,
        nint psiItem,
        nint psiDestinationFolder,
        nint pszNewName,
        int hrMove,
        nint psiNewlyCreated
    ) { }

    public void PreCopyItem(
        uint dwFlags,
        nint psiItem,
        nint psiDestinationFolder,
        nint pszNewName
    ) { }

    public void PostCopyItem(
        uint dwFlags,
        nint psiItem,
        nint psiDestinationFolder,
        nint pszNewName,
        int hrCopy,
        nint psiNewlyCreated
    ) { }

    public void PreNewItem(uint dwFlags, nint psiDestinationFolder, nint pszNewName) { }

    public void PostNewItem(
        uint dwFlags,
        nint psiDestinationFolder,
        nint pszNewName,
        nint pszTemplateName,
        uint dwFileAttributes,
        int hrNew,
        nint psiNewItem
    ) { }

    public void UpdateProgress(uint iWorkTotal, uint iWorkSoFar) { }

    public void ResetTimer() { }

    public void PauseTimer() { }

    public void ResumeTimer() { }
}
