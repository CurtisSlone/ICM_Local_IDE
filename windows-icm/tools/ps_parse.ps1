# ps_parse.ps1 - the PowerShell oracle for the generate-verify-repair loop.
# Reads a PowerShell script from stdin and reports parse errors. Syntax only - it NEVER executes the
# script. Exit 0 = parses cleanly.

# Read raw bytes from stdin, strip a leading UTF-8 BOM (the host's writer prepends one), decode UTF-8.
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

$tokens = $null
$errors = $null
[void][System.Management.Automation.Language.Parser]::ParseInput($code, [ref]$tokens, [ref]$errors)

if ($errors -and $errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Output ("Line " + $_.Extent.StartLineNumber + ": " + $_.Message) }
    exit 1
}
Write-Output "OK: parses cleanly"
exit 0
