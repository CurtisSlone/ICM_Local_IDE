# build_csharp.ps1 - compile a workspace C# source file to a PERSISTENT .exe (unlike the csc_check /
# csc_winforms oracles, which verify into a temp dir and discard). Auto-detects WinForms (-> winexe +
# WinForms/Drawing refs); otherwise a console exe. Output goes next to the source unless -Out is given.
#   tools/build_csharp.ps1 out\HexViewer.cs
param([string] $Src, [string] $Out)

if (-not (Test-Path $Src)) { Write-Output ("source not found: " + $Src); exit 1 }
$code = Get-Content -Raw $Src

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { Write-Output "csc.exe not found (.NET Framework missing?)"; exit 1 }

if (-not $Out) { $Out = [System.IO.Path]::ChangeExtension($Src, ".exe") }
$winforms = $code -match 'System\.Windows\.Forms'
$target = "-target:exe"
$refs = @("-reference:System.dll", "-reference:System.Core.dll", "-reference:System.Web.Extensions.dll")
$kind = "exe"
if ($winforms) {
    $target = "-target:winexe"
    $refs += @("-reference:System.Drawing.dll", "-reference:System.Windows.Forms.dll")
    $kind = "winexe"
}

$diag = & $csc -nologo -noconfig -optimize+ -langversion:5 -warn:0 $target $refs "-out:$Out" $Src 2>&1
if ($LASTEXITCODE -eq 0) { Write-Output ("OK: built " + $Out + " (" + $kind + ")"); exit 0 }
$diag | ForEach-Object { $_.ToString() }
exit 1
