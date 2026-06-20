<!--icm
{
  "id": "powershell-5-1",
  "title": "PowerShell 5.1 constraints and gotchas",
  "doc_type": "reference",
  "summary": "Windows PowerShell 5.1 differences from pwsh 7: no ternary/null-coalescing, no chain operators, here-string rules, UTF-16 default encoding, and other things a modern-trained model gets wrong.",
  "keywords": ["powershell 5.1", "windows powershell", "pwsh 7", "ternary", "null-coalescing", "chain operator", "here-string", "utf-16", "encoding", "ConvertFrom-Json"],
  "source": { "origin": "authored", "note": "host environment constraint - scripts run under powershell.exe 5.1, not pwsh 7" }
}
-->
# PowerShell 5.1 constraints and gotchas

Scripts here run under **Windows PowerShell 5.1** (`powershell.exe`), not PowerShell 7 (`pwsh`). A
model trained on modern PowerShell will use 7-only syntax that fails to parse or run.

## Not available in 5.1
- **Ternary** `a ? b : c` -> use `if (a) { b } else { c }`.
- **Null-coalescing** `??` and `??=` -> explicit `if ($null -eq $x) { ... }`.
- **Null-conditional** `?.` / `?[]` -> explicit null checks.
- **Pipeline chain operators** `&&` and `||` -> `cmd1; if ($?) { cmd2 }`.
- **`-AsHashtable`** on `ConvertFrom-Json` -> it returns a `PSCustomObject`.
- **`Test-Json`**, `ForEach-Object -Parallel`, and `clean {}` blocks -> not present.

## Gotchas that bite
- **Default file encoding is UTF-16 LE (BOM).** Use `Set-Content -Encoding UTF8` (or `Out-File
  -Encoding utf8`) when another tool will read the file.
- **`$var:`** in a double-quoted string is parsed as a scope/drive qualifier - write `"${var}: text"`
  or use `-f` formatting.
- **Here-strings**: the closing `'@` / `"@` must be at column 0, on its own line.
- **`$?`** is `$false` after a native exe's stderr is redirected with `2>&1`; check `$LASTEXITCODE`
  for native exit status, not `$?`.
- Prefer `[System.IO.File]::ReadAllText` / `Get-Content -Raw` for whole-file reads.
- **A typed `param` silently coerces every assignment to that variable, and variables are
  case-insensitive.** So in a script with `param([string] $Proj)`, writing
  `$proj = Get-Content x.json | ConvertFrom-Json` does NOT give you an object - `$proj` *is* `$Proj`,
  still `[string]`-constrained, so the object is coerced to `"@{name=...; files=System.Object[]}"`.
  Symptoms: `.property` lookups fail ("property cannot be found") and re-serializing writes a quoted
  string. Fix: never reuse a typed param's name for parsed data - load into a differently-named var
  (`$meta = ... | ConvertFrom-Json`).

## Parsing without running (the oracle)
Validate a script's syntax without executing it:
```powershell
$errors = $null
[System.Management.Automation.Language.Parser]::ParseInput($code, [ref]$null, [ref]$errors)
# $errors.Count -eq 0  ==>  parses cleanly
```
This is what the `ps_parse` tool does - it never executes generated PowerShell.
