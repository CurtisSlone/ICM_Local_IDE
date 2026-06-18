# csc_check.ps1 - the C# oracle for the generate-verify-repair loop.
# Reads a complete C# compilation unit from stdin, compiles it as a library with the in-box csc at
# langversion 5, and prints OK or the compiler diagnostics. Exit 0 = compiles. Does not run anything.

# Read raw bytes from stdin, strip a leading UTF-8 BOM (the host's writer prepends one), then decode
# as UTF-8 - reading via [Console]::In would mis-decode the BOM under a non-UTF-8 console codepage.
$stdin = [Console]::OpenStandardInput()
$ms = New-Object System.IO.MemoryStream
$stdin.CopyTo($ms)
$bytes = $ms.ToArray()
if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {
    $bytes = if ($bytes.Length -eq 3) { @() } else { $bytes[3..($bytes.Length - 1)] }
}
$code = [System.Text.Encoding]::UTF8.GetString($bytes)
# Models often wrap output in a Markdown code fence; extract the fenced content if present.
if ($code -match '(?s)```[a-zA-Z0-9_+-]*\r?\n(.*?)\r?\n```') { $code = $matches[1] }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { Write-Output "csc.exe not found (.NET Framework missing?)"; exit 1 }

$tmp = Join-Path $env:TEMP ("icm_csc_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $tmp | Out-Null
$src = Join-Path $tmp "snippet.cs"
# Write UTF-8 WITHOUT a BOM - csc reads the BOM bytes as stray characters otherwise.
[System.IO.File]::WriteAllText($src, $code, (New-Object System.Text.UTF8Encoding $false))

$out = & $csc -nologo -noconfig -langversion:5 -warn:0 -target:library `
    "-reference:System.dll" "-reference:System.Core.dll" "-reference:System.Web.Extensions.dll" `
    "-out:$tmp\out.dll" $src 2>&1
$exit = $LASTEXITCODE

try { [System.IO.Directory]::Delete($tmp, $true) } catch {}

if ($exit -eq 0) { Write-Output "OK: compiles under C# 5"; exit 0 }
$out | ForEach-Object { $_.ToString() }
exit 1
