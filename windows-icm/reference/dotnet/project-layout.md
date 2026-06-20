<!--icm
{
  "id": "project-layout",
  "title": "Project layout for in-box csc (structure, response file, memory)",
  "doc_type": "reference",
  "group": "dotnet",
  "summary": "How a multi-file project is laid out, built, and remembered when driving the in-box csc.exe (no SDK/MSBuild): the src/lib/dist blueprint, a verified response.rsp using /recurse, and the on-disk memory contract (project.json + PROJECT.md) that keeps project state out of the model's context window.",
  "keywords": ["project", "directory structure", "layout", "response file", "rsp", "recurse", "src", "dist", "lib", "Core", "Drivers", "high cohesion", "loose coupling", "composition", "project.json", "PROJECT.md", "memory", "context management", "csc", "no MSBuild"],
  "source": { "origin": "authored", "note": "blueprint adapted from general csc.exe automation guidance, corrected for in-box C# 5; /recurse + response-file behavior verified on this box" }
}
-->
# Project layout for in-box csc (structure, response file, memory)

A "project" here is **a folder of `.cs` files + a `response.rsp` + a `build.ps1` + two memory files**.
There is no `.csproj`, MSBuild, or NuGet — the in-box `csc.exe` is driven directly (see
[[csc-build]]). The layout does double duty: it enforces normal software-engineering boundaries
(high cohesion, loose coupling) **and** it is scannable by a local model so the project's state lives
on disk, not in the model's context window.

Projects live under `out/<name>/` in the instance.

## The blueprint

```
out/App/
├── build.ps1            # orchestrator: runs `csc -noconfig @response.rsp`
├── response.rsp         # all compiler flags (the build never changes as files are added)
├── project.json         # machine memory: kind, entry, refs, file manifest + roles
├── PROJECT.md           # narrative memory: purpose, decisions, status, changelog
│
├── src/                 # pure source — no build artifacts ever land here
│   ├── Core/            # HIGH COHESION: single-responsibility domain logic, no I/O or UI
│   │   ├── Model.cs
│   │   └── Parser.cs
│   ├── Drivers/         # COMPOSITION: interface-driven adapters as flat siblings
│   │   ├── FileDriver.cs
│   │   └── NetworkDriver.cs
│   └── Program.cs       # thin entry point that composes Core + Drivers
│
├── lib/                 # direct DLL dependencies (rare here; mostly in-box refs)
└── dist/                # ALL output (App.exe, App.pdb) — isolated from src
```

### Why each rule (and why it matters for the model)
- **`Core/` is pure logic, no infrastructure.** The model reads `Core/` to learn *what* the app does
  without wading through *how* data is fetched or written. This is interface-driven composition — see
  the Strategy / Adapter patterns under `patterns/`.
- **`Drivers/` groups implementations of one interface as flat siblings.** To add an adapter, feed
  the model **one** sibling as a template; it doesn't need the rest of the app to write a compliant
  pluggable module.
- **No artifacts in `src/` — output only to `dist/`.** When the context loader crawls the tree it
  never scrapes binaries or stale build noise into the prompt.

## The build: one write-once command

The build is `csc.exe -noconfig @response.rsp`. Because flags live in the response file and source is
pulled in with `/recurse`, **the build script never changes as you add files** — the folder tree
dictates the architecture and the compiler adapts.

`response.rsp` (console; paths are relative to the build's working directory):
```
/nologo
/target:exe
/langversion:5
/warn:4
/reference:System.dll
/reference:System.Core.dll
/out:dist\App.exe
/recurse:src\*.cs
```

WinForms variant (no console window): swap `/target:exe` for `/target:winexe` and add
`/reference:System.Windows.Forms.dll` + `/reference:System.Drawing.dll`.

`build.ps1`:
```powershell
# Compile the whole project with the in-box csc via the response file.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe not found." }

New-Item -ItemType Directory -Force "$root\dist" | Out-Null
Push-Location $root            # rsp paths (src\, dist\) are relative to CWD
try {
    & $csc -noconfig "@response.rsp"
    if ($LASTEXITCODE -ne 0) { throw "build failed (csc exit $LASTEXITCODE)" }
} finally { Pop-Location }
Write-Host "built $root\dist\App.exe"
```

### Response-file gotchas (verified on this box)
- **`/noconfig` must be on the command line, not in the rsp** — inside the file it is ignored with
  warning `CS2023`. That is why `build.ps1` passes `-noconfig` and `@response.rsp` separately.
- **Use backslashes in paths** (`src\*.cs`, `dist\App.exe`). `csc` treats `/` as an option prefix, so
  `src/*.cs` is mis-parsed.
- **`/recurse:src\*.cs` is supported** by the in-box compiler and pulls in every nested `.cs`. (This
  corrects older notes that said in-box csc has no recursive glob — it does.)
- One `Main` per executable. Keep entry points out of `Core/`/`Drivers/`; use `/main:App.Program` to
  disambiguate if needed.

## Single exe vs. decoupled DLLs

Default to a **single exe via `/recurse`** — zero inter-assembly reference management and a write-once
build. Only split `Core/` into its own `/target:library` DLL once it must be reused by something else;
at that point each subtree gets its own `response.rsp` and the exe references the produced DLL. Record
the choice in `project.json` (`"assembly": "single" | "multi"`) and in `PROJECT.md`.

## Project memory (state lives on disk, not in context)

The two memory files are what let the host **manage** a project across turns without depending on the
model remembering anything. They are the backbone of the design.

### `project.json` — machine state (tool-owned)
Deterministic facts the tools read/write. The file *list* can be re-derived by scanning `src/`; the
per-file **roles** are the model-authored value the scan can't recover, so they are persisted here.
```json
{
  "name": "App",
  "kind": "console",
  "created": "2026-06-19",
  "entry": "src/Program.cs",
  "out": "dist/App.exe",
  "rsp": "response.rsp",
  "assembly": "single",
  "references": ["System.dll", "System.Core.dll"],
  "files": [
    { "path": "src/Program.cs",      "role": "entry / composition root" },
    { "path": "src/Core/Parser.cs",  "role": "domain: tokenize + parse input" },
    { "path": "src/Drivers/FileDriver.cs", "role": "IStore impl over the filesystem" }
  ]
}
```
A read tool reconciles `files` against a scan of `src/`: paths missing from disk and files on disk
missing a role are both flagged so the manifest never silently drifts.

### `PROJECT.md` — narrative memory (model-owned, human-readable)
What the model reads at the start of a project turn and appends to after a green build. Fixed sections:
```markdown
# App

## Purpose
One or two lines: what this is and why it exists.

## Architecture
Key decisions: single-exe vs multi-DLL, the interfaces in Drivers/, anything non-obvious.

## Status / TODO
- [x] scaffold + first green build
- [ ] add NetworkDriver

## Changelog
- 2026-06-19  scaffolded (console); Parser + FileDriver compile clean
```

## Context-loading protocol (how to read a project without dumping it)

Never load the whole project into the prompt. Load in tiers; let the embedder rank which Tier-1/2
files are relevant to the request (same RRF used for the KB):

- **Tier 0 — always (cheap orientation):** `project.json` + `PROJECT.md` + the `src/` tree as paths
  only. This alone tells the model what exists, each file's role, and recent history.
- **Tier 1 — on demand:** contents of `Core/` when reasoning about *what* the app does.
- **Tier 2 — template:** exactly one `Drivers/` sibling when adding a new adapter.
- **Tier 3 — focused edit:** the target file plus the files it directly references.

The compiler remains the oracle: correctness comes from `csc -noconfig @response.rsp` exiting 0, not
from the model holding the project in its head. Even with lossy context, a broken edit fails the build
and is repaired — the ICM thesis applied to a multi-file project.
