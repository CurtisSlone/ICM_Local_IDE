# build.ps1 — compile the ICM host (console) and the GUI with the C# compiler that ships with
# Windows. No SDK, no NuGet, no MSBuild: we call the in-box .NET Framework csc.exe directly.
#
# Two executables share one core (Json/Config/Manifest/Icm/Oracle/Ollama/Dispatch):
#   icm.exe      console CLI         (src\Program.cs has its Main; -target:exe)
#   icm-gui.exe  WinForms front end  (src\Gui.cs has its Main;     -target:winexe)
# Each entry-point file is excluded from the other's compile so there is exactly one Main.
#
# Non-default references: System.Web.Extensions.dll (JavaScriptSerializer, the JSON layer) for
# both; System.Windows.Forms.dll + System.Drawing.dll (resolved from the GAC) for the GUI.
# -noconfig ignores the machine csc.rsp so the build is deterministic; -langversion:5 pins the
# language to what this in-box (pre-Roslyn) compiler supports.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework C# compiler) not found. Is .NET Framework 4.x installed?" }

function Resolve-GacDll($name) {
    $p = Get-ChildItem "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\$name" -Filter "$name.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { throw "$name.dll not found in the GAC (.NET Framework component missing?)" }
    return $p.FullName
}

# Partition by folder. The Gui\ folder is the only WinForms code and holds the GUI entry point;
# the Cli\ folder holds the console entry point. Each exe excludes the other's entry folder so there
# is exactly one Main. Shared code (root, Model\, Runtime\, Server\) compiles into both.
$all     = Get-ChildItem "$root\src" -Recurse -Filter *.cs
$console = $all | Where-Object { $_.Directory.Name -ne 'Gui' } | ForEach-Object { $_.FullName }
$gui     = $all | Where-Object { $_.Directory.Name -ne 'Cli' } | ForEach-Object { $_.FullName }

# ---- console: icm.exe ----
& $csc -nologo -noconfig -optimize+ -langversion:5 -warn:4 -target:exe -platform:anycpu `
    "-reference:System.dll" "-reference:System.Core.dll" "-reference:System.Web.Extensions.dll" `
    "-out:$root\icm.exe" $console
if ($LASTEXITCODE -ne 0) { throw "console build failed (csc exit $LASTEXITCODE)" }
Write-Host "built $root\icm.exe"

# ---- GUI: icm-gui.exe ----
$wf = Resolve-GacDll "System.Windows.Forms"
$dr = Resolve-GacDll "System.Drawing"
& $csc -nologo -noconfig -optimize+ -langversion:5 -warn:4 -target:winexe -platform:anycpu `
    "-reference:System.dll" "-reference:System.Core.dll" "-reference:System.Web.Extensions.dll" `
    "-reference:$wf" "-reference:$dr" `
    "-out:$root\icm-gui.exe" $gui
if ($LASTEXITCODE -ne 0) { throw "gui build failed (csc exit $LASTEXITCODE)" }
Write-Host "built $root\icm-gui.exe"
