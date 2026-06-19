# mcp-smoke.ps1 - drive the icm MCP server over stdio exactly as a client would, and assert the
# JSON-RPC responses. Model-free (initialize / tools/list / catalog / read_entry / ping), so it runs
# without Ollama. Usage: powershell -NoProfile -File mcp-smoke.ps1 [instanceDir]
param([string] $Dir = "windows-icm")

# NOTE: do not set ErrorActionPreference=Stop here - the server writes a one-line banner to stderr at
# startup, which PowerShell 5.1 wraps as a NativeCommandError; under Stop that would abort the run.
$launcher = Join-Path $PSScriptRoot "icm.cmd"

# A real client's opening handshake, then a few tool calls. One JSON object per line.
$msgs = @(
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"mcp-smoke","version":"1.0"}}}'
  '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
  '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"catalog","arguments":{"group":"concurrency"}}}'
  '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"read_entry","arguments":{"id":"host-conventions"}}}'
  '{"jsonrpc":"2.0","id":5,"method":"ping"}'
  '{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"does_not_exist","arguments":{}}}'
)

$raw = $msgs | & $launcher mcp $Dir 2>$null
$resp = @{}
foreach ($line in $raw) {
  $t = "$line".Trim()
  if ($t.Length -eq 0) { continue }
  try { $o = $t | ConvertFrom-Json } catch { continue }
  if ($null -ne $o.id) { $resp[[int]$o.id] = $o }
}

$fails = 0
function Check($name, $cond) {
  if ($cond) { Write-Host ("  ok    " + $name) }
  else { Write-Host ("  FAIL  " + $name); $script:fails++ }
}

# 1: initialize echoes the client's protocol version + advertises tools capability + serverInfo
$init = $resp[1]
Check "initialize: protocolVersion echoed" ($init -and $init.result.protocolVersion -eq "2025-06-18")
Check "initialize: tools capability"        ($init -and $null -ne $init.result.capabilities.tools)
Check "initialize: serverInfo.name"          ($init -and $init.result.serverInfo.name)

# 2: tools/list advertises the built-ins + at least one instance tool
$tools = @(); if ($resp[2]) { $tools = $resp[2].result.tools.name }
Check "tools/list: catalog"     ($tools -contains "catalog")
Check "tools/list: read_entry"  ($tools -contains "read_entry")
Check "tools/list: instance tools present" ($tools.Count -ge 3)

# 3: catalog returns text, not an error
$cat = $resp[3]
Check "catalog: returns content" ($cat -and $cat.result.content[0].text.Length -gt 0 -and -not $cat.result.isError)
Check "catalog: filtered to group" ($cat -and $cat.result.content[0].text -match "concurrency")

# 4: read_entry returns the entry body with the metadata block stripped
$re = $resp[4]
Check "read_entry: returns content" ($re -and $re.result.content[0].text.Length -gt 0 -and -not $re.result.isError)
Check "read_entry: metadata stripped" ($re -and ($re.result.content[0].text -notmatch '<!--icm'))

# 5: ping ok
Check "ping: ok" ($resp[5] -and $null -ne $resp[5].result)

# 6: unknown tool -> JSON-RPC error (-32602)
$err = $resp[6]
Check "unknown tool: error -32602" ($err -and $err.error -and $err.error.code -eq -32602)

Write-Host ""
if ($fails -eq 0) { Write-Host "mcp-smoke: ALL PASS"; exit 0 }
else { Write-Host ("mcp-smoke: " + $fails + " FAILED"); exit 1 }
