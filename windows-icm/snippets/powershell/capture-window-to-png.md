<!--icm
{
  "id": "capture-window-to-png",
  "title": "Capture a window to a PNG (PrintWindow)",
  "doc_type": "snippet",
  "group": "powershell",
  "summary": "Render any top-level window (e.g. a WinForms form under test) straight to a PNG via user32 PrintWindow with PW_RENDERFULLCONTENT - works even when the window is occluded, off-screen, or hidden from normal screen capture.",
  "keywords": ["PrintWindow", "PW_RENDERFULLCONTENT", "screenshot", "WinForms", "capture", "Bitmap", "user32", "headless", "GDI"],
  "source": { "origin": "authored", "note": "used to capture a generated WinForms app for verification; verified in use" }
}
-->
# Capture a window to a PNG (PrintWindow)

`PrintWindow` asks a window to paint itself into a device context, so you get a clean image of the
whole window regardless of z-order, occlusion, or which monitor it is on - unlike a screen grab. Flag
`PW_RENDERFULLCONTENT (2)` captures DWM/modern content correctly.

```powershell
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace U -Name W -MemberDefinition '[DllImport("user32.dll")] public static extern bool PrintWindow(System.IntPtr h, System.IntPtr dc, uint f);'

function Save-WindowPng([System.IntPtr] $hwnd, [int] $w, [int] $h, [string] $path) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [void][U.W]::PrintWindow($hwnd, $hdc, 2)   # 2 = PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# usage with a WinForms form you created/showed:
#   Save-WindowPng $form.Handle $form.Width $form.Height "$env:TEMP\shot.png"
```

## Notes
- The window must have been shown at least once (pump messages first:
  `[System.Windows.Forms.Application]::DoEvents()` after `$form.Show()`).
- `CopyFromScreen` is the alternative but grabs raw screen pixels, so it is thrown off by DPI scaling
  and anything overlapping the window; `PrintWindow` avoids both.
