namespace PhotoPresenter.Services;

// Folder picker that uses IFileOpenDialog COM directly — without FOS_FORCEFILESYSTEM —
// so that MTP devices (Android phones connected via USB) appear alongside local folders.
// WPF's built-in OpenFolderDialog always sets FOS_FORCEFILESYSTEM, hiding MTP devices.

internal static class ShellFolderPicker
{
    /// <summary>
    /// Shows a folder picker dialog. Returns the filesystem path when a normal folder is
    /// selected, or null when an MTP (phone) folder is selected. Returns (null, null) if
    /// the user cancels. The IShellItem is valid for the lifetime of the COM apartment.
    /// </summary>
    internal static (string? fsPath, IShellItem? shellItem) PickFolder(nint ownerHwnd, string title)
    {
        IFileOpenDialog dialog = ShellInterop.CreateFileOpenDialog();
        try
        {
            // Get current options, then remove FOS_FORCEFILESYSTEM so MTP devices appear.
            dialog.GetOptions(out uint fos);
            dialog.SetOptions((fos | ShellInterop.FOS_PICKFOLDERS) & ~ShellInterop.FOS_FORCEFILESYSTEM);
            dialog.SetTitle(title);

            int hr = dialog.Show(ownerHwnd);
            if (hr != ShellInterop.S_OK)
                return (null, null); // user cancelled (HRESULT_FROM_WIN32(ERROR_CANCELLED) = 0x800704C7)

            dialog.GetResult(out IShellItem item);

            // Try to get a Win32 filesystem path. MTP items return E_INVALIDARG here.
            int pathHr = item.GetDisplayName(ShellInterop.SIGDN_FILESYSPATH, out string fsPath);
            if (pathHr == ShellInterop.S_OK && !string.IsNullOrEmpty(fsPath))
                return (fsPath, null); // normal filesystem folder — caller uses File.Copy etc.

            return (null, item); // MTP or other non-filesystem destination
        }
        catch
        {
            return (null, null);
        }
    }
}
