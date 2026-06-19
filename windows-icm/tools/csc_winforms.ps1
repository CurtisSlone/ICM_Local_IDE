# csc_winforms.ps1 - the C# oracle for WinForms GUI apps. Like csc_check.ps1 but compiles a complete
# program as a -target:winexe with the WinForms + GDI+ references, so a generated desktop app is
# verified to build. Reads the C# from stdin. Exit 0 = compiles. Does not run anything.

$stdin = [Console]::OpenStandardInput()
$ms = New-Object System.IO.MemoryStream
$stdin.CopyTo($ms)
$bytes = $ms.ToArray()
if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {
    $bytes = if ($bytes.Length -eq 3) { @() } else { $bytes[3..($bytes.Length - 1)] }
}
$code = [System.Text.Encoding]::UTF8.GetString($bytes)
if ($code -match '(?s)```[a-zA-Z0-9_+-]*\r?\n(.*?)\r?\n```') { $code = $matches[1] }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { Write-Output "csc.exe not found (.NET Framework missing?)"; exit 1 }

$tmp = Join-Path $env:TEMP ("icm_winforms_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $tmp | Out-Null
$src = Join-Path $tmp "app.cs"
[System.IO.File]::WriteAllText($src, $code, (New-Object System.Text.UTF8Encoding $false))

$out = & $csc -nologo -noconfig -langversion:5 -warn:0 -target:winexe `
    "-reference:System.dll" "-reference:System.Core.dll" `
    "-reference:System.Drawing.dll" "-reference:System.Windows.Forms.dll" `
    "-out:$tmp\app.exe" $src 2>&1
$exit = $LASTEXITCODE

try { [System.IO.Directory]::Delete($tmp, $true) } catch {}

if ($exit -eq 0) { Write-Output "OK: compiles as a WinForms winexe under C# 5"; exit 0 }
$out | ForEach-Object { $_.ToString() }
exit 1
