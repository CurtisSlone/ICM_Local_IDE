# build_ps_docs.ps1 - build a searchable PowerShell cmdlet corpus from Get-Command + Get-Help.
# Works OFFLINE (name, syntax, parameters from metadata) and richer ONLINE: pass -UpdateHelp to pull
# synopses and examples first (one-time network + admin). Output: refdocs/powershell.json
# (an array of { id, title, kind, text }).
#
#   powershell -NoProfile -File tools/build_ps_docs.ps1                 # offline (metadata)
#   powershell -NoProfile -File tools/build_ps_docs.ps1 -UpdateHelp     # online (with prose/examples)
param(
    [string] $Out = "refdocs/powershell.json",
    [switch] $UpdateHelp,
    [int] $Max = 0
)

if ($UpdateHelp) {
    Write-Host "Update-Help (network)..."
    try { Update-Help -Force -ErrorAction SilentlyContinue } catch { Write-Warning ("Update-Help failed: " + $_.Exception.Message) }
}

$cmds = Get-Command -CommandType Cmdlet, Function -ErrorAction SilentlyContinue | Sort-Object Name -Unique
if ($Max -gt 0) { $cmds = $cmds | Select-Object -First $Max }

$chunks = New-Object System.Collections.Generic.List[object]
foreach ($c in $cmds) {
    $name = $c.Name

    $syntax = ""
    try { $syntax = ((Get-Command -Name $name -Syntax -ErrorAction SilentlyContinue) | Out-String).Trim() } catch { }

    $params = ""
    try { if ($c.Parameters) { $params = (($c.Parameters.Keys | Sort-Object) -join ", ") } } catch { }

    $synopsis = ""
    $examples = ""
    try {
        $help = Get-Help -Name $name -ErrorAction SilentlyContinue
        if ($help) {
            $syn = ($help.Synopsis | Out-String).Trim()
            # An undownloaded help stub repeats the syntax as the synopsis; ignore that.
            if ($syn -and -not $syn.StartsWith($name)) { $synopsis = $syn }
            if ($help.examples -and $help.examples.example) {
                $ex = ($help.examples.example | ForEach-Object { ($_.code | Out-String).Trim() }) -join " | "
                if ($ex.Length -gt 600) { $ex = $ex.Substring(0, 600) }
                $examples = $ex
            }
        }
    } catch { }

    $text = "cmdlet " + $name + "."
    if ($synopsis) { $text += " " + $synopsis }
    if ($syntax) { $text += " Syntax: " + $syntax }
    if ($params) { $text += " Parameters: " + $params }
    if ($examples) { $text += " Examples: " + $examples }
    if ($text.Length -gt 1500) { $text = $text.Substring(0, 1500) }

    $chunks.Add([ordered]@{ id = $name; title = $name; kind = "cmdlet"; text = $text })
}

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$json = $chunks | ConvertTo-Json -Depth 4 -Compress
[System.IO.File]::WriteAllText($Out, $json, (New-Object System.Text.UTF8Encoding $false))
Write-Output ("wrote " + $chunks.Count + " cmdlet chunks -> " + $Out)
