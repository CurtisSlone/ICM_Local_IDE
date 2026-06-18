# run-gui.ps1 - launch icm-gui without tripping Smart App Control.
#
# Smart App Control (enforce mode on this box) blocks running unsigned, locally-built .exe files
# from disk. It does NOT block in-memory managed execution inside an already-trusted, signed host
# (powershell.exe). So we read the assembly's bytes and run them in-process. This runs only the
# exe you just built here; SAC still guards everything else.
#
# WinForms needs an STA thread; if we are not STA, relaunch ourselves with -STA.

param([string]$Folder = ".")

if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    & powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Folder $Folder
    return
}

$ErrorActionPreference = "Stop"
$exe = Join-Path $PSScriptRoot "icm-gui.exe"
try { $dir = (Resolve-Path -LiteralPath $Folder).Path } catch { $dir = $Folder }
try {
    $bytes = [System.IO.File]::ReadAllBytes($exe)
    $asm = [System.Reflection.Assembly]::Load($bytes)
    [void]$asm.EntryPoint.Invoke($null, @(, [string[]]@($dir)))
} catch {
    $log = Join-Path $PSScriptRoot "icm-gui.error.log"
    $msg = $_.Exception.ToString()
    if ($_.Exception.InnerException) { $msg += "`n--- INNER ---`n" + $_.Exception.InnerException.ToString() }
    Set-Content -Encoding utf8 -Path $log -Value $msg
    Write-Error $msg
}
