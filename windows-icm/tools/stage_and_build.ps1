# stage_and_build.ps1 - the add_file oracle. Writes a candidate C# file into a project's src tree and
# builds the WHOLE project (csc -noconfig @response.rsp), so the project - not one snippet - is the
# thing that must compile. Side effect: the file is left in place; a failing candidate is overwritten
# on the next loop iteration. Code is read from stdin; OK/diagnostics + exit 0/1 (loop-oracle contract).
#   ... | tools/stage_and_build.ps1 -Proj FunnyApp -Path src\Drivers\NetworkOutput.cs
param([string] $Proj, [string] $Path)

# Read code from stdin: strip a leading UTF-8 BOM, decode UTF-8, unwrap a markdown fence if present.
$stdin = [Console]::OpenStandardInput()
$ms = New-Object System.IO.MemoryStream
$stdin.CopyTo($ms)
$bytes = $ms.ToArray()
if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {
    $bytes = if ($bytes.Length -eq 3) { @() } else { $bytes[3..($bytes.Length - 1)] }
}
$code = [System.Text.Encoding]::UTF8.GetString($bytes)
if ($code -match '(?s)```[a-zA-Z0-9_+-]*\r?\n(.*?)\r?\n```') { $code = $matches[1] }

if (-not $Proj) { Write-Output "stage_and_build: missing -Proj"; exit 1 }
if (-not $Path) { Write-Output "stage_and_build: missing -Path"; exit 1 }

# Resolve project root.
$root = $null
if ((Test-Path $Proj) -and (Test-Path (Join-Path $Proj "response.rsp"))) { $root = $Proj }
elseif (Test-Path (Join-Path (Join-Path "out" $Proj) "response.rsp")) { $root = Join-Path "out" $Proj }
if (-not $root) { Write-Output ("no project (response.rsp not found) for: " + $Proj); exit 1 }

# Guard the target path: must live under src\ and be a .cs file, no traversal.
$rel = $Path -replace '/','\'
if ($rel -match '\.\.') { Write-Output ("refusing path with '..': " + $Path); exit 1 }
if ($rel -notmatch '^(?i)src\\.+\.cs$') { Write-Output ("path must be under src\ and end in .cs: " + $Path); exit 1 }

$full = Join-Path $root $rel
$dir = Split-Path -Parent $full
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
[System.IO.File]::WriteAllText($full, $code, (New-Object System.Text.UTF8Encoding $false))

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { Write-Output "csc.exe not found (.NET Framework missing?)"; exit 1 }

Push-Location $root
try {
    $diag = & $csc -noconfig "@response.rsp" 2>&1
    $code2 = $LASTEXITCODE
}
finally { Pop-Location }

if ($code2 -eq 0) { Write-Output ("OK: staged " + ($rel -replace '\\','/') + "; project builds"); exit 0 }
$diag | ForEach-Object { $_.ToString() }
exit 1
