# reference/

Authoritative facts - the grounding the model must not hallucinate. API surfaces, language and tool
constraints, exact flags and signatures. Distill keeping signatures **verbatim**, and carry a
`source` block in the metadata for cite-and-verify.

## Sub-folders (each becomes the entry's `group`)

- `csharp/`     - the C# language under the in-box `csc` (C# 5 subset, build invocation). The
                  existing top-level `csharp-5-subset` and `csc-build` entries can move here.
- `dotnet/`     - .NET Framework 4.8 BCL surfaces you actually call: `System.IO`, `System.Text`,
                  `System.Diagnostics`, `System.Runtime.InteropServices`.
- `powershell/` - Windows PowerShell 5.1 constraints and cmdlet surfaces. (`powershell-5-1` fits here.)
- `win32/`      - native APIs reached via P/Invoke: `kernel32`/`user32`/`psapi` signatures (pinvoke.net).

`host-conventions` is about this host, not a technology, so it stays at the top level (no group).

## Bulk corpora vs. hand-written entries

Big, machine-generated reference (every BCL type, every cmdlet, prose docs) lives in `refdocs/` as a
searchable corpus (`dotnet.json`, `dotnet_prose.json`, `powershell.json`) reached by the `search`
flow node - NOT as thousands of files here. Hand-write a `reference/` entry only for the high-value,
curated facts you want the router to land on directly.

## Workflow

Lead each file with an `<!--icm {...}-->` block, then `icm reindex windows-icm`. Manifest is generated.
