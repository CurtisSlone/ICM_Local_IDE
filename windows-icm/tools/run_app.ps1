# run_app.ps1 - launch a (locally-built, unsigned) .NET exe via in-memory load inside trusted
# powershell, so Smart App Control allows it. STA for WinForms; invokes Main with the right arity.
# Used by the console's /run command (launched detached so a GUI does not block the console).
param([string] $Exe)

if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    & powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Exe $Exe
    return
}
$bytes = [System.IO.File]::ReadAllBytes($Exe)
$asm = [System.Reflection.Assembly]::Load($bytes)
$ep = $asm.EntryPoint
if ($ep.GetParameters().Length -eq 0) { [void]$ep.Invoke($null, $null) }
else { [void]$ep.Invoke($null, @(, [string[]]@())) }
