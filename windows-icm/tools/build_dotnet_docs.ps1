# build_dotnet_docs.ps1 - build a searchable .NET API corpus by REFLECTION over the in-box
# assemblies (offline; signatures, not prose). One chunk per public type, with its public member
# signatures flattened into the text so a keyword search finds the type by a member name. Output:
# refdocs/dotnet.json (an array of { id, title, kind, text }).
#
#   powershell -NoProfile -File tools/build_dotnet_docs.ps1
#   powershell -NoProfile -File tools/build_dotnet_docs.ps1 -Assemblies System,System.Core -Out refdocs/dotnet.json
param(
    [string[]] $Assemblies,
    [string] $Out = "refdocs/dotnet.json",
    [int] $MaxMembers = 60
)

if (-not $Assemblies -or $Assemblies.Count -eq 0) {
    $Assemblies = @("mscorlib", "System", "System.Core", "System.Web.Extensions", "System.Windows.Forms", "System.Drawing")
}

$chunks = New-Object System.Collections.Generic.List[object]
$flags = [System.Reflection.BindingFlags]"Public,Instance,Static,DeclaredOnly"

# Split CamelCase / Pascale identifiers into space-separated lowercase words so a natural-language
# query ("read all text") matches a mashed member name ("ReadAllText"). Reflection has no prose, so
# these split keywords are the only word-level signal BM25 and the embedder get.
function Split-Words([string] $s) {
    if (-not $s) { return "" }
    $x = [regex]::Replace($s, '([a-z0-9])([A-Z])', '$1 $2')
    $x = [regex]::Replace($x, '([A-Z]+)([A-Z][a-z])', '$1 $2')
    $x = [regex]::Replace($x, '[^A-Za-z0-9]+', ' ')
    return $x.ToLower().Trim()
}

foreach ($name in $Assemblies) {
    $asm = $null
    try { $asm = [System.Reflection.Assembly]::LoadWithPartialName($name) } catch { }
    if ($null -eq $asm) { Write-Warning ("could not load assembly: " + $name); continue }

    $types = @()
    try { $types = $asm.GetExportedTypes() } catch { }

    foreach ($t in $types) {
        $kind = "class"
        if ($t.IsEnum) { $kind = "enum" }
        elseif ($t.IsInterface) { $kind = "interface" }
        elseif ($t.IsValueType) { $kind = "struct" }

        $members = New-Object System.Collections.Generic.List[string]
        $names = New-Object System.Collections.Generic.List[string]
        try {
            foreach ($m in $t.GetMethods($flags)) {
                if ($m.IsSpecialName) { continue }  # skip get_/set_/add_/op_ accessors
                $ps = ($m.GetParameters() | ForEach-Object { $_.ParameterType.Name + " " + $_.Name }) -join ", "
                $members.Add($m.ReturnType.Name + " " + $m.Name + "(" + $ps + ")")
                $names.Add($m.Name)
            }
            foreach ($p in $t.GetProperties($flags)) { $members.Add($p.PropertyType.Name + " " + $p.Name); $names.Add($p.Name) }
        } catch { }

        # Keywords (split member names) first so they survive truncation - they carry the word-level
        # signal; the verbose signatures follow for grounding/display.
        $keywords = (($names | Select-Object -First $MaxMembers | ForEach-Object { Split-Words $_ }) -join " ")
        $memText = ($members | Select-Object -First $MaxMembers) -join "; "
        $text = $kind + " " + $t.FullName + " (" + (Split-Words $t.Name) + "). Namespace " + $t.Namespace +
                ". Keywords: " + $keywords + ". Members: " + $memText
        if ($text.Length -gt 2000) { $text = $text.Substring(0, 2000) }
        $chunks.Add([ordered]@{ id = $t.FullName; title = $t.Name; kind = $kind; text = $text })
    }
}

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$json = $chunks | ConvertTo-Json -Depth 4 -Compress
[System.IO.File]::WriteAllText($Out, $json, (New-Object System.Text.UTF8Encoding $false))
Write-Output ("wrote " + $chunks.Count + " type chunks -> " + $Out)
