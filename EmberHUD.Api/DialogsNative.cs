using System.Runtime.InteropServices;

namespace EmberHUD;

internal static class NativeDialog
{
    const uint OverwritePrompt = 0x2, PickFolders = 0x20, ForceFileSystem = 0x40, AllowMultiselect = 0x200, PathMustExist = 0x800, FileMustExist = 0x1000;
    const uint FileSystemPath = 0x80058000;
    const int Cancelled = unchecked((int)0x800704C7);
    const uint ToolWindow = 0x80, Topmost = 0x8, Popup = 0x80000000;

    static Guid _shellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    public static string[] Run(DialogSpec spec)
    {
        var owner = CreateWindowExW(Topmost | ToolWindow, "STATIC", "", Popup, 0, 0, 0, 0, 0, 0, 0, 0);
        if (owner != 0) ShowWindow(owner, 4);
        try
        {
            var dialog = spec.Kind == DialogKind.SaveFile ? (IFileDialog)new FileSaveDialog() : (IFileDialog)new FileOpenDialog();
            try
            {
                Configure(dialog, spec);
                var hr = dialog.Show(owner);
                if (hr == Cancelled) return [];
                Marshal.ThrowExceptionForHR(hr);
                return Results(dialog, spec);
            }
            finally
            {
                Marshal.FinalReleaseComObject(dialog);
            }
        }
        finally
        {
            if (owner != 0) DestroyWindow(owner);
        }
    }

    static void Configure(IFileDialog dialog, DialogSpec spec)
    {
        dialog.GetOptions(out var options);
        options |= ForceFileSystem | PathMustExist;
        options |= spec.Kind switch
        {
            DialogKind.PickFolder => PickFolders,
            DialogKind.OpenFiles => AllowMultiselect | FileMustExist,
            DialogKind.SaveFile => OverwritePrompt,
            _ => FileMustExist,
        };
        dialog.SetOptions(options);
        dialog.SetTitle(spec.Title);

        if (spec.Kind != DialogKind.PickFolder)
        {
            var filters = spec.Filters.Count > 0 ? spec.Filters : [new DialogFilter("All files", "*.*")];
            dialog.SetFileTypes((uint)filters.Count, filters.Select(f => new FilterSpec(f.Name, f.Pattern)).ToArray());
            dialog.SetFileTypeIndex(1);
            if (spec.Kind == DialogKind.SaveFile && DefaultExtension(filters[0].Pattern) is { } extension) dialog.SetDefaultExtension(extension);
        }
        if (spec.FileName is { Length: > 0 }) dialog.SetFileName(spec.FileName);
        if (spec.StartFolder is { Length: > 0 } && Directory.Exists(spec.StartFolder))
        {
            SHCreateItemFromParsingName(spec.StartFolder, 0, ref _shellItem, out var folder);
            dialog.SetFolder(folder);
        }
    }

    static string? DefaultExtension(string pattern)
    {
        var first = pattern.Split(';')[0].Trim();
        var dot = first.LastIndexOf('.');
        return dot >= 0 && first[(dot + 1)..] is { Length: > 0 } extension && extension != "*" ? extension : null;
    }

    static string[] Results(IFileDialog dialog, DialogSpec spec)
    {
        if (spec.Kind != DialogKind.OpenFiles)
        {
            dialog.GetResult(out var item);
            return [PathOf(item)];
        }

        ((IFileOpenDialog)dialog).GetResults(out var items);
        items.GetCount(out var count);
        var paths = new string[count];
        for (uint i = 0; i < count; i++)
        {
            items.GetItemAt(i, out var item);
            paths[i] = PathOf(item);
        }
        return paths;
    }

    static string PathOf(IShellItem item)
    {
        item.GetDisplayName(FileSystemPath, out var path);
        return path;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct FilterSpec(string name, string spec)
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Name = name;
        [MarshalAs(UnmanagedType.LPWStr)] public string Spec = spec;
    }

    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    class FileOpenDialog;

    [ComImport, Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    class FileSaveDialog;

    [ComImport, Guid("b4db1657-70d7-485e-8e3e-6fcb5a5c1802"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IModalWindow
    {
        [PreserveSig] int Show(nint owner);
    }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileDialog : IModalWindow
    {
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] specs);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, uint placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(nint filter);
    }

    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOpenDialog : IFileDialog
    {
        void GetResults(out IShellItemArray items);
        void GetSelectedItems(out IShellItemArray items);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(nint context, ref Guid handler, ref Guid riid, out nint result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint form, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemArray
    {
        void BindToHandler(nint context, ref Guid handler, ref Guid riid, out nint result);
        void GetPropertyStore(int flags, ref Guid riid, out nint result);
        void GetPropertyDescriptionList(nint keyType, ref Guid riid, out nint result);
        void GetAttributes(int flags, uint mask, out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index, out IShellItem item);
        void EnumItems(out nint items);
    }

    [DllImport("shell32", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, nint context, ref Guid riid, out IShellItem item);

    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern nint CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32")]
    static extern bool ShowWindow(nint window, int command);

    [DllImport("user32")]
    static extern bool DestroyWindow(nint window);
}
