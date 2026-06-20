# build_project.ps1 - build a whole project via its response.rsp (csc -noconfig @response.rsp).
# Persistent output to <proj>\dist. Mirrors the csc_check OK/diagnostics + exit 0/1 contract so it
# also works as a flow loop oracle. Arg: Proj = a project name (resolved under out\) or a path to the
# project root (the folder holding response.rsp).
#   tools/build_project.ps1 FunnyApp
param([string] $Proj)

if (-not $Proj) { Write-Output "usage: build <project>"; exit 1 }

# Resolve the project root: an explicit path, or out\<name>. Must contain a response.rsp.
$root = $null
if ((Test-Path $Proj) -and (Test-Path (Join-Path $Proj "response.rsp"))) { $root = $Proj }
elseif (Test-Path (Join-Path (Join-Path "out" $Proj) "response.rsp")) { $root = Join-Path "out" $Proj }
if (-not $root) { Write-Output ("no project (response.rsp not found) for: " + $Proj); exit 1 }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { Write-Output "csc.exe not found (.NET Framework missing?)"; exit 1 }

New-Item -ItemType Directory -Force (Join-Path $root "dist") | Out-Null
Push-Location $root
try {
    $diag = & $csc -noconfig "@response.rsp" 2>&1
    $code = $LASTEXITCODE
}
finally { Pop-Location }

if ($code -eq 0) {
    # Report the produced exe path (read /out from the rsp).
    $out = ""
    foreach ($line in (Get-Content (Join-Path $root "response.rsp"))) {
        if ($line -match '^\s*/out:(.+)$') { $out = $matches[1].Trim() }
    }
    Write-Output ("OK: built " + (Join-Path $root $out))
    exit 0
}
$diag | ForEach-Object { $_.ToString() }
exit 1
