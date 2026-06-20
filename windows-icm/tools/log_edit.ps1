# log_edit.ps1 - record an edit in a project's on-disk memory by appending an "edited" line to the
# PROJECT.md changelog. Keeps the narrative memory honest as files change (the file list itself does
# not move, so without this an edit would leave no trace).
param([string] $Proj, [string] $Path, [string] $Summary)

if (-not $Proj) { Write-Output "log_edit: missing -Proj"; exit 1 }
if (-not $Path) { Write-Output "log_edit: missing -Path"; exit 1 }
if (-not $Summary) { $Summary = "edited" }

$root = $null
if ((Test-Path $Proj) -and (Test-Path (Join-Path $Proj "PROJECT.md"))) { $root = $Proj }
elseif (Test-Path (Join-Path (Join-Path "out" $Proj) "PROJECT.md")) { $root = Join-Path "out" $Proj }
if (-not $root) { Write-Output ("no project (PROJECT.md not found) for: " + $Proj); exit 1 }

$rel = ($Path -replace '\\','/')
$summary = ($Summary -replace '\s+',' ').Trim()
$date = Get-Date -Format "yyyy-MM-dd"
Add-Content -Encoding UTF8 (Join-Path $root "PROJECT.md") ("- " + $date + "  edited " + $rel + " (" + $summary + ")")
Write-Output ("OK: logged edit of " + $rel)
exit 0
