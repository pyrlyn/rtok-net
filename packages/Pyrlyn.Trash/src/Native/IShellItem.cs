using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Pyrlyn.Trash.Native;

/// <summary>shobjidl_core.h <c>IShellItem</c>; only <c>GetDisplayName</c> is called.</summary>
[GeneratedComInterface]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal partial interface IShellItem
{
    nint BindToHandler(nint pbc, in Guid bhid, in Guid riid);

    IShellItem GetParent();

    /// <summary>Returns a CoTaskMem string the caller frees.</summary>
    nint GetDisplayName(uint sigdnName);

    uint GetAttributes(uint sfgaoMask);

    int Compare(IShellItem psi, uint hint);
}
