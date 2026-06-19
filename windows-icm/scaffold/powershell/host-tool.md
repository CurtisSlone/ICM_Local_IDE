<!--icm
{
  "id": "host-tool",
  "title": "Host tool (ToolRunner stdin contract)",
  "doc_type": "scaffold",
  "group": "powershell",
  "summary": "A PowerShell instance-tool skeleton fitting this host's ToolRunner contract: read raw stdin bytes, strip a leading UTF-8 BOM, UTF-8 decode, optionally strip a markdown code fence, do the work, exit 0 on success / non-zero on failure.",
  "keywords": ["host tool", "ToolRunner", "stdin", "OpenStandardInput", "BOM", "UTF-8", "code fence", "exit code", "csc_check", "instance tool", "PS 5.1"],
  "source": { "origin": "authored", "note": "PowerShell 5.1; distilled from tools/csc_check.ps1; verified to parse" }
}
-->
# Host tool (ToolRunner stdin contract)

The shape every instance tool that takes content over stdin should follow, distilled from the host's
`tools/csc_check.ps1`. The host's `ToolRunner` writes the payload to the tool's standard input
(often with a leading UTF-8 BOM) and reads the exit code: **0 = success, non-zero = failure**. The
diagnostics the tool prints become the tool `{output}` a flow can feed back into the next step.

Why read raw bytes and not `[Console]::In`: under a non-UTF-8 console codepage, `[Console]::In`
mis-decodes the BOM and any non-ASCII bytes. Reading the raw stream and decoding as UTF-8 yourself is
deterministic.

## Skeleton

```powershell
# my_tool.ps1 - an instance tool on the host's stdin contract.
# Reads a payload from stdin, does the work, prints output, and sets the exit code
# (0 = success, non-zero = failure). Declared in icm.config.json under "tools".

# 1. Read RAW bytes from stdin (not [Console]::In, which mis-decodes under non-UTF-8 codepages).
$stdin = [Console]::OpenStandardInput()
$ms = New-Object System.IO.MemoryStream
$stdin.CopyTo($ms)
$bytes = $ms.ToArray()

# 2. Strip a leading UTF-8 BOM (239,187,191) - the host's writer often prepends one.
if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {
    if ($bytes.Length -eq 3) {
        $bytes = @()
    }
    else {
        $bytes = $bytes[3..($bytes.Length - 1)]
    }
}

# 3. UTF-8 decode.
$text = [System.Text.Encoding]::UTF8.GetString($bytes)

# 4. Optional: models often wrap output in a Markdown code fence - extract the fenced content.
if ($text -match '(?s)```[a-zA-Z0-9_+-]*\r?\n(.*?)\r?\n```') {
    $text = $matches[1]
}

# 5. Do the work. Replace this block. On success print result and exit 0; on failure
#    print the diagnostic and exit non-zero so the host (and any repair loop) sees it.
try {
    if ([string]::IsNullOrWhiteSpace($text)) {
        Write-Output "error: empty input"
        exit 1
    }

    $lineCount = ($text -split "`n").Count
    Write-Output ("OK: " + $lineCount + " line(s)")
    exit 0
}
catch {
    Write-Output ("error: " + $_.Exception.Message)
    exit 1
}
```

## Usage

Declare it in the instance's `icm.config.json` under `tools`, with the payload sent on stdin:

```
{ "name": "my_tool", "script": "tools/my_tool.ps1", "stdin": "input" }
```

Test it standalone (note the BOM-free pipe; the host adds the BOM itself):

```
"line one`nline two" | powershell -NoProfile -ExecutionPolicy Bypass -File tools\my_tool.ps1
echo $LASTEXITCODE
```

## Notes

- **Exit code is the verdict.** 0 means the check passed; any non-zero means it failed. A `loop`
  flow's repair check reads exactly this, and `{output}` (what you printed) feeds the next
  `generate` prompt. Print a terse diagnostic on failure, not a stack dump.
- **Always read raw bytes + strip the BOM.** This is the single most common cause of a tool that
  "works in the shell but fails under the host" - the BOM bytes leak into the content otherwise.
- **The code-fence strip (step 4) is optional** - keep it when the payload is model-authored content
  (code to compile, JSON to validate); drop it when the input is already clean.
- For a file-on-disk variant (compile a temp file, etc.), write the decoded text WITHOUT a BOM via
  `New-Object System.Text.UTF8Encoding $false` so downstream tools (like `csc`) do not read the BOM
  as stray characters - see `csc_check.ps1`.
- PS 5.1 only: no ternary, no `??`, no `&&`/`||`. Use `if`/`else` and check `$LASTEXITCODE` after any
  native process you invoke.
