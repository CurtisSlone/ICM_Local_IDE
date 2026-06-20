<!--icm
{
  "id": "csc-build",
  "title": "Building C# with the in-box csc.exe",
  "doc_type": "reference",
  "summary": "How to compile with the .NET Framework csc.exe (no SDK/NuGet/MSBuild): invocation, references, exe vs library, and the JSON library to use (JavaScriptSerializer, not System.Text.Json).",
  "keywords": ["csc", "compile", "build", "references", "no SDK", "NuGet", "MSBuild", "target exe", "target library", "JavaScriptSerializer", "System.Web.Extensions"],
  "source": { "origin": "authored", "note": "host build model - in-box .NET Framework compiler" }
}
-->
# Building C# with the in-box csc.exe

No .NET SDK, NuGet, or MSBuild here. Compile directly with the .NET Framework compiler.

## The compiler
```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```
(Fall back to `Framework\v4.0.30319\csc.exe` on 32-bit.)

## Typical invocation
```
csc -nologo -noconfig -optimize+ -langversion:5 -target:exe ^
    -reference:System.dll -reference:System.Core.dll ^
    -out:app.exe File1.cs File2.cs
```
- `-noconfig` ignores the machine `csc.rsp` so the build is deterministic (you then reference what
  you need explicitly).
- `-langversion:5` pins the language (this compiler's ceiling anyway).
- `-target:exe` (console), `-target:winexe` (WinForms, no console window), `-target:library` (DLL).
- `mscorlib` is referenced automatically; add others by path or simple name.

## References you will commonly need
- `System.dll`, `System.Core.dll` (LINQ) - almost always.
- `System.Web.Extensions.dll` - for JSON via `System.Web.Script.Serialization.JavaScriptSerializer`.
  **There is no `System.Text.Json`** (that is .NET Core). Use `JavaScriptSerializer`, or
  `DataContractJsonSerializer` from `System.Runtime.Serialization.dll`.
- WinForms: `System.Windows.Forms.dll` + `System.Drawing.dll` (from the GAC).

## Notes
- `csc` treats `/` as an option prefix, so pass file paths with backslashes (or quote them).
- It supports both a one-level wildcard (`src\*.cs`) and a **recursive** one
  (`-recurse:src\*.cs`), which pulls in every nested `.cs` — so a folder tree needs no explicit file
  list. For a multi-file project, drive this from a `response.rsp`; see [[project-layout]]. Note
  `-noconfig` must stay on the command line (it is ignored inside an `.rsp`).
- Exit code 0 = success; diagnostics print to stdout.
