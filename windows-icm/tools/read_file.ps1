# read_file.ps1 - print one project source file verbatim. Used to ground a surgical edit on the exact
# current contents (and handy as a /show). Path is resolved under the project; no traversal.
param([string] $Proj, [string] $Path)

if (-not $Proj) { Write-Output "read_file: missing -Proj"; exit 1 }
if (-not $Path) { Write-Output "read_file: missing -Path"; exit 1 }

$root = $null
if ((Test-Path $Proj) -and (Test-Path (Join-Path $Proj "project.json"))) { $root = $Proj }
elseif (Test-Path (Join-Path (Join-Path "out" $Proj) "project.json")) { $root = Join-Path "out" $Proj }
if (-not $root) { Write-Output ("no project (project.json not found) for: " + $Proj); exit 1 }

$rel = $Path -replace '/','\'
if ($rel -match '\.\.') { Write-Output ("refusing path with '..': " + $Path); exit 1 }
$full = Join-Path $root $rel
if (-not (Test-Path $full)) { Write-Output ("file not found: " + $Path); exit 1 }

Write-Output (Get-Content -Raw $full)
exit 0
