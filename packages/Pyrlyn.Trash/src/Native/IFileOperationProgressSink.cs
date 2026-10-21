using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Pyrlyn.Trash.Native;

/// <summary>shobjidl_core.h <c>IFileOperationProgressSink</c>, every slot in vtable order.</summary>
[GeneratedComInterface]
[Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2")]
internal partial interface IFileOperationProgressSink
{
    void StartOperations();

    void FinishOperations(int hrResult);

    void PreRenameItem(uint dwFlags, nint psiItem, nint pszNewName);

    void PostRenameItem(
        uint dwFlags,
        nint psiItem,
        nint pszNewName,
        int hrRename,
        nint psiNewlyCreated
    );

    void PreMoveItem(uint dwFlags, nint psiItem, nint psiDestinationFolder, nint pszNewName);

    void PostMoveItem(
        uint dwFlags,
        nint psiItem,
        nint psiDestinationFolder,
        nint pszNewName,
        int hrMove,
        nint psiNewlyCreated
    );

    void PreCopyItem(uint dwFlags, nint psiItem, nint psiDestinationFolder, nint pszNewName);

    void PostCopyItem(
        uint dwFlags,
        nint psiItem,
        nint psiDestinationFolder,
        nint pszNewName,
        int hrCopy,
        nint psiNewlyCreated
    );

    /// <summary>Returning a failure HRESULT cancels the delete.</summary>
    [PreserveSig]
    int PreDeleteItem(uint dwFlags, nint psiItem);

    void PostDeleteItem(uint dwFlags, nint psiItem, int hrDelete, IShellItem? psiNewlyCreated);

    void PreNewItem(uint dwFlags, nint psiDestinationFolder, nint pszNewName);

    void PostNewItem(
        uint dwFlags,
        nint psiDestinationFolder,
        nint pszNewName,
        nint pszTemplateName,
        uint dwFileAttributes,
        int hrNew,
        nint psiNewItem
    );

    void UpdateProgress(uint iWorkTotal, uint iWorkSoFar);

    void ResetTimer();

    void PauseTimer();

    void ResumeTimer();
}
