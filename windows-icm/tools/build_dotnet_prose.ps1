# build_dotnet_prose.ps1 - build a searchable .NET PROSE corpus from curated learn.microsoft
# conceptual pages (online). Where build_dotnet_docs.ps1 gives reflection SIGNATURES, this gives the
# "how/why" text that semantic search wants. Each page is split into <h2> sections; each section is
# one chunk. Output: refdocs/dotnet_prose.json (an array of { id, title, kind, text }), the same
# shape the host's `search` node / `icm docsearch` reads.
#
#   powershell -NoProfile -File tools/build_dotnet_prose.ps1
#   powershell -NoProfile -File tools/build_dotnet_prose.ps1 -UrlFile refdocs/dotnet_prose.urls.txt -Out refdocs/dotnet_prose.json
param(
    [string] $UrlFile = "refdocs/dotnet_prose.urls.txt",
    [string] $Out = "refdocs/dotnet_prose.json",
    [int] $MaxChars = 2400
)

# PS 5.1 defaults to TLS 1.0/1.1; learn.microsoft requires TLS 1.2. Without this the request fails.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$urls = @()
if (Test-Path $UrlFile) {
    $urls = Get-Content -LiteralPath $UrlFile | ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith("#") }
} else {
    Write-Warning ("no url list at " + $UrlFile + " - nothing to fetch")
}
if (-not $urls -or $urls.Count -eq 0) { Write-Warning "no URLs to fetch"; return }

# HTML -> readable text: drop script/style/nav, strip tags, decode entities, collapse whitespace.
function Convert-HtmlToText([string] $html) {
    if (-not $html) { return "" }
    $t = [regex]::Replace($html, '(?is)<(script|style|nav|svg|template)\b[^>]*>.*?</\1>', ' ')
    $t = [regex]::Replace($t, '(?is)<!--.*?-->', ' ')
    $t = [regex]::Replace($t, '(?i)<(br|/p|/div|/li|/h[1-6])\s*>', "`n")
    $t = [regex]::Replace($t, '<[^>]+>', ' ')
    $t = [System.Net.WebUtility]::HtmlDecode($t)
    $t = [regex]::Replace($t, '[ \t]+', ' ')
    $t = [regex]::Replace($t, '(\s*\n\s*)+', "`n")
    return $t.Trim()
}

function Get-PageTitle([string] $html) {
    $m = [regex]::Match($html, '(?is)<title>(.*?)</title>')
    if (-not $m.Success) { return "" }
    $title = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value).Trim()
    # learn.microsoft suffixes "| Microsoft Learn"; drop it.
    return ([regex]::Replace($title, '\s*[|\-]\s*Microsoft Learn\s*$', '')).Trim()
}

$chunks = New-Object System.Collections.Generic.List[object]
foreach ($url in $urls) {
    Write-Output ("fetch " + $url)
    $resp = $null
    try { $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -UserAgent "Mozilla/5.0 (windows-icm docs builder)" -TimeoutSec 30 }
    catch { Write-Warning ("  failed: " + $_.Exception.Message); continue }

    $content = $resp.Content
    $pageTitle = Get-PageTitle $content
    if (-not $pageTitle) { $pageTitle = ($url -split '/')[-1] }

    # The article body lives in <main>...</main>; fall back to the whole document.
    $main = $content
    $mm = [regex]::Match($content, '(?is)<main\b[^>]*>(.*?)</main>')
    if ($mm.Success) { $main = $mm.Groups[1].Value }

    # Split into sections at each <h2>; the text before the first <h2> is the intro section.
    $parts = [regex]::Split($main, '(?i)(?=<h2\b)')
    $slug = ($url -split '/' | Where-Object { $_ }) | Select-Object -Last 1
    $idx = 0
    foreach ($part in $parts) {
        if (-not $part) { continue }
        $hm = [regex]::Match($part, '(?is)<h2\b[^>]*>(.*?)</h2>')
        $heading = if ($hm.Success) { (Convert-HtmlToText $hm.Groups[1].Value) } else { "Overview" }
        $body = Convert-HtmlToText $part
        if ($body.Length -lt 80) { continue }   # skip nav crumbs / empty sections
        if ($body.Length -gt $MaxChars) { $body = $body.Substring(0, $MaxChars) }
        $title = $pageTitle + " - " + $heading
        $text = $title + ". " + $body + " (source: " + $url + ")"
        $chunks.Add([ordered]@{ id = ($slug + "#" + $idx); title = $title; kind = "prose"; text = $text })
        $idx++
    }
    Write-Output ("  + " + $idx + " section chunk(s)")
}

if ($chunks.Count -eq 0) { Write-Warning "no chunks produced"; return }
$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$json = $chunks | ConvertTo-Json -Depth 4 -Compress
[System.IO.File]::WriteAllText($Out, $json, (New-Object System.Text.UTF8Encoding $false))
Write-Output ("wrote " + $chunks.Count + " prose chunks -> " + $Out)
