<!--icm
{
  "id": "winforms-app",
  "title": "Minimal WinForms app (csc -t:winexe)",
  "doc_type": "scaffold",
  "group": "csharp",
  "summary": "A single-window WinForms app built with the in-box csc as -t:winexe: [STAThread] Main with EnableVisualStyles and Application.Run, a Form subclass with one control wired entirely in code (no designer).",
  "keywords": ["WinForms", "winexe", "STAThread", "Application.Run", "EnableVisualStyles", "Form", "Button", "no designer", "System.Windows.Forms", "System.Drawing", "C# 5"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc; verified to compile/parse" }
}
-->
# Minimal WinForms app (csc -t:winexe)

A single-window GUI with no console window. There is no designer here - the form and its one control
are built entirely in code, which is how the host's own GUI works. Compile with `-target:winexe` and
reference `System.Windows.Forms.dll` + `System.Drawing.dll` (from the GAC).

## Files

`Main.cs` - the entry point and a `Form` subclass with a button wired in the constructor.

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace App
{
    public class MainForm : Form
    {
        private readonly Button _button;
        private int _clicks;

        public MainForm()
        {
            Text = "Scaffold";
            ClientSize = new Size(320, 160);
            StartPosition = FormStartPosition.CenterScreen;

            _button = new Button();
            _button.Text = "Click me";
            _button.Location = new Point(110, 60);
            _button.AutoSize = true;
            _button.Click += OnButtonClick;   // wire the event in code, no designer
            Controls.Add(_button);
        }

        private void OnButtonClick(object sender, EventArgs e)
        {
            _clicks++;
            _button.Text = "Clicked " + _clicks + "x";
        }
    }

    internal static class Program
    {
        [STAThread]   // required: WinForms needs a single-threaded apartment
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
```

## Build

```
csc.exe -nologo -noconfig -optimize+ -langversion:5 -target:winexe ^
    -reference:System.dll -reference:System.Core.dll ^
    -reference:System.Windows.Forms.dll -reference:System.Drawing.dll ^
    -out:app.exe Main.cs
```

`-target:winexe` (not `exe`) so no console window pops up. `csc` resolves the two WinForms simple
names from the framework dir; the host's `build.ps1` instead resolves them from the GAC with a
`Resolve-GacDll` helper - either works. `csc` =
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

## Notes

- `[STAThread]` on `Main` is mandatory - WinForms relies on the single-threaded apartment for COM
  (clipboard, drag-drop, common dialogs).
- Call `EnableVisualStyles()` before `Application.Run` so controls get the themed (not Windows-95)
  look. `SetCompatibleTextRenderingDefault(false)` opts into GDI+ text and must precede the first
  window too.
- No WPF here - `PresentationFramework.dll` is not in the in-box reference dir. WinForms is the GUI
  toolkit (see `in-box-assemblies`).
- C# 5: set control properties on separate statements (no object-initializer requirement, no
  interpolation). For native polish (Explorer theming, dark title bar) see `ui-pinvoke`.
