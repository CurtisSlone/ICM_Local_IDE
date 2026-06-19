<!--icm
{
  "id": "module",
  "title": "PowerShell module (.psm1 + .psd1 manifest)",
  "doc_type": "scaffold",
  "group": "powershell",
  "summary": "A .psm1 module skeleton with one exported advanced function plus a matching .psd1 manifest skeleton, both PS 5.1 and both parse clean.",
  "keywords": ["PowerShell", "module", "psm1", "psd1", "manifest", "Export-ModuleMember", "RootModule", "FunctionsToExport", "PS 5.1", "advanced function"],
  "source": { "origin": "authored", "note": "PowerShell 5.1; verified to parse" }
}
-->
# PowerShell module (.psm1 + .psd1 manifest)

A module is a `.psm1` (the code) plus an optional `.psd1` manifest (metadata + what to export).
Place both in a folder named for the module (e.g. `MyTool\MyTool.psm1`, `MyTool\MyTool.psd1`) on
`$env:PSModulePath`, then `Import-Module MyTool`.

## Skeleton

`MyTool.psm1` - the module body. Define functions, then export the public ones explicitly. Exporting
by name (not `*`) keeps internal helpers private.

```powershell
# MyTool.psm1 - module body. Public functions are exported at the bottom.

function Get-Greeting {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Name
    )

    process {
        Write-Output ("Hello, " + $Name)
    }
}

# Private helper - intentionally NOT exported.
function Format-Internal {
    param([string] $Text)
    return $Text.Trim()
}

Export-ModuleMember -Function Get-Greeting
```

`MyTool.psd1` - the manifest. It is a PowerShell data file (a single hashtable literal) and must
itself parse as PowerShell. `New-ModuleManifest` generates one; this is the trimmed core.

```powershell
@{
    RootModule        = 'MyTool.psm1'
    ModuleVersion     = '1.0.0'
    GUID              = '00000000-0000-0000-0000-000000000000'
    Author            = 'you'
    Description       = 'One-line description of the module.'
    PowerShellVersion = '5.1'

    # List exports explicitly (not '*') so Import-Module is fast and the surface is intentional.
    FunctionsToExport = @('Get-Greeting')
    CmdletsToExport   = @()
    VariablesToExport = @()
    AliasesToExport   = @()
}
```

## Notes

- Generate a real GUID for the manifest: `[guid]::NewGuid().ToString()` - do not ship the all-zeros
  placeholder.
- `FunctionsToExport` in the manifest and `Export-ModuleMember` in the `.psm1` must agree. The
  manifest gates what `Import-Module` surfaces; keep both lists explicit (avoid `'*'`).
- Test it: `Test-ModuleManifest .\MyTool\MyTool.psd1` then
  `Import-Module .\MyTool\MyTool.psd1 -Force; Get-Greeting -Name world`.
- The function inside is an advanced function (`[CmdletBinding()]` + pipeline `process` block) - see
  the `advanced-function` scaffold for the full template.
- PS 5.1 only: no ternary (`? :`), no null-coalescing (`??`), no pipeline-chain operators
  (`&&`/`||`). Use `if`/`else`.
