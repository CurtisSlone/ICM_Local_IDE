<!--icm
{
  "id": "csharp-5-subset",
  "title": "C# 5 language subset (what the in-box csc allows)",
  "doc_type": "reference",
  "summary": "Which modern C# features are NOT available under the in-box csc (langversion 5) - no string interpolation, expression-bodied members, ?., records - and the C# 5 way to write each.",
  "keywords": ["c# 5", "langversion 5", "csc", "string interpolation", "expression-bodied", "null-conditional", "records", "nameof", "using static", "tuples"],
  "source": { "origin": "authored", "note": "host environment constraint - the in-box pre-Roslyn csc caps at C# 5" }
}
-->
# C# 5 language subset (in-box csc)

This environment compiles with the **.NET Framework `csc.exe`** that ships in Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`). It is the **pre-Roslyn** compiler and
caps at **C# 5**. A model trained on modern C# will reach for features that do not compile here.
Use the C# 5 form instead.

## Not available (and the C# 5 way)

| Modern feature | Not allowed | C# 5 instead |
| --- | --- | --- |
| String interpolation | `$"hi {name}"` | `"hi " + name` or `string.Format("hi {0}", name)` |
| Expression-bodied members | `int X => 1;` | `int X { get { return 1; } }` |
| Null-conditional | `obj?.Foo` | `obj != null ? obj.Foo : null` |
| Null-coalescing assignment | `x ??= y;` | `if (x == null) x = y;` |
| `nameof(x)` | `nameof(x)` | the literal string `"x"` |
| Auto-property initializer | `int X { get; } = 1;` | a field, or set it in the constructor |
| Getter-only auto-property | `int X { get; }` | `int X { get; private set; }` |
| `using static` | `using static System.Math;` | `System.Math.Sqrt(...)` |
| Records / target-typed `new` / tuples `(a, b)` | — | classes; `new T(...)`; `out` params or a small class |
| `var` in some positions, pattern matching `is T t` | limited | explicit types; `is` + cast |

## Available (C# 5 and earlier)
Generics, LINQ, lambdas, `var` (local inference), `async`/`await`, iterators (`yield`), nullable value
types (`int?`), extension methods, `dynamic`, object/collection initializers.

## Quick gut-check
If a feature arrived in C# 6 or later (interpolation, `?.`, expression bodies, `nameof`, records,
`using static`, tuples), it will NOT compile. When unsure, write the verbose, older form - the
compiler is the oracle and will reject anything newer.
