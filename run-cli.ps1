# run-cli.ps1 - launch the icm console without tripping Smart App Control (see run-gui.ps1 for
# why). Runs icm.exe's bytes in-memory and forwards the process exit code.

param([Parameter(ValueFromRemainingArguments = $true)][string[]] $CliArgs)

$exe = Join-Path $PSScriptRoot "icm.exe"
if ($null -eq $CliArgs) { $CliArgs = @() }
$bytes = [System.IO.File]::ReadAllBytes($exe)
$asm = [System.Reflection.Assembly]::Load($bytes)
$rv = $asm.EntryPoint.Invoke($null, @(, [string[]]$CliArgs))
exit [int]$rv
