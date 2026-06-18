# patterns/structural/

Composing types into larger structures. Candidates and their idiomatic C# notes:

- **Adapter** - make an incompatible interface usable; common when wrapping a Win32/native API in a
  clean managed class.
- **Decorator** - add behavior by wrapping an interface implementation (e.g. a buffering/ logging stream).
- **Facade** - a simple front over a complicated subsystem (your tool's public API over the internals).
- **Proxy** - stand-in that controls access (lazy load, remoting, guarding).
- **Composite / Bridge / Flyweight** - tree structures, decoupling abstraction from implementation,
  sharing immutable state. Capture only if you actually hit the need.

Each file: `<!--icm {...}-->` block, C# 5 example, the rules, then `icm reindex windows-icm`.
