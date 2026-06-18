# patterns/

The "right way to do a recurring thing" - design patterns and C# idioms. A pattern file says *how to
structure* code, with a small canonical example and the rules. (For a fact, use `reference/`; for a
whole task, use `recipes/`; for one copy-paste fragment, use `snippets/`.)

## Sub-folders (each becomes the entry's `group`)

- `creational/`    - object construction: Factory Method, Abstract Factory, Builder, Prototype, Singleton.
- `structural/`    - composing types: Adapter, Bridge, Composite, Decorator, Facade, Flyweight, Proxy.
- `behavioral/`    - object interaction: Strategy, Observer, Command, State, Template Method, Iterator,
                     Mediator, Chain of Responsibility, Visitor, Memento, Interpreter.
- `csharp-idioms/` - NOT GoF, but how idiomatic C# is actually written: Dispose, TryParse, fluent
                     builders, RAII via `using`, async/await usage, immutability, the options pattern.

## Capture the C# realization, not the textbook Java form

Several GoF patterns are **language features** in C#. When you capture one, show the idiomatic C#
version and note when the full ceremony is unnecessary:

| GoF pattern        | Idiomatic C#                                              |
| ------------------ | -------------------------------------------------------- |
| Iterator           | `IEnumerable<T>` + `yield return`                        |
| Observer           | `event` / `EventHandler` (or `IObservable<T>`)           |
| Strategy / Command | a delegate - `Func<...>` / `Action<...>`                 |
| Singleton          | a `static` holder, or one instance passed by composition |
| Template Method    | an `abstract` base with overridable steps                |

## C# 5 constraint

Examples must compile with the in-box `csc` (C# 5): no string interpolation, expression-bodied
members, `?.`, records, or pattern matching. See `reference/csharp-5-subset`.

## Workflow

Each pattern file leads with an `<!--icm {...}-->` metadata block (copy `csharp-idioms/disposable-pattern.md`),
then `icm reindex windows-icm` makes it routable. Do not hand-edit `manifest.json` - it is generated.
