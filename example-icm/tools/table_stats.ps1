# table_stats.ps1 - an example instance-provided tool/skill the host can run.
# Reports the row and column counts of samples/<Table>.txt. The host runs this with the instance
# root as the working directory, so relative paths resolve against the instance.
param([Parameter(Mandatory = $true)][string]$Table)

$path = Join-Path "samples" ("$Table.txt")
if (-not (Test-Path $path)) {
    Write-Error ("no table file: {0}" -f $path)
    exit 1
}

$lines = Get-Content -LiteralPath $path | Where-Object { $_.Trim().Length -gt 0 }
if ($lines.Count -eq 0) {
    Write-Output ("{0}: empty" -f $Table)
    exit 0
}
$cols = ($lines[0] -split "`t").Count
$rows = $lines.Count - 1
Write-Output ("{0}: {1} data row(s), {2} column(s)" -f $Table, $rows, $cols)
