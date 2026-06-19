<!--icm
{
  "id": "ui-pinvoke",
  "title": "Win32 P/Invoke for WinForms polish",
  "doc_type": "reference",
  "summary": "P/Invoke signatures the host GUI actually uses (uxtheme SetWindowTheme, shell32 SHGetFileInfo + IFileOpenDialog folder picker, user32 DestroyIcon) plus common additions (dwmapi dark title bar, GetAsyncKeyState) - exact DllImport declarations for a WinForms tool.",
  "keywords": ["P/Invoke", "DllImport", "WinForms", "SetWindowTheme", "uxtheme", "SHGetFileInfo", "shell32", "IFileOpenDialog", "folder picker", "DestroyIcon", "DwmSetWindowAttribute", "dark title bar", "Marshal", "StructLayout"],
  "group": "win32",
  "source": { "origin": "authored", "repo_path": "win_icm_code/src/Gui/Native.cs", "note": "signatures marked (host) are verbatim from this repo's GUI and compile under the in-box csc; signatures marked (pinvoke.net) are common additions - verify against pinvoke.net before relying on them" }
}
-->
# Win32 P/Invoke for WinForms polish

P/Invoke lives in `System.Runtime.InteropServices` (in `mscorlib`, no extra reference). These are the
declarations the host's own GUI uses, plus a few common additions for native look and feel. All are
C# 5 compatible.

## In use by the host GUI (verbatim from `Native.cs`)

Explorer theming for a `TreeView`/`ListView` (modern chevrons, themed selection):
```csharp
[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);
// usage: if (ctrl.IsHandleCreated) SetWindowTheme(ctrl.Handle, "explorer", null);
```

Real system file/folder icons for a tree (`SHGetFileInfo` + `DestroyIcon`):
```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
private struct SHFILEINFO
{
    public IntPtr hIcon;
    public int iIcon;
    public uint dwAttributes;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]  public string szTypeName;
}

[DllImport("shell32.dll", CharSet = CharSet.Auto)]
private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
    ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

[DllImport("user32.dll", SetLastError = true)]
private static extern bool DestroyIcon(IntPtr hIcon);   // free the HICON SHGetFileInfo returns

private const uint SHGFI_ICON = 0x100, SHGFI_SMALLICON = 0x1, SHGFI_USEFILEATTRIBUTES = 0x10;
private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10, FILE_ATTRIBUTE_NORMAL = 0x80;
```

The modern folder picker uses the `IFileOpenDialog` COM interface with `FOS_PICKFOLDERS`; see
`Native.cs` in the host for the full `[ComImport]` vtable (declare the whole interface in vtable order,
call only what you need). Key rule: free COM objects with `Marshal.ReleaseComObject` in a `finally`,
and translate failures with `Marshal.ThrowExceptionForHR`.

## Common additions (pinvoke.net - verify before relying on them)

Dark title bar on Windows 10/11 (`dwmapi`):
```csharp
[DllImport("dwmapi.dll")]
private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
// DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (19 on older Win10); int value = 1 to enable.
// usage: int on = 1; DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int));
```

Window messages and z-order:
```csharp
[DllImport("user32.dll", CharSet = CharSet.Auto)]
private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

[DllImport("user32.dll", SetLastError = true)]
private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
```

Keyboard state (polling a key, e.g. a global-ish hotkey check):
```csharp
[DllImport("user32.dll")]
private static extern short GetAsyncKeyState(int vKey);
```

## Notes
- Mark `[DllImport(..., SetLastError = true)]` when you will call `Marshal.GetLastWin32Error()`.
- Prefer the managed API when one exists (WinForms `FolderBrowserDialog`, `ProcessModule`) - reach for
  P/Invoke only for what the BCL does not expose. For in-process keyboard shortcuts inside a form,
  override `ProcessCmdKey` rather than `GetAsyncKeyState`.
