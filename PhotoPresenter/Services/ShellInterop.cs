using System.Runtime.InteropServices;

namespace PhotoPresenter.Services;

// COM/P/Invoke declarations for shell-namespace-aware folder picking and file operations.
// These interfaces work with MTP devices (Android phones) which have no Win32 filesystem path
// and therefore cannot be accessed via File.Copy / Directory.GetFiles / File.WriteAllText.

internal static class ShellInterop
{
    internal const uint FOS_PICKFOLDERS     = 0x00000020;
    internal const uint FOS_FORCEFILESYSTEM = 0x00000040; // removed to expose MTP devices
    internal const uint FOF_SILENT          = 0x0004;
    internal const uint FOF_NOCONFIRMATION  = 0x0010;
    internal const uint FOF_NOERRORUI       = 0x0400;
    internal const uint SIGDN_NORMALDISPLAY = 0x00000000;
    internal const uint SIGDN_FILESYSPATH   = 0x80058000;
    internal const int  S_OK               = 0;

    internal static readonly Guid CLSID_FileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
    internal static readonly Guid CLSID_FileOperation  = new("3AD05575-8857-4850-9277-11B85BDB8E09");
    internal static          Guid IID_IShellItem       = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHCreateItemFromParsingName(
        string pszPath, nint pbc, ref Guid riid, out IShellItem ppv);

    internal static IFileOpenDialog CreateFileOpenDialog()
        => (IFileOpenDialog)Activator.CreateInstance(
               Type.GetTypeFromCLSID(CLSID_FileOpenDialog)!)!;

    internal static IFileOperation CreateFileOperation()
        => (IFileOperation)Activator.CreateInstance(
               Type.GetTypeFromCLSID(CLSID_FileOperation)!)!;
}

// ── COM interfaces ────────────────────────────────────────────────────────────
// Unused vtable slots are declared as void stubs (wrong signature is safe as long
// as they are never called — they just occupy the correct vtable positions).

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler_(nint pbc, ref Guid bhid, ref Guid riid, out nint ppv);
    void GetParent_(out IShellItem ppsi);
    // Returns S_OK with a CoTaskMem-allocated string, or E_INVALIDARG for MTP items
    // when SIGDN_FILESYSPATH is requested (they have no Win32 path).
    [PreserveSig] int GetDisplayName(uint sigdnName,
        [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes_(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare_(IShellItem psi, uint hint, out int piOrder);
}

// IFileOpenDialog vtable order: IUnknown (handled by runtime) → IModalWindow.Show →
// IFileDialog (SetFileTypes … SetFilter) → IFileOpenDialog (GetResults, GetSelectedItems).
[ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    // IModalWindow
    [PreserveSig] int Show(nint hwnd);
    // IFileDialog — stubs for unused slots
    void SetFileTypes_(uint n, nint specs);
    void SetFileTypeIndex_(uint i);
    void GetFileTypeIndex_(out uint pi);
    void Advise_(nint pfde, out uint pdwCookie);
    void Unadvise_(uint dwCookie);
    void SetOptions(uint fos);
    void GetOptions(out uint fos);
    void SetDefaultFolder_(nint psi);
    void SetFolder_(nint psi);
    void GetFolder_(out nint ppsi);
    void GetCurrentSelection_(out nint ppsi);
    void SetFileName_([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetFileName_(out nint ppszName);
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
    void SetOkButtonLabel_(nint pszText);
    void SetFileNameLabel_(nint pszLabel);
    void GetResult(out IShellItem ppsi);
    void AddPlace_(nint psi, uint fdap);
    void SetDefaultExtension_(nint pszExt);
    void Close_(int hr);
    void SetClientGuid_(ref Guid guid);
    void ClearClientData_();
    void SetFilter_(nint pFilter);
    // IFileOpenDialog
    void GetResults_(out nint ppenum);
    void GetSelectedItems_(out nint ppsai);
}

// IFileOperation vtable order: IUnknown → Advise, Unadvise, SetOperationFlags,
// SetProgressMessage, SetProgressDialog, SetProperties, SetOwnerWindow,
// ApplyPropertiesToItem, ApplyPropertiesToItems, RenameItem, RenameItems,
// MoveItem, MoveItems, CopyItem, CopyItems, DeleteItem, DeleteItems,
// NewItem, PerformOperations, GetAnyOperationsAborted.
[ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperation
{
    void Advise_(nint pfops, out uint pdwCookie);
    void Unadvise_(uint dwCookie);
    void SetOperationFlags(uint dwOperationFlags);
    void SetProgressMessage_(nint pszMessage);
    void SetProgressDialog_(nint popd);
    void SetProperties_(nint pproparray);
    void SetOwnerWindow_(nint hwndOwner);
    void ApplyPropertiesToItem_(IShellItem psiItem);
    void ApplyPropertiesToItems_(nint punkItems);
    void RenameItem_(IShellItem psiItem,
        [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, nint pfopsItem);
    void RenameItems_(nint pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
    void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder,
        [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, nint pfopsItem);
    void MoveItems_(nint punkItems, IShellItem psiDestinationFolder);
    void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder,
        [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName, nint pfopsItem);
    void CopyItems_(nint punkItems, IShellItem psiDestinationFolder);
    void DeleteItem(IShellItem psiItem, nint pfopsItem);
    void DeleteItems_(nint punkItems);
    void NewItem_(IShellItem psiDestFolder, uint dwFileAttribs,
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, nint pfopsItem);
    [PreserveSig] int PerformOperations();
    void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool pfAnyOperationsAborted);
}
