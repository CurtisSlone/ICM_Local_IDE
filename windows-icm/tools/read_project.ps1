# read_project.ps1 - the tiered context loader (the "memory read" for a project). Emits ONE text blob
# for grounding / status: Tier 0 always (PROJECT.md, project.json, the src tree with each file's role,
# and a manifest-drift check), plus one Drivers/ sibling as an adapter template. -Include core|all adds
# the contents of src\Core (Tier 1) or every src file (all). Backs both the add_file flow and /project.
#   tools/read_project.ps1 FunnyApp
param([string] $Proj, [string] $Include = "default")

if (-not $Proj) { Write-Output "usage: project <name>"; exit 1 }
$root = $null
if ((Test-Path $Proj) -and (Test-Path (Join-Path $Proj "project.json"))) { $root = $Proj }
elseif (Test-Path (Join-Path (Join-Path "out" $Proj) "project.json")) { $root = Join-Path "out" $Proj }
if (-not $root) { Write-Output ("no project (project.json not found) for: " + $Proj); exit 1 }
$rootFull = (Resolve-Path $root).Path
if ($Include -ne "core" -and $Include -ne "all") { $Include = "default" }   # tolerate unfilled {include}

$sb = New-Object System.Text.StringBuilder
function Add($s) { [void]$sb.AppendLine($s) }

# --- PROJECT.md (narrative memory) ---
$pmd = Join-Path $root "PROJECT.md"
if (Test-Path $pmd) { Add "=== PROJECT.md ==="; Add ((Get-Content -Raw $pmd).TrimEnd()); Add "" }

# --- project.json (machine memory) --- (use $meta, not the [string]-typed $Proj param, or the
# parsed object gets coerced back to a string)
$pj = Join-Path $root "project.json"
$json = Get-Content -Raw $pj
$meta = $json | ConvertFrom-Json
Add "=== project.json ==="; Add ($json.TrimEnd()); Add ""

# --- src tree with roles (Tier 0 orientation) ---
Add "=== src tree (path - role) ==="
$roleOf = @{}
if ($meta.files) { foreach ($f in $meta.files) { $roleOf[($f.path -replace '\\','/')] = $f.role } }
$srcDir = Join-Path $root "src"
$onDisk = @()
if (Test-Path $srcDir) {
    Get-ChildItem -Recurse -File -Filter *.cs $srcDir | ForEach-Object {
        $rel = $_.FullName.Substring($rootFull.Length).TrimStart('\','/') -replace '\\','/'
        $onDisk += $rel
        $role = if ($roleOf.ContainsKey($rel)) { $roleOf[$rel] } else { "(no role recorded)" }
        Add ("  " + $rel + "  -  " + $role)
    }
}
Add ""

# --- manifest drift: disk vs project.json.files ---
$drift = @()
foreach ($rel in $onDisk) { if (-not $roleOf.ContainsKey($rel)) { $drift += ("on disk but unrecorded: " + $rel) } }
foreach ($rel in $roleOf.Keys) { if ($onDisk -notcontains $rel) { $drift += ("in manifest but missing on disk: " + $rel) } }
if ($drift.Count -gt 0) { Add "=== MANIFEST DRIFT ==="; foreach ($d in $drift) { Add ("  " + $d) }; Add "" }

# --- one Drivers sibling as an adapter template (Tier 2) ---
$drv = Join-Path $srcDir "Drivers"
if (Test-Path $drv) {
    $sample = Get-ChildItem -File -Filter *.cs $drv | Where-Object { $_.Name -notmatch '^I[A-Z]' } | Select-Object -First 1
    if (-not $sample) { $sample = Get-ChildItem -File -Filter *.cs $drv | Select-Object -First 1 }
    if ($sample) {
        Add ("=== adapter template: src/Drivers/" + $sample.Name + " ===")
        Add ((Get-Content -Raw $sample.FullName).TrimEnd()); Add ""
    }
}

# --- Tier 1 / all: file contents ---
if ($Include -eq "core" -or $Include -eq "all") {
    $coreDir = Join-Path $srcDir "Core"
    if (Test-Path $coreDir) {
        Get-ChildItem -Recurse -File -Filter *.cs $coreDir | ForEach-Object {
            $rel = $_.FullName.Substring($rootFull.Length).TrimStart('\','/') -replace '\\','/'
            Add ("=== " + $rel + " ==="); Add ((Get-Content -Raw $_.FullName).TrimEnd()); Add ""
        }
    }
}
if ($Include -eq "all") {
    foreach ($rel in $onDisk) {
        if ($rel -notmatch '^src/Core/') {
            Add ("=== " + $rel + " ==="); Add ((Get-Content -Raw (Join-Path $root $rel)).TrimEnd()); Add ""
        }
    }
}

Write-Output ($sb.ToString().TrimEnd())
exit 0
