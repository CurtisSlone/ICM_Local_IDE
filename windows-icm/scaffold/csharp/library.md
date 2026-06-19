<!--icm
{
  "id": "library",
  "title": "Class library (csc -t:library)",
  "doc_type": "scaffold",
  "group": "csharp",
  "summary": "A ready-to-fill class library (no Main) compiled to a DLL with the in-box csc as -t:library, with a public type and the exact reference line.",
  "keywords": ["library", "DLL", "csc", "target library", "class library", "C# 5", "in-box", "no Main", "reference"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc; verified to compile/parse" }
}
-->
# Class library (csc -t:library)

A reusable DLL with no entry point. Compile with the in-box `csc` as `-target:library`, then
reference the resulting `.dll` from a console/WinForms build (`-reference:Greeter.dll`). Copy
`Greeter.cs` and adjust.

## Files

`Greeter.cs` - one public type with a public surface. A library has no `Main`.

```csharp
using System;

namespace MyLib
{
    // A library exposes public types for other assemblies to reference. No Main.
    public class Greeter
    {
        private readonly string _salutation;

        public Greeter(string salutation)
        {
            if (string.IsNullOrEmpty(salutation))
            {
                throw new ArgumentException("salutation must be non-empty", "salutation");
            }
            _salutation = salutation;
        }

        public string Greet(string name)
        {
            if (string.IsNullOrEmpty(name)) { name = "world"; }
            return _salutation + ", " + name;
        }
    }
}
```

## Build

```
csc.exe -nologo -noconfig -optimize+ -langversion:5 -target:library ^
    -reference:System.dll -reference:System.Core.dll ^
    -out:MyLib.dll Greeter.cs
```

`csc` = `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. The output is `MyLib.dll`; there is
no exe and nothing to run. A `build.ps1` for a library is the console scaffold's script with
`-target:library` and `-out:...\MyLib.dll`.

## Notes

- Consuming the DLL: add `-reference:MyLib.dll` (by simple name if it sits beside the source, else by
  full path) to the exe's `csc` line.
- `-target:library` produces a DLL even if the code happens to contain a `Main` - that `Main` is just
  ignored.
- C# 5 only: validate args the old way (`string.IsNullOrEmpty`, throw `ArgumentException` with the
  parameter name as the second arg). No interpolation, no `?.`, no expression-bodied members.
- Keep the public surface deliberate - everything `public` is part of the contract callers depend on.
