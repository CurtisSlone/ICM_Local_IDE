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

## Capturing a snippet (the `snippet` flow)

To capture something worth keeping, use the `snippet` capture flow - it generates a ready-to-save
snippet file (metadata block + code + notes). From the operator console:

```
/flow snippet a PowerShell function that reads a file as bytes > snippets/powershell/read-bytes.md
/reindex
```

(or just say "capture a snippet that ..." and let the router pick `snippet`). Review the generated
file, then `reindex` makes it routable. The flow is also exposed over MCP as `capture_snippet`.
