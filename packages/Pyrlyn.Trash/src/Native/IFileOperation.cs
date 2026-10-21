using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Pyrlyn.Trash.Native;

/// <summary>shobjidl_core.h <c>IFileOperation</c>, every slot in vtable order; unused arguments are raw pointers.</summary>
[GeneratedComInterface]
[Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8")]
internal partial interface IFileOperation
{
    uint Advise(IFileOperationProgressSink pfops);

    void Unadvise(uint dwCookie);

    void SetOperationFlags(uint dwOperationFlags);

    void SetProgressMessage(nint pszMessage);

    void SetProgressDialog(nint popd);

    void SetProperties(nint pproparray);

    void SetOwnerWindow(nint hwndOwner);

    void ApplyPropertiesToItem(nint psiItem);

    void ApplyPropertiesToItems(nint punkItems);

    void RenameItem(nint psiItem, nint pszNewName, nint pfopsItem);

    void RenameItems(nint pUnkItems, nint pszNewName);

    void MoveItem(nint psiItem, nint psiDestinationFolder, nint pszNewName, nint pfopsItem);

    void MoveItems(nint punkItems, nint psiDestinationFolder);

    void CopyItem(nint psiItem, nint psiDestinationFolder, nint pszCopyName, nint pfopsItem);

    void CopyItems(nint punkItems, nint psiDestinationFolder);

    void DeleteItem(IShellItem psiItem, IFileOperationProgressSink? pfopsItem);

    void DeleteItems(nint punkItems);

    void NewItem(
        nint psiDestinationFolder,
        uint dwFileAttributes,
        nint pszName,
        nint pszTemplateName,
        nint pfopsItem
    );

    void PerformOperations();

    int GetAnyOperationsAborted();
}
