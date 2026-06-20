# build.ps1 - compile the whole project with the in-box csc via the response file.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe not found." }
New-Item -ItemType Directory -Force "$root\dist" | Out-Null
Push-Location $root            # rsp paths (src\, dist\) are relative to CWD
try {
    & $csc -noconfig "@response.rsp"
    if ($LASTEXITCODE -ne 0) { throw "build failed (csc exit $LASTEXITCODE)" }
} finally { Pop-Location }
Write-Host "built $root\dist\WordCount.exe"