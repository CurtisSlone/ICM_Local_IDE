<!--icm
{
  "id": "csharp-control-flow",
  "title": "Conditionals vs switch (C# 5)",
  "doc_type": "reference",
  "group": "csharp",
  "summary": "When to branch with if/else vs switch in C# 5: switch to dispatch one expression against many constant int/char/string/enum cases (always with a default and explicit break); if/else for ranges, compound predicates, or few branches. C# 5 has NO switch patterns, no when-guards, and no switch expressions; for a data-to-data map prefer a Dictionary over a long chain, and for type-based dispatch prefer polymorphism.",
  "keywords": ["if", "else", "switch", "case", "default", "break", "control flow", "branching", "enum", "dictionary lookup", "pattern matching", "fall through", "polymorphism", "C# 5"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc control-flow conventions" }
}
-->
# Conditionals vs switch (C# 5)

Pick the branch construct by what you are testing - then respect what C# 5 actually supports.

## Use `switch` when

You are dispatching on the **value of one expression** against several **compile-time constant**
cases (`int`, `char`, `string`, or an `enum`). This is the common shape in this codebase: the
calculator's operator, the KvStore CLI's `set`/`get`/`rm`/`keys`, the color-picker index.

```csharp
switch (command)              // one expression, constant cases
{
    case "set": store.Set(key, value); break;
    case "get": Console.WriteLine(store.Get(key)); break;
    case "rm":  store.Delete(key); break;
    default:    Console.Error.WriteLine("unknown command"); return 2;   // always handle the rest
}
```

Rules that matter:
- **Three or more discrete values** is the threshold; below that, `if`/`else` is usually clearer.
- **Always include `default`** - fail loud or pick a safe fallback for unexpected values.
- **Every case ends with `break`, `return`, `throw`, or `goto case`.** C# 5 forbids silent
  fall-through; the only allowed fall-through is **empty stacked labels** for shared handling:
  ```csharp
  case 'a': case 'e': case 'i': case 'o': case 'u': isVowel = true; break;
  ```

## Use `if` / `else` when

- The test is a **range or comparison** (`x > 10`, `i >= 0 && i < n`) - switch cannot do ranges.
- The branches test **different variables or compound predicates** (`a && (b || c)`).
- There are only **one or two** branches.

```csharp
if (args.Length != 3) { ...usage...; return 2; }
else if (secondNumber == 0) { ...divide-by-zero...; return 1; }
```

## C# 5 limits (the model often reaches past these)

The in-box `csc` is **langversion 5**. These do NOT compile and must be avoided:
- **No pattern matching in `switch`** - cases are constants only; no `case int n:`, no type patterns.
- **No `when` guards** on cases.
- **No switch expressions** (`x switch { ... }` is C# 8).
- So **you cannot `switch` on a type.** For type-based dispatch, prefer **polymorphism** (a virtual
  method overridden per subclass); if you must test types, use an `if (x is T)` chain.

## Prefer a lookup table over a long chain

When the branch is really a **data-to-data map** (a key selects a value, not behavior), a
`Dictionary<TKey,TValue>` is clearer and faster than a big `switch`/`if`-chain and needs no edit to
add entries:

```csharp
// instead of switch (name) { case "Red": return Color.Red; ... }
private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>()
{
    { "Black", Color.Black }, { "Red", Color.Red }, { "Blue", Color.Blue }, { "Green", Color.Green }
};
Color c; if (!Colors.TryGetValue(name, out c)) c = Color.Black;
```

Keep `switch` for dispatching **behavior** (different code per case); use a dictionary for mapping
**data**.
