using System.Runtime.InteropServices;

namespace PhotoPresenter.Services;

// COM/P/Invoke declarations for shell-namespace-aware folder picking and file operations.
// These interfaces work with MTP devices (Android phones) which have no Win32 filesystem path
// and therefore cannot be accessed via File.Copy / Directory.GetFiles / File.WriteAllText.

internal static class ShellInterop
{
    // IFileOpenDialog option flags
    internal const uint FOS_PICKFOLDERS     = 0x00000020;
    internal const uint FOS_FORCEFILESYSTEM = 0x00000040; // removed to expose MTP devices

    // IFileOperation operation flags
    internal const uint FOF_SILENT          = 0x0004;
    internal const uint FOF_NOCONFIRMATION  = 0x0010;
    internal const uint FOF_NOERRORUI       = 0x0400;

    // IShellItem display name forms
    internal const uint SIGDN_NORMALDISPLAY          = 0x00000000;
    internal const uint SIGDN_PARENTRELATIVEPARSING  = 0x80018001; // filename relative to parent
    internal const uint SIGDN_FILESYSPATH            = 0x80058000; // E_INVALIDARG for MTP items

    // IShellFolder.EnumObjects flags
    internal const uint SHCONTF_NONFOLDERS   = 0x0040;
    internal const uint SHCONTF_INCLUDEHIDDEN = 0x0080;

    internal const int S_OK = 0;

    // CLSIDs for CoCreateInstance via Activator
    internal static readonly Guid CLSID_FileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
    internal static readonly Guid CLSID_FileOperation  = new("3AD05575-8857-4850-9277-11B85BDB8E09");

    // IIDs used in BindToHandler / SHCreateItem* calls
    internal static Guid IID_IShellItem    = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    internal static Guid IID_IShellItem2   = new("7E9FB0D3-919F-4307-AB2E-9B1860310C93");
    internal static Guid IID_IShellFolder  = new("000214E6-0000-0000-C000-000000000046");

    // Binding handler ID for obtaining IShellFolder from IShellItem.BindToHandler
    internal static readonly Guid BHID_SFObject = new("3981E227-F559-11D3-8E3A-00C04F6837D5");

    // PKEY_Size — used to read a file's byte-size from IShellItem2
    internal static readonly PROPERTYKEY PKEY_Size =
        new(new Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), 12);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHCreateItemFromParsingName(
        string pszPath, nint pbc, ref Guid riid, out IShellItem ppv);

    // Creates an IShellItem2 for a child PIDL relative to a parent IShellFolder.
    // Used when enumerating an MTP folder via IEnumIDList.
    [DllImport("shell32.dll")]
    internal static extern int SHCreateItemWithParent(
        nint pidlParent, IShellFolder psfParent, nint pidl,
        ref Guid riid, out IShellItem2 ppvItem);

    internal static IFileOpenDialog CreateFileOpenDialog()
        => (IFileOpenDialog)Activator.CreateInstance(
               Type.GetTypeFromCLSID(CLSID_FileOpenDialog)!)!;

    internal static IFileOperation CreateFileOperation()
        => (IFileOperation)Activator.CreateInstance(
               Type.GetTypeFromCLSID(CLSID_FileOperation)!)!;
}

// ── Structs ───────────────────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
internal struct PROPERTYKEY
{
    internal Guid  fmtid;
    internal uint  pid;
    internal PROPERTYKEY(Guid fmtid, uint pid) { this.fmtid = fmtid; this.pid = pid; }
}

// ── COM interfaces ────────────────────────────────────────────────────────────
// Unused vtable slots are declared as void stubs (wrong signature is safe as long
// as they are never called — they just occupy the correct vtable positions).

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    // Slot 0: used to obtain IShellFolder for enumeration
    [PreserveSig] int BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, out nint ppv);
    void GetParent_(out IShellItem ppsi);
    // Returns S_OK with a CoTaskMem-allocated string, or E_INVALIDARG for MTP items
    // when SIGDN_FILESYSPATH is requested (they have no Win32 path).
    [PreserveSig] int GetDisplayName(uint sigdnName,
        [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes_(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare_(IShellItem psi, uint hint, out int piOrder);
}

// IShellItem2 inherits IShellItem and adds typed property access.
// Used to read PKEY_Size (byte count) from enumerated MTP file items.
// Vtable: IShellItem slots (5) then IShellItem2 slots (11).
[ComImport, Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem2
{
    // ── IShellItem slots ──
    [PreserveSig] int BindToHandler_(nint pbc, ref Guid bhid, ref Guid riid, out nint ppv);
    void GetParent_(out IShellItem2 ppsi);
    [PreserveSig] int GetDisplayName(uint sigdnName,
        [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes_(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare_(IShellItem2 psi, uint hint, out int piOrder);
    // ── IShellItem2 slots ──
    void GetPropertyStore_(uint flags, ref Guid riid, out nint ppv);
    void GetPropertyStoreWithCreateObject_(uint flags, nint punkCreateObject, ref Guid riid, out nint ppv);
    void GetPropertyStoreForKeys_(nint rgKeys, uint cKeys, uint flags, ref Guid riid, out nint ppv);
    void GetProperty_(ref PROPERTYKEY key, out nint ppropvar);
    void GetCLSID_(ref PROPERTYKEY key, out Guid pclsid);
    void GetFileTime_(ref PROPERTYKEY key, out long pft);
    void GetInt32_(ref PROPERTYKEY key, out int pi);
    void GetString_(ref PROPERTYKEY key, out nint ppsz);
    void GetUInt32_(ref PROPERTYKEY key, out uint pui);
    [PreserveSig] int GetUInt64(ref PROPERTYKEY key, out ulong pull);
    void GetBool_(ref PROPERTYKEY key, out int pf);
}

// IShellFolder — used to enumerate children of a folder in the shell namespace (incl. MTP).
// Vtable: ParseDisplayName, EnumObjects, BindToObject, BindToStorage, CompareIDs,
//         CreateViewObject, GetAttributesOf, GetUIObjectOf, GetDisplayNameOf, SetNameOf.
[ComImport, Guid("000214E6-0000-0000-C000-000000000046"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellFolder
{
    void ParseDisplayName_(nint hwnd, nint pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
        out uint pchEaten, out nint ppidl, ref uint pdwAttributes);
    [PreserveSig] int EnumObjects(nint hwnd, uint grfFlags, out IEnumIDList ppenumIDList);
    void BindToObject_(nint pidl, nint pbc, ref Guid riid, out nint ppv);
    void BindToStorage_(nint pidl, nint pbc, ref Guid riid, out nint ppv);
    void CompareIDs_(nint lParam, nint pidl1, nint pidl2);
    void CreateViewObject_(nint hwndOwner, ref Guid riid, out nint ppv);
    void GetAttributesOf_(uint cidl, nint apidl, ref uint rgfInOut);
    void GetUIObjectOf_(nint hwndOwner, uint cidl, nint apidl, ref Guid riid, out uint rgfReserved, out nint ppv);
    void GetDisplayNameOf_(nint pidl, uint uFlags, out nint pName);
    void SetNameOf_(nint hwnd, nint pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        uint uFlags, out nint ppidlOut);
}

// IEnumIDList — returned by IShellFolder.EnumObjects; yields child PIDLs one at a time.
[ComImport, Guid("000214F2-0000-0000-C000-000000000046"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumIDList
{
    // rgelt receives a single CoTask-allocated ITEMIDLIST pointer when celt=1.
    [PreserveSig] int Next(uint celt, out nint rgelt, out uint pceltFetched);
    [PreserveSig] int Skip(uint celt);
    void Reset();
    void Clone_(out nint ppenum);
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
