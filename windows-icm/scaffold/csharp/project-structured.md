<!--icm
{
  "id": "project-structured",
  "title": "Structured multi-file project (src/Core + src/Drivers, response.rsp, on-disk memory)",
  "doc_type": "scaffold",
  "group": "csharp",
  "summary": "The stampable template for a managed project: src/Core (pure domain) + src/Drivers (interface-driven adapters) + Program.cs, built write-once with response.rsp via /recurse, plus the project.json and PROJECT.md memory files. Verified to compile with the in-box csc.",
  "keywords": ["project", "scaffold", "template", "src", "Core", "Drivers", "dist", "response.rsp", "recurse", "build.ps1", "project.json", "PROJECT.md", "composition", "interface", "C# 5", "in-box csc", "new project"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc; the example compiles clean via -recurse (verified)" }
}
-->
# Structured multi-file project

This is the **target** structure for a managed project, and the reference the model grounds on when
adding files. Note `/new` stamps a *minimal* starting point (just `Program.cs` plus empty `src/Core`
and `src/Drivers` folders) so a fresh project carries no dead boilerplate; the worked example below
shows what it grows into. It graduates the flat [[console-app]] into the layout from [[project-layout]]:
pure domain in `src/Core`, interface-driven adapters in `src/Drivers`, a thin `Program.cs` composition
root, a write-once `response.rsp` build, and the two on-disk memory files. `Core` depends on nothing;
`Drivers` depends on nothing; `Program` composes both. The example below compiles clean with the
in-box csc via `-recurse`.

```
out/App/
├── build.ps1
├── response.rsp
├── project.json
├── PROJECT.md
├── src/
│   ├── Program.cs
│   ├── Core/Greeter.cs
│   └── Drivers/
│       ├── IOutput.cs
│       └── ConsoleOutput.cs
└── dist/
```

## src/Program.cs — composition root
```csharp
using System;
using App.Core;
using App.Drivers;

namespace App
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string name = args.Length > 0 ? args[0] : "world";
            IOutput output = new ConsoleOutput();   // composition root: swap the driver here
            Greeter greeter = new Greeter();
            output.Write(greeter.Greet(name));
            return 0;
        }
    }
}
```

## src/Core/Greeter.cs — pure domain logic
```csharp
namespace App.Core
{
    // Pure domain logic: no console, no files, no UI. Trivially testable.
    public sealed class Greeter
    {
        public string Greet(string name)
        {
            if (string.IsNullOrEmpty(name)) { name = "world"; }
            return "hello, " + name;
        }
    }
}
```

## src/Drivers/IOutput.cs — the seam
```csharp
namespace App.Drivers
{
    // The seam Core's callers are written against. Implementations are flat siblings here.
    public interface IOutput
    {
        void Write(string line);
    }
}
```

## src/Drivers/ConsoleOutput.cs — one adapter (the template for new ones)
```csharp
using System;

namespace App.Drivers
{
    // One IOutput implementation. To add another (file, network), copy this file as a template.
    public sealed class ConsoleOutput : IOutput
    {
        public void Write(string line) { Console.WriteLine(line); }
    }
}
```

## response.rsp — all build flags (the build never changes as files are added)
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
WinForms variant: replace `/target:exe` with `/target:winexe` and add
`/reference:System.Windows.Forms.dll` and `/reference:System.Drawing.dll`.

## build.ps1
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

## project.json — machine memory
```json
{
  "name": "App",
  "kind": "console",
  "created": "REPLACE_DATE",
  "entry": "src/Program.cs",
  "out": "dist/App.exe",
  "rsp": "response.rsp",
  "assembly": "single",
  "references": ["System.dll", "System.Core.dll"],
  "files": [
    { "path": "src/Program.cs",            "role": "entry / composition root" },
    { "path": "src/Core/Greeter.cs",       "role": "domain: build the greeting" },
    { "path": "src/Drivers/IOutput.cs",    "role": "output seam (interface)" },
    { "path": "src/Drivers/ConsoleOutput.cs", "role": "IOutput impl over the console" }
  ]
}
```

## PROJECT.md — narrative memory
```markdown
# App

## Purpose
One or two lines: what this is and why it exists.

## Architecture
Single exe via /recurse. Core is pure; Drivers holds IOutput implementations. Program composes them.

## Status / TODO
- [x] scaffold + first green build
- [ ] (next feature)

## Changelog
- REPLACE_DATE  scaffolded (console); compiles clean
```

## Notes
- One `Main` only — it lives in `Program.cs`. Keep entry points out of `Core/` and `Drivers/`.
- C# 5 only: no string interpolation, no `?.`, no expression-bodied members. (`?:` is fine, as used
  in `Program.cs`.)
- Add a feature by dropping a file into the right folder; `/recurse` picks it up with no build edit.
  Adapters go in `Drivers/` (copy `ConsoleOutput.cs`); domain logic goes in `Core/`.
