# patterns/creational/

Object construction. Candidates and their idiomatic C# notes:

- **Factory Method / Abstract Factory** - hide which concrete type is built behind an interface.
- **Builder** - assemble a complex object step by step; in C# usually a fluent API returning `this`.
- **Prototype** - copy an existing instance (`ICloneable`, or a copy constructor).
- **Singleton** - one shared instance. Prefer a `static readonly` holder or passing one instance by
  composition over a classic lazy double-checked singleton. Note thread-safety explicitly.

Each file: `<!--icm {...}-->` block, C# 5 example, the rules, then `icm reindex windows-icm`.
