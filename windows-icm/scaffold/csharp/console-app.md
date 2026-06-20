<!--icm
{
  "id": "console-app",
  "title": "Minimal console app (csc -t:exe)",
  "doc_type": "scaffold",
  "group": "csharp",
  "summary": "A ready-to-fill console app with a static Main built by the in-box csc as -t:exe, plus a build.ps1 (modeled on the host's) that globs *.cs and pins -langversion:5.",
  "keywords": ["console", "Main", "csc", "target exe", "build.ps1", "C# 5", "in-box", "entry point", "args"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc; verified to compile/parse" }
}
-->
# Minimal console app (csc -t:exe)

A starting point for a command-line tool: one `static Main`, compiled to a console `.exe` with the
in-box .NET Framework `csc`. No SDK, no NuGet, no MSBuild. Copy `Program.cs` and `build.ps1` into a
folder and run the build.

## Files

`Program.cs` - the entry point. `Main` returns an `int` so the process can set an exit code (0 =
success), which the host's verify loops key on.

```csharp
using System;

namespace App
{
    internal static class Program
    {
        // Returning int lets the process report an exit code. Keep 0 = success.
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: app <name>");
                return 2;
            }

            Console.WriteLine("hello, " + args[0]);
            return 0;
        }
    }
}
```

`build.ps1` - modeled on the host's build script: locate the in-box `csc`, glob the `.cs` files, and
invoke with `-noconfig -langversion:5 -target:exe`. One level of wildcard is fine for a flat folder;
enumerate explicitly if you add nested folders (csc has no recursive glob).

```powershell
# build.ps1 - compile this console app with the in-box .NET Framework csc.exe.
# No SDK, no NuGet, no MSBuild. -noconfig ignores the machine csc.rsp so the build is
# deterministic; -langversion:5 pins the language to what this pre-Roslyn compiler supports.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework C# compiler) not found." }

# Flat folder: one-level glob is enough. For a nested tree, use -recurse:src\*.cs instead
# (the in-box csc supports it) or move to a response.rsp project layout - see project-layout.
$src = Get-ChildItem "$root\*.cs" | ForEach-Object { $_.FullName }

& $csc -nologo -noconfig -optimize+ -langversion:5 -warn:4 -target:exe -platform:anycpu `
    "-reference:System.dll" "-reference:System.Core.dll" `
    "-out:$root\app.exe" $src
if ($LASTEXITCODE -ne 0) { throw "build failed (csc exit $LASTEXITCODE)" }
Write-Host "built $root\app.exe"
```

## Build

Direct invocation (what `build.ps1` runs):

```
csc.exe -nologo -noconfig -optimize+ -langversion:5 -target:exe ^
    -reference:System.dll -reference:System.Core.dll ^
    -out:app.exe Program.cs
```

`csc` = `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (fall back to `Framework\...` on
32-bit). `mscorlib` is referenced automatically.

## Notes

- Exactly one `Main` per executable. If you add more `.cs` files, keep their entry points out (or
  use `-main:App.Program` to disambiguate).
- This is the single-file/flat starting point. For a structured multi-file project (src/Core,
  src/Drivers, dist/, response.rsp, on-disk memory) graduate to the project-layout reference.
- `csc` treats `/` as an option prefix - pass paths with backslashes or quote them.
- C# 5 only: no string interpolation (`$"..."`), no expression-bodied members, no `?.`. Use string
  concatenation as shown.
- Stay disciplined about the exit code: a verify loop (e.g. compile-then-run) reads it.
