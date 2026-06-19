<!--icm
{
  "id": "bridge",
  "title": "Bridge pattern (split an abstraction from its implementation so both vary independently)",
  "doc_type": "pattern",
  "summary": "Separates an abstraction (what) from its implementation (how) by composition so the two hierarchies can be extended independently instead of multiplying subclasses.",
  "keywords": ["bridge", "abstraction implementation split", "IWeapon", "IEnchantment", "Sword", "Hammer", "composition over inheritance", "structural pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/BridgePattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Bridge pattern

## Intent
Decouple an abstraction from its implementation so the two can vary independently. Instead of one class hierarchy that bakes in both, you keep two hierarchies (abstraction and implementor) and wire them together by composition.

## When to use
- You have two independent dimensions of variation (here: weapon type vs enchantment behaviour) and inheritance would force a class per combination (SwordWithFlying, HammerWithSoulEating, ...).
- You want to swap the implementation at runtime by injecting a different implementor.
- Not needed when there is only one dimension of variation, or the implementation never changes.

## Participants
- Abstraction (the interface clients hold; delegates to the implementor) - `IWeapon` (`Wield()`, `Swing()`, `Unwield()`, `GetEnchantment()`)
- Refined Abstractions - `Sword`, `Hammer` (each holds an `IEnchantment`)
- Implementor (the interface the abstraction delegates to) - `IEnchantment` (`OnActivate()`, `Apply()`, `OnDeactivate()`)
- Concrete Implementors - `FlyingEnchantment`, `SoulEatingEnchantment`

## Key code
```csharp
using System;

namespace BridgePattern
{
    // Implementor side
    public interface IEnchantment
    {
        void OnActivate();
        void Apply();
        void OnDeactivate();
    }

    // Abstraction side
    public interface IWeapon
    {
        void Wield();
        void Swing();
        void Unwield();
        IEnchantment GetEnchantment();
    }

    public class FlyingEnchantment : IEnchantment
    {
        public void OnActivate()
        {
            Console.WriteLine("The item begins to glow faintly.");
        }

        public void Apply()
        {
            Console.WriteLine("The item flies and strikes the enemies finally returning to owner's hand.");
        }

        public void OnDeactivate()
        {
            Console.WriteLine("The item's glow fades.");
        }
    }

    // Refined abstraction: holds an IEnchantment (the bridge) and delegates to it.
    public class Sword : IWeapon
    {
        private readonly IEnchantment _enchantment;

        public Sword(IEnchantment enchantment)
        {
            _enchantment = enchantment;
        }

        public void Wield()
        {
            Console.WriteLine("The sword is wielded.");
            _enchantment.OnActivate();
        }

        public void Swing()
        {
            Console.WriteLine("The sword is swinged.");
            _enchantment.Apply();
        }

        public void Unwield()
        {
            Console.WriteLine("The sword is unwielded.");
            _enchantment.OnDeactivate();
        }

        public IEnchantment GetEnchantment()
        {
            return _enchantment;
        }
    }

    // Hammer is the same shape as Sword with different wording; SoulEatingEnchantment
    // is the same shape as FlyingEnchantment. Any weapon pairs with any enchantment.
}
```

## How it wires (from Program.cs)
```csharp
namespace BridgePattern
{
    internal static class Program
    {
        private static void Main()
        {
            IWeapon sword = new Sword(new FlyingEnchantment());
            sword.Wield();
            sword.Swing();
            sword.Unwield();

            IWeapon hammer = new Hammer(new SoulEatingEnchantment());
            hammer.Wield();
            hammer.Swing();
            hammer.Unwield();
        }
    }
}
```

## C# 5 notes
- This example is C# 5 compatible. Add `using System;` explicitly in each file that uses `Console` (the source relied on implicit usings).
- No string interpolation, expression-bodied members, or other C#-6+ syntax is used. `var` and `readonly` fields are fine in C# 5.
