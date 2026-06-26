using System.Runtime.InteropServices;

namespace PhotoPresenter.Services;

// File operations via IFileOperation COM, which works in the shell namespace and therefore
// handles MTP devices (Android phones) transparently. Used by ExportProgressDialog when the
// user selects a phone folder as the export destination.

internal static class ShellFileOperation
{
    private const uint SilentFlags = ShellInterop.FOF_SILENT
                                   | ShellInterop.FOF_NOCONFIRMATION
                                   | ShellInterop.FOF_NOERRORUI;

    /// <summary>
    /// Copies a single file to a shell-namespace destination folder (e.g. a folder on an MTP device).
    /// The destination file is always overwritten if it already exists (FOF_NOCONFIRMATION).
    /// Throws on failure.
    /// </summary>
    internal static void CopyFile(string srcPath, IShellItem destFolder, string destFileName)
    {
        var srcItem = CreateItemFromPath(srcPath);
        var op = ShellInterop.CreateFileOperation();
        op.SetOperationFlags(SilentFlags);
        op.CopyItem(srcItem, destFolder, destFileName, nint.Zero);
        int hr = op.PerformOperations();
        if (hr != ShellInterop.S_OK)
            throw new IOException($"Shell copy failed (0x{hr:X8}): {srcPath} → {destFileName}");
    }

    /// <summary>
    /// Writes text content to a file in a shell-namespace destination folder.
    /// Writes to a temp file first, then moves it into the destination via IFileOperation.
    /// </summary>
    internal static void WriteTextFile(string content, IShellItem destFolder, string destFileName)
    {
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, content, System.Text.Encoding.UTF8);
            var tmpItem = CreateItemFromPath(tmp);
            var op = ShellInterop.CreateFileOperation();
            op.SetOperationFlags(SilentFlags);
            op.MoveItem(tmpItem, destFolder, destFileName, nint.Zero);
            int hr = op.PerformOperations();
            if (hr != ShellInterop.S_OK)
                throw new IOException($"Shell write failed (0x{hr:X8}): {destFileName}");
        }
        finally
        {
            // PerformOperations moves (not copies) the temp file; delete only if still present.
            if (File.Exists(tmp))
                try { File.Delete(tmp); } catch { }
        }
    }

    /// <summary>
    /// Deletes a single shell item (e.g. a file on an MTP device) silently via IFileOperation.
    /// </summary>
    internal static void DeleteShellItem(IShellItem item)
    {
        var op = ShellInterop.CreateFileOperation();
        op.SetOperationFlags(SilentFlags);
        op.DeleteItem(item, nint.Zero);
        op.PerformOperations(); // ignore return — deletion failures are non-fatal
    }

    /// <summary>
    /// Enumerates all non-folder children of a shell-namespace folder (including MTP folders).
    /// Returns a case-insensitive dictionary mapping filename → (byte size, IShellItem).
    /// Returns an empty dictionary on any error (e.g. empty/inaccessible destination).
    /// </summary>
    internal static Dictionary<string, (ulong Size, IShellItem Item)> EnumerateFolderContents(
        IShellItem folder)
    {
        var result = new Dictionary<string, (ulong, IShellItem)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Bind the IShellItem to its IShellFolder interface.
            var bhid = ShellInterop.BHID_SFObject;
            var iid  = ShellInterop.IID_IShellFolder;
            int hr = folder.BindToHandler(nint.Zero, ref bhid, ref iid, out nint ppv);
            if (hr != ShellInterop.S_OK || ppv == nint.Zero) return result;

            var shellFolder = (IShellFolder)Marshal.GetObjectForIUnknown(ppv);
            Marshal.Release(ppv);

            hr = shellFolder.EnumObjects(nint.Zero,
                ShellInterop.SHCONTF_NONFOLDERS | ShellInterop.SHCONTF_INCLUDEHIDDEN,
                out IEnumIDList enumIdList);
            if (hr != ShellInterop.S_OK || enumIdList == null) return result;

            while (true)
            {
                hr = enumIdList.Next(1, out nint pidl, out uint fetched);
                if (hr != ShellInterop.S_OK || fetched == 0 || pidl == nint.Zero) break;
                try
                {
                    var iid2 = ShellInterop.IID_IShellItem2;
                    hr = ShellInterop.SHCreateItemWithParent(nint.Zero, shellFolder, pidl,
                        ref iid2, out IShellItem2 child);
                    if (hr != ShellInterop.S_OK) continue;

                    // Get filename (parent-relative parsing name is reliable for MTP file items).
                    hr = child.GetDisplayName(ShellInterop.SIGDN_PARENTRELATIVEPARSING, out string name);
                    if (hr != ShellInterop.S_OK || string.IsNullOrEmpty(name)) continue;

                    // Get byte size (PKEY_Size); treat failure as size=0.
                    var pkey = ShellInterop.PKEY_Size;
                    child.GetUInt64(ref pkey, out ulong size); // ignore hr; size stays 0 on failure

                    // Cast IShellItem2 → IShellItem for the stored reference.
                    result[name] = (size, (IShellItem)child);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pidl);
                }
            }
        }
        catch { /* enumeration errors are non-fatal; return what was collected */ }
        return result;
    }

    /// <summary>
    /// Returns the human-readable display name of a shell item (e.g. "Internal shared storage").
    /// </summary>
    internal static string GetDisplayName(IShellItem item)
    {
        item.GetDisplayName(ShellInterop.SIGDN_NORMALDISPLAY, out string name);
        return name ?? string.Empty;
    }

    private static IShellItem CreateItemFromPath(string path)
    {
        int hr = ShellInterop.SHCreateItemFromParsingName(path, nint.Zero,
            ref ShellInterop.IID_IShellItem, out IShellItem item);
        if (hr != ShellInterop.S_OK)
            throw new IOException($"SHCreateItemFromParsingName failed (0x{hr:X8}): {path}");
        return item;
    }
}
