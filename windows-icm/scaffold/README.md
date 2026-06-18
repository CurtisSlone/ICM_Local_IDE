# scaffold/

Ready-to-fill skeletons to copy - a minimal `csc`-built console app, a WinForms app, a PowerShell
module layout, a `build.ps1`, a P/Invoke wrapper class. Keep them buildable as-is so they are a
known-good starting point.

## Sub-folders (each becomes the entry's `group`)

- `csharp/`     - in-box `csc` build layouts: console (`-t:exe`), WinForms (`-t:winexe`), library
                  (`-t:library`), plus the matching `build.ps1`. Must build under C# 5.
- `powershell/` - a `.psm1` module + manifest skeleton, an advanced-function template.

## Workflow

Lead each file with an `<!--icm {...}-->` block, then `icm reindex windows-icm`. Manifest is generated.
A scaffold's example code must actually compile/parse here - it is a starting point, not pseudocode.
