<!--icm
{
  "id": "advanced-function",
  "title": "Advanced function template (CmdletBinding + pipeline)",
  "doc_type": "scaffold",
  "group": "powershell",
  "summary": "A PS 5.1 advanced function template: [CmdletBinding()], typed and validated params, pipeline input via a process block, and begin/process/end - no ternary, no ??, no chain operators.",
  "keywords": ["PowerShell", "advanced function", "CmdletBinding", "param", "ValidateSet", "ValidateRange", "ValueFromPipeline", "begin process end", "pipeline", "PS 5.1"],
  "source": { "origin": "authored", "note": "PowerShell 5.1; verified to parse" }
}
-->
# Advanced function template (CmdletBinding + pipeline)

An advanced function behaves like a compiled cmdlet: it gets common parameters (`-Verbose`,
`-ErrorAction`, etc.) from `[CmdletBinding()]`, validates its inputs declaratively, and streams
pipeline input through a `process` block. Copy this and replace the body.

## Skeleton

```powershell
function Convert-Item {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        # Pipeline input: each object flowing in fires the process block once.
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string] $InputObject,

        # Constrained choice - PowerShell rejects anything not in the set.
        [Parameter()]
        [ValidateSet('Upper', 'Lower', 'Trim')]
        [string] $Mode = 'Trim',

        # Range-validated numeric option.
        [Parameter()]
        [ValidateRange(1, 100)]
        [int] $Repeat = 1
    )

    begin {
        # Runs once before any pipeline input. Set up counters, open resources here.
        Write-Verbose "Convert-Item starting (Mode=$Mode, Repeat=$Repeat)"
        $count = 0
    }

    process {
        # Runs once per pipeline item ($InputObject is the current item).
        $count++

        # PS 5.1: use if/else, not a ternary.
        if ($Mode -eq 'Upper') {
            $result = $InputObject.ToUpper()
        }
        elseif ($Mode -eq 'Lower') {
            $result = $InputObject.ToLower()
        }
        else {
            $result = $InputObject.Trim()
        }

        for ($i = 0; $i -lt $Repeat; $i++) {
            Write-Output $result
        }
    }

    end {
        # Runs once after all input. Flush, summarize, release resources.
        Write-Verbose "Convert-Item processed $count item(s)"
    }
}
```

## Usage

```powershell
'  hi  ' | Convert-Item -Mode Trim
'abc', 'def' | Convert-Item -Mode Upper -Repeat 2
Convert-Item -InputObject 'x' -Mode Lower -Verbose
```

## Notes

- `[CmdletBinding()]` is what makes it "advanced": it adds the common parameters and enables
  `Write-Verbose`/`Write-Debug` gated by `-Verbose`/`-Debug`.
- The three-block model: `begin` once before input, `process` once per pipeline item, `end` once
  after. With `ValueFromPipeline`, the per-item work MUST live in `process` - code outside it sees
  only the last item.
- Validate at the param boundary (`ValidateNotNullOrEmpty`, `ValidateSet`, `ValidateRange`,
  `ValidatePattern`) instead of hand-checking in the body - the engine enforces it and the error is
  clear.
- Emit objects with `Write-Output` (or bare expressions); reserve `Write-Host` for UI text. Declare
  `[OutputType(...)]` for tooling.
- PS 5.1 only: NO ternary (`cond ? a : b`), NO null-coalescing (`$x ?? $y`), NO pipeline-chain
  operators (`cmd1 && cmd2`, `cmd1 || cmd2`). Those are PowerShell 7+. Use `if`/`else` and check
  `$?` / `$LASTEXITCODE` explicitly.
