<!--icm
{
  "id": "adapter",
  "title": "Adapter pattern (wrap an incompatible type to match an expected interface)",
  "doc_type": "pattern",
  "summary": "Wraps an object with an incompatible interface in an adapter that exposes the interface a client expects, letting otherwise unusable classes work together.",
  "keywords": ["adapter", "wrapper", "object adapter", "IDuck", "ITurkey", "TurkeyAdapter", "interface compatibility", "structural pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/AdapterPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Adapter pattern

## Intent
Convert the interface of a class into another interface clients expect. The adapter lets classes that could not otherwise cooperate (because of mismatched method names/shapes) work together, without changing either side.

## When to use
- You have an existing class (the adaptee) whose interface does not match what your client code needs.
- You want to reuse a class but cannot edit its source, or do not want to.
- Not needed when you control both interfaces and can simply align them, or when a thin facade over many classes is the real goal.

## Participants
- Target (the interface the client codes against) - `IDuck` (`Quack()`, `Fly()`)
- Adaptee (the incompatible type) - `ITurkey` (`Gobble()`, `Fly()`), implemented by `WildTurkey`
- Adapter (implements Target, holds an Adaptee, translates calls) - `TurkeyAdapter`
- Client - `Program.Tester(IDuck duck)`

## Key code
```csharp
using System;

namespace AdapterPattern
{
    public interface IDuck
    {
        void Quack();
        void Fly();
    }

    public interface ITurkey
    {
        void Gobble();
        void Fly();
    }

    class WildTurkey : ITurkey
    {
        public void Gobble()
        {
            Console.WriteLine("Gobble Gobble Gobble");
        }

        public void Fly()
        {
            Console.WriteLine("Flies 100 Metres");
        }
    }

    // The adapter: implements the Target (IDuck), wraps the Adaptee (ITurkey),
    // and maps each Target call onto the Adaptee.
    public class TurkeyAdapter : IDuck
    {
        private readonly ITurkey _turkey;

        public TurkeyAdapter(ITurkey turkey)
        {
            _turkey = turkey;
        }

        public void Quack()
        {
            _turkey.Gobble();
        }

        public void Fly()
        {
            for (var i = 0; i < 5; i++)
            {
                _turkey.Fly();
                Console.WriteLine("Resting..");
            }
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
namespace AdapterPattern
{
    internal static class Program
    {
        private static void Main()
        {
            var turkey = new WildTurkey();
            var adapter = new TurkeyAdapter(turkey);

            Tester(adapter);
        }

        // Client only knows IDuck; the adapter makes a turkey usable here.
        private static void Tester(IDuck duck)
        {
            duck.Fly();
            duck.Quack();
        }
    }
}
```

## C# 5 notes
- This example is C# 5 compatible. The only adjustment versus the source is the implicit-usings reliance: add `using System;` explicitly where `Console` is used (shown above).
- `var` is fine in C# 5 (and earlier from C# 3). No string interpolation, expression-bodied members, or other C#-6+ syntax appears in this project.
