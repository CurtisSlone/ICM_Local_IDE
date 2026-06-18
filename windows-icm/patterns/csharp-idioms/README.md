# patterns/csharp-idioms/

Not GoF - the patterns that are simply *how idiomatic C# is written*. These earn their keep more
often than the textbook GoF patterns. Candidates:

- **Dispose / IDisposable** - deterministic cleanup + `using`. (See `disposable-pattern`.)
- **TryParse / Try-pattern** - `bool TryX(in, out result)` instead of throwing on expected failure.
- **Fluent builder** - methods return `this` to chain configuration.
- **Options object** - pass a single settings object instead of many parameters (no optional/named
  args ceremony, and C# 5 has no record/init).
- **RAII via `using`** - scope-bound acquisition/release.
- **Async/await usage** - C# 5 HAS `async`/`await`; capture correct usage (no `async void`, no
  `.Result` deadlocks) since the model often gets this wrong.
- **Immutability** - readonly fields + constructor assignment (no `record`/`init` under C# 5).

Each file: `<!--icm {...}-->` block, C# 5 example, the rules, then `icm reindex windows-icm`.
