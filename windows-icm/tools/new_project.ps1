# new_project.ps1 - scaffold a structured project under out\<name> from the project-structured
# template (see scaffold/csharp/project-structured.md and reference/dotnet/project-layout.md).
# Deterministic: no model. Source files use the constant root namespace "App" so the templates need
# no fragile substitution; only build.ps1, response.rsp, project.json and PROJECT.md are stamped with
# the name/kind/date. Refuses to overwrite an existing folder.
#   tools/new_project.ps1 "FunnyApp winforms"
param([string] $Spec)

$ErrorActionPreference = "Stop"

$parts = @()
if ($Spec) { $parts = ($Spec.Trim() -split '\s+') }
if ($parts.Count -lt 1 -or $parts[0].Length -eq 0) { Write-Output "usage: new <name> [console|winforms]"; exit 1 }
$name = $parts[0]
$kind = "console"
if ($parts.Count -gt 1) { $kind = $parts[1].ToLower() }
if ($kind -ne "console" -and $kind -ne "winforms") { Write-Output ("unknown kind: " + $kind + " (use console or winforms)"); exit 1 }
if ($name -notmatch '^[A-Za-z0-9_.-]+$') { Write-Output ("invalid project name: " + $name + " (use letters, digits, _ . -)"); exit 1 }

$root = Join-Path "out" $name
if (Test-Path $root) { Write-Output ("already exists: " + $root + " (refusing to overwrite)"); exit 1 }
$date = Get-Date -Format "yyyy-MM-dd"

function Write-Proj($rel, $content) {
    $full = Join-Path $root $rel
    $dir = Split-Path -Parent $full
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    [System.IO.File]::WriteAllText($full, $content, (New-Object System.Text.UTF8Encoding $false))
}

New-Item -ItemType Directory -Force $root | Out-Null
New-Item -ItemType Directory -Force (Join-Path $root "dist") | Out-Null

# ----- build.ps1 (shared) -----
$buildPs1 = @'
# build.ps1 - compile the whole project with the in-box csc via the response file.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe not found." }
New-Item -ItemType Directory -Force "$root\dist" | Out-Null
Push-Location $root            # rsp paths (src\, dist\) are relative to CWD
try {
    & $csc -noconfig "@response.rsp"
    if ($LASTEXITCODE -ne 0) { throw "build failed (csc exit $LASTEXITCODE)" }
} finally { Pop-Location }
Write-Host "built $root\dist\__NAME__.exe"
'@
Write-Proj "build.ps1" ($buildPs1.Replace("__NAME__", $name))

if ($kind -eq "console") {
    # ----- console source (minimal: just the entry point + empty Core/Drivers folders to grow into;
    # the full composition example lives in scaffold/csharp/project-structured.md) -----
    Write-Proj "src\Program.cs" @'
using System;

namespace App
{
    internal static class Program
    {
        // The entry point. Add pure logic under src\Core and I/O adapters under src\Drivers, then
        // wire them in here. Returning int lets the process report an exit code (0 = success).
        private static int Main(string[] args)
        {
            Console.WriteLine("App ready. Add features under src\\Core and src\\Drivers.");
            return 0;
        }
    }
}
'@
    New-Item -ItemType Directory -Force (Join-Path $root "src\Core") | Out-Null
    New-Item -ItemType Directory -Force (Join-Path $root "src\Drivers") | Out-Null
    $rsp = "/nologo`r`n/target:exe`r`n/langversion:5`r`n/warn:4`r`n/reference:System.dll`r`n/reference:System.Core.dll`r`n/out:dist\" + $name + ".exe`r`n/recurse:src\*.cs`r`n"
    $refs = @("System.dll", "System.Core.dll")
    $files = @(
        [pscustomobject]@{ path = "src/Program.cs"; role = "entry point" }
    )
    $arch = "Single exe via /recurse. Pure logic in src/Core, I/O adapters in src/Drivers, wired together in Program.cs."
}
else {
    # ----- winforms source -----
    Write-Proj "src\Program.cs" @'
using System;
using System.Windows.Forms;
using App.Ui;

namespace App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
'@
    Write-Proj "src\Ui\MainForm.cs" @'
using System;
using System.Drawing;
using System.Windows.Forms;

namespace App.Ui
{
    // The main window, built entirely in code (no designer, no .resx).
    public sealed class MainForm : Form
    {
        public MainForm()
        {
            this.Text = "App";
            this.ClientSize = new Size(360, 180);

            Label label = new Label();
            label.Text = "hello";
            label.AutoSize = true;
            label.Location = new Point(20, 20);
            this.Controls.Add(label);
        }
    }
}
'@
    $rsp = "/nologo`r`n/target:winexe`r`n/langversion:5`r`n/warn:4`r`n/reference:System.dll`r`n/reference:System.Core.dll`r`n/reference:System.Drawing.dll`r`n/reference:System.Windows.Forms.dll`r`n/out:dist\" + $name + ".exe`r`n/recurse:src\*.cs`r`n"
    $refs = @("System.dll", "System.Core.dll", "System.Drawing.dll", "System.Windows.Forms.dll")
    $files = @(
        [pscustomobject]@{ path = "src/Program.cs"; role = "entry / [STAThread] Main" },
        [pscustomobject]@{ path = "src/Ui/MainForm.cs"; role = "main window (controls built in code)" }
    )
    $arch = "Single winexe via /recurse. Ui holds Forms built in code; add domain logic under src/Core."
}

Write-Proj "response.rsp" $rsp

# ----- project.json (machine memory) -----
$proj = [pscustomobject]@{
    name       = $name
    kind       = $kind
    created    = $date
    entry      = "src/Program.cs"
    out        = ("dist/" + $name + ".exe")
    rsp        = "response.rsp"
    assembly   = "single"
    references = $refs
    files      = $files
}
Write-Proj "project.json" ($proj | ConvertTo-Json -Depth 6)

# ----- PROJECT.md (narrative memory) -----
$projMd = "# __NAME__`r`n`r`n## Purpose`r`nOne or two lines: what this is and why it exists.`r`n`r`n## Architecture`r`n__ARCH__`r`n`r`n## Status / TODO`r`n- [x] scaffold + first green build`r`n- [ ] (next feature)`r`n`r`n## Changelog`r`n- __DATE__  scaffolded (__KIND__)`r`n"
$projMd = $projMd.Replace("__NAME__", $name).Replace("__ARCH__", $arch).Replace("__DATE__", $date).Replace("__KIND__", $kind)
Write-Proj "PROJECT.md" $projMd

$count = (Get-ChildItem -Recurse -File $root | Measure-Object).Count
Write-Output ("OK: created " + $root + " (" + $kind + ") - " + $count + " files. Build with: /build " + $name)
exit 0
