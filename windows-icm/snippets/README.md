# snippets/

The smallest copy-paste units - one exact, known-good fragment with real signatures. A single
P/Invoke `[DllImport]` line, a `JavaScriptSerializer` round-trip, a hex-dump helper, a regex. Smaller
than a pattern; grab-and-go. If it needs explanation of *how to structure*, it is a pattern, not a
snippet.

## Sub-folders (each becomes the entry's `group`)

- `csharp/`     - C# fragments (C# 5 compatible).
- `powershell/` - PowerShell 5.1 fragments (mind the 5.1 gotchas: no ternary/`??`, `-Encoding Byte`).

## Workflow

Lead each file with an `<!--icm {...}-->` block, then `icm reindex windows-icm`. Manifest is generated.
Keep signatures verbatim and add a `source` when the fragment came from official docs or real code.
