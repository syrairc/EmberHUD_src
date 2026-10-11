using System.Runtime.InteropServices;

namespace Aember.Setup;

internal static class FileDialog
{
    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    class FileOpenDialogClass
    {
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct FilterSpec
    {
        public string Name;
        public string Spec;
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(nint context, ref Guid handler, ref Guid riid, out nint result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint form, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOpenDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] filters);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem folder);
        void SetFolder(IShellItem folder);
        void GetFolder(out IShellItem folder);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, nint context, ref Guid riid, out IShellItem item);

    public static string? PickFolder(nint owner, string start) => Pick(owner, start, "Game folder", 0x20 | 0x40, []);

    public static string? PickProgram(nint owner, string start) => Pick(owner, start, "Program", 0x40 | 0x1000, [new FilterSpec { Name = "Programs", Spec = "*.exe" }, new FilterSpec { Name = "All files", Spec = "*.*" }]);

    static string? Pick(nint owner, string start, string title, uint options, FilterSpec[] filters)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogClass();
        try
        {
            dialog.SetOptions(options);
            dialog.SetTitle(title);
            if (filters.Length > 0) dialog.SetFileTypes((uint)filters.Length, filters);
            var folder = Directory.Exists(start) ? start : Path.GetDirectoryName(start);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                var riid = typeof(IShellItem).GUID;
                SHCreateItemFromParsingName(folder, 0, ref riid, out var item);
                dialog.SetFolder(item);
            }
            if (dialog.Show(owner) != 0) return null;
            dialog.GetResult(out var result);
            result.GetDisplayName(0x80058000, out var path);
            return path;
        }
        finally
        {
            Marshal.FinalReleaseComObject(dialog);
        }
    }
}
