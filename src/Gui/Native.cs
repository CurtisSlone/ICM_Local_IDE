// Native.cs - small Win32 interop the GUI uses for native look: explorer-themed tree glyphs,
// real system file/folder icons (SHGetFileInfo), and the modern folder picker (IFileOpenDialog).
// GUI-only (build.ps1 keeps it out of the console exe).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Icm
{
    internal static class Native
    {
        // ----- explorer visual style (modern chevron glyphs on the dark tree) -----

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        public static void UseExplorerTheme(Control c)
        {
            try { if (c != null && c.IsHandleCreated) SetWindowTheme(c.Handle, "explorer", null); }
            catch { }
        }

        // ----- the modern folder picker (IFileOpenDialog with FOS_PICKFOLDERS) -----

        private const uint FOS_PICKFOLDERS = 0x20;
        private const uint FOS_FORCEFILESYSTEM = 0x40;
        private const uint SIGDN_FILESYSPATH = 0x80058000;
        private const int ERROR_CANCELLED = unchecked((int)0x800704C7);

        // Returns the chosen folder path, null if the user cancelled. Throws on COM failure so the
        // caller can fall back to the classic dialog.
        public static string PickFolder(IntPtr owner, string initial)
        {
            IFileOpenDialog dlg = (IFileOpenDialog)new FileOpenDialogRCW();
            try
            {
                uint opts; dlg.GetOptions(out opts);
                dlg.SetOptions(opts | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
                if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
                {
                    try
                    {
                        Guid iid = typeof(IShellItem).GUID;
                        IShellItem si;
                        SHCreateItemFromParsingName(initial, IntPtr.Zero, ref iid, out si);
                        if (si != null) dlg.SetFolder(si);
                    }
                    catch { /* best-effort start folder */ }
                }
                int hr = dlg.Show(owner);
                if (hr == ERROR_CANCELLED) return null;
                if (hr != 0) { Marshal.ThrowExceptionForHR(hr); }
                IShellItem result; dlg.GetResult(out result);
                string path; result.GetDisplayName(SIGDN_FILESYSPATH, out path);
                return path;
            }
            finally { Marshal.ReleaseComObject(dlg); }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc,
            [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRCW { }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        // Full vtable order: IModalWindow -> IFileDialog -> IFileOpenDialog. Unused methods are
        // declared (correctly sized) only to keep the layout right; we call a handful.
        [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);                 // IModalWindow
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);                    // IFileOpenDialog
            void GetSelectedItems(out IntPtr ppsai);
        }
    }

    // Builds a small ImageList of real system icons (folder + per-extension), for the file tree.
    internal sealed class IconProvider
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x100, SHGFI_SMALLICON = 0x1, SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10, FILE_ATTRIBUTE_NORMAL = 0x80;

        public ImageList Images = new ImageList();
        private readonly Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public IconProvider()
        {
            Images.ColorDepth = ColorDepth.Depth32Bit;
            Images.ImageSize = new Size(16, 16);
        }

        public int Folder() { return Get("dir::", "folder", FILE_ATTRIBUTE_DIRECTORY); }

        public int File(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) ext = "(noext)";
            return Get("ext:" + ext, "name" + ext, FILE_ATTRIBUTE_NORMAL);
        }

        private int Get(string key, string sample, uint attr)
        {
            int idx;
            if (map.TryGetValue(key, out idx)) return idx;
            var info = new SHFILEINFO();
            IntPtr r = SHGetFileInfo(sample, attr, ref info, (uint)Marshal.SizeOf(info),
                SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
            if (r == IntPtr.Zero || info.hIcon == IntPtr.Zero) { map[key] = -1; return -1; }
            try { using (Icon ic = Icon.FromHandle(info.hIcon)) using (Bitmap bmp = ic.ToBitmap()) Images.Images.Add(bmp); }
            catch { map[key] = -1; return -1; }
            finally { DestroyIcon(info.hIcon); }
            idx = Images.Images.Count - 1; map[key] = idx; return idx;
        }
    }
}
