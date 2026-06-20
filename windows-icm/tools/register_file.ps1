# register_file.ps1 - commit a newly added file to a project's on-disk memory: add (or update) its
# entry in project.json.files and append a PROJECT.md changelog line. Idempotent. Run only after a
# green build (the add_file flow gates this on the project compiling).
#   tools/register_file.ps1 -Proj FunnyApp -Path src\Drivers\NetworkOutput.cs -Role "IOutput over TCP"
param([string] $Proj, [string] $Path, [string] $Role)

if (-not $Proj) { Write-Output "register_file: missing -Proj"; exit 1 }
if (-not $Path) { Write-Output "register_file: missing -Path"; exit 1 }
if (-not $Role) { $Role = "(no role recorded)" }

$root = $null
if ((Test-Path $Proj) -and (Test-Path (Join-Path $Proj "project.json"))) { $root = $Proj }
elseif (Test-Path (Join-Path (Join-Path "out" $Proj) "project.json")) { $root = Join-Path "out" $Proj }
if (-not $root) { Write-Output ("no project (project.json not found) for: " + $Proj); exit 1 }

$rel = ($Path -replace '\\','/')
$role = ($Role -replace '\s+',' ').Trim()

# --- project.json --- (note: the loaded object must NOT reuse the [string]-typed $Proj param name,
# or PowerShell's case-insensitive variables coerce the object back to a string.)
$pj = Join-Path $root "project.json"
$meta = Get-Content -Raw $pj | ConvertFrom-Json
$list = @()
if ($meta.files) { $list = @($meta.files) }
$found = $false
foreach ($f in $list) { if (($f.path -replace '\\','/') -eq $rel) { $f.role = $role; $found = $true } }
if (-not $found) { $list += [pscustomobject]@{ path = $rel; role = $role } }
$meta.files = $list
($meta | ConvertTo-Json -Depth 6) | Set-Content -Encoding UTF8 $pj

# --- PROJECT.md changelog (Changelog is the last section, so append at EOF) ---
$pmd = Join-Path $root "PROJECT.md"
$date = Get-Date -Format "yyyy-MM-dd"
$line = "- " + $date + "  added " + $rel + " (" + $role + ")"
if (Test-Path $pmd) { Add-Content -Encoding UTF8 $pmd $line }

Write-Output ("OK: registered " + $rel + " in " + $meta.name)
exit 0
