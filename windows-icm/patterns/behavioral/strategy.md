<!--icm
{
  "id": "strategy",
  "title": "Strategy pattern (interchangeable algorithms behind an interface)",
  "doc_type": "pattern",
  "summary": "Defines a family of algorithms, encapsulates each behind a common interface, and lets the context swap them at runtime via composition instead of inheritance.",
  "keywords": ["strategy", "IFlyBehaviour", "IQuackBehaviour", "duck", "behavior composition", "favor composition over inheritance", "pluggable algorithm", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/StrategyPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Strategy pattern

## Intent
Define a family of algorithms, encapsulate each one, and make them interchangeable. The context (Duck) holds references to behavior interfaces and delegates to them, so behaviors can be set or swapped at runtime without changing the Duck. This is the canonical "favor composition over inheritance" example.

## When to use
- Several variants of an algorithm/behavior exist and you want to pick one at runtime.
- You want to avoid a tangle of subclasses that differ only in one behavior.
- Behavior should be configurable or swappable per instance.
- NOT when there is only one fixed algorithm that never varies.

## Participants
- Strategy interfaces - `IFlyBehaviour` (`Fly`), `IQuackBehaviour` (`Quack`)
- Concrete strategies - `FlyWings`, `FlyNope`; `QuackNormal`, `QuackSqueak`, `QuackNope`
- Context - `Duck` (holds `IFlyBehaviour` and `IQuackBehaviour`, delegates via `PerformFly`/`PerformQuack`)
- Concrete context - `MallardDuck`
- Client - `Program`
- Namespace note: this project's namespace is `Ducks` (not `StrategyPattern`).

## Key code
```csharp
namespace Ducks
{
    internal interface IFlyBehaviour { void Fly(); }
}

namespace Ducks
{
    internal interface IQuackBehaviour { void Quack(); }
}

using System;
namespace Ducks
{
    class FlyWings : IFlyBehaviour { public void Fly() { Console.WriteLine("Flap Flap"); } }
    class FlyNope  : IFlyBehaviour { public void Fly() { Console.WriteLine("I can't fly"); } }
}

using System;
namespace Ducks
{
    class QuackNormal : IQuackBehaviour { public void Quack() { Console.WriteLine("Quack Quack"); } }
    class QuackSqueak : IQuackBehaviour { public void Quack() { Console.WriteLine("Squeeeak"); } }
    internal class QuackNope : IQuackBehaviour { public void Quack() { Console.WriteLine("..."); } }
}

namespace Ducks
{
    internal class Duck
    {
        private IQuackBehaviour _quacker;
        private IFlyBehaviour _flyer;

        // strategies are injected/swapped through setters (composition)
        public IQuackBehaviour Quacker { set { _quacker = value; } }
        public IFlyBehaviour   Flyer   { set { _flyer = value; } }

        protected void PerformQuack() { _quacker.Quack(); }
        protected void PerformFly()   { _flyer.Fly(); }
    }

    internal class MallardDuck : Duck
    {
        public MallardDuck()
        {
            Flyer = new FlyNope();      // default behaviors set in constructor
            Quacker = new QuackNope();
        }

        public void Display() { PerformFly(); PerformQuack(); }
    }
}
```

## How it wires (from Program.cs)
```csharp
namespace Ducks
{
    internal static class Program
    {
        private static void Main()
        {
            // override the default Quacker via object initializer
            var mallard = new MallardDuck { Quacker = new QuackNormal() };
            mallard.Display();             // FlyNope + QuackNormal

            mallard.Flyer = new FlyWings(); // swap fly strategy at runtime
            mallard.Display();             // FlyWings + QuackNormal
        }
    }
}
```

## C# 5 notes
- This example is already C# 5 compatible apart from implicit usings - the behavior files add `using System;` explicitly, and Program.cs needs none. No string interpolation, `?.`, expression-bodied members, records, or target-typed `new` appear here.
- Object-initializer syntax (`new MallardDuck { Quacker = new QuackNormal() }`) and write-only properties are valid in C# 5.
- The `Duck`/`MallardDuck` context classes happen to live in Program.cs in this project rather than separate files.
