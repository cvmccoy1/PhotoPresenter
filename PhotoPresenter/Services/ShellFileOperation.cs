namespace PhotoPresenter.Services;

// File operations via IFileOperation COM, which works in the shell namespace and therefore
// handles MTP devices (Android phones) transparently. Used by ExportProgressDialog when the
// user selects a phone folder as the export destination.

internal static class ShellFileOperation
{
    private const uint CopyFlags = ShellInterop.FOF_SILENT
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
        op.SetOperationFlags(CopyFlags);
        op.CopyItem(srcItem, destFolder, destFileName, nint.Zero);

        int hr = op.PerformOperations();
        if (hr != ShellInterop.S_OK)
            throw new System.IO.IOException($"Shell copy failed (0x{hr:X8}): {srcPath} → {destFileName}");
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
            op.SetOperationFlags(CopyFlags);
            op.MoveItem(tmpItem, destFolder, destFileName, nint.Zero);

            int hr = op.PerformOperations();
            if (hr != ShellInterop.S_OK)
                throw new System.IO.IOException($"Shell write failed (0x{hr:X8}): {destFileName}");
        }
        finally
        {
            // PerformOperations moves (not copies) the temp file; delete only if still present.
            if (File.Exists(tmp))
                try { File.Delete(tmp); } catch { }
        }
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
            throw new System.IO.IOException($"SHCreateItemFromParsingName failed (0x{hr:X8}): {path}");
        return item;
    }
}
