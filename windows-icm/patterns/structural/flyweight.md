<!--icm
{
  "id": "flyweight",
  "title": "Flyweight pattern (share immutable instances instead of allocating duplicates)",
  "doc_type": "pattern",
  "summary": "Caches and reuses shared instances through a factory so that many logical objects of the same kind are backed by one physical object, cutting allocation.",
  "keywords": ["flyweight", "object sharing", "instance cache", "BeverageFlyweightFactory", "IBeverage", "BeverageType", "memory optimization", "structural pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/FlyweightPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Flyweight pattern

## Intent
Use sharing to support large numbers of fine-grained objects efficiently. A factory keeps a cache keyed by type and hands back an existing instance whenever one already exists, so repeated requests for the same kind reuse one object instead of allocating a new one each time.

## When to use
- An application creates very many objects that are largely interchangeable (here, identical milk-tea instances) and allocation/memory is a concern.
- The shared (intrinsic) state can live on the shared instance; any per-use (extrinsic) state would be passed in by the caller.
- Not worth it when objects are few, or when each object holds unique mutable state that cannot be shared.

## Participants
- Flyweight (the shared type) - `IBeverage` (`Drink()`)
- Concrete Flyweights - `BubbleMilkTea`, `FoamMilkTea`, `OolingMilkTea`, `CoconutMilkTea` (constructors print when an instance is actually built)
- Flyweight Factory (caches and returns shared instances) - `BeverageFlyweightFactory`
- Key - `BeverageType` enum
- Client - `BubbleTeaShop`, driven by `Program`

## Key code
```csharp
using System;
using System.Collections.Generic;

namespace FlyweightPattern
{
    public interface IBeverage
    {
        void Drink();
    }

    public enum BeverageType
    {
        BubbleMilk, FoamMilk, OolongMilk, CoconutMilk
    }

    public class BubbleMilkTea : IBeverage
    {
        public BubbleMilkTea()
        {
            Console.WriteLine("Initializing a Bubble Milk Tea instance");
        }

        public void Drink()
        {
            Console.WriteLine("hmmm... this is bubble milk tea");
        }
    }
    // FoamMilkTea, OolingMilkTea, CoconutMilkTea are the same shape with different text.

    // Factory: builds each concrete flyweight at most once and caches it by type.
    public class BeverageFlyweightFactory
    {
        private readonly Dictionary<BeverageType, IBeverage> _beverages;

        public BeverageFlyweightFactory()
        {
            _beverages = new Dictionary<BeverageType, IBeverage>();
        }

        public IBeverage MakeBeverage(BeverageType type)
        {
            IBeverage beverage;
            _beverages.TryGetValue(type, out beverage);
            if (beverage == null)
            {
                switch (type)
                {
                    case BeverageType.BubbleMilk:
                        beverage = new BubbleMilkTea();
                        _beverages.Add(BeverageType.BubbleMilk, beverage);
                        break;
                    case BeverageType.FoamMilk:
                        beverage = new FoamMilkTea();
                        _beverages.Add(BeverageType.FoamMilk, beverage);
                        break;
                    case BeverageType.OolongMilk:
                        beverage = new OolingMilkTea();
                        _beverages.Add(BeverageType.OolongMilk, beverage);
                        break;
                    case BeverageType.CoconutMilk:
                        beverage = new CoconutMilkTea();
                        _beverages.Add(BeverageType.CoconutMilk, beverage);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("type", type, null);
                }
            }

            return beverage;
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;
using System.Collections.Generic;

namespace FlyweightPattern
{
    public class BubbleTeaShop
    {
        private List<IBeverage> takeAwayOrders;

        public BubbleTeaShop()
        {
            takeAwayOrders = new List<IBeverage>();
            TakeOrders();
        }

        private void TakeOrders()
        {
            var factory = new BeverageFlyweightFactory();

            // Six orders, but only four "Initializing..." lines print: duplicates
            // (two BubbleMilk, two OolongMilk) reuse the cached flyweight.
            takeAwayOrders.Add(factory.MakeBeverage(BeverageType.BubbleMilk));
            takeAwayOrders.Add(factory.MakeBeverage(BeverageType.BubbleMilk));
            takeAwayOrders.Add(factory.MakeBeverage(BeverageType.CoconutMilk));
            takeAwayOrders.Add(factory.MakeBeverage(BeverageType.FoamMilk));
            takeAwayOrders.Add(factory.MakeBeverage(BeverageType.OolongMilk));
            takeAwayOrders.Add(factory.MakeBeverage(BeverageType.OolongMilk));
        }

        public void Enumerate()
        {
            Console.WriteLine("Enumerating take away orders\n");
            foreach (var beverage in takeAwayOrders)
            {
                beverage.Drink();
            }
        }
    }

    static class Program
    {
        private static void Main()
        {
            var teaShop = new BubbleTeaShop();
            teaShop.Enumerate();
        }
    }
}
```

## C# 5 notes
- The source uses an inline out variable: `_beverages.TryGetValue(type, out var beverage);`. Out variable declarations are a C# 7 feature. In C# 5 declare the variable first, then call:
  ```csharp
  IBeverage beverage;
  _beverages.TryGetValue(type, out beverage);
  ```
- The source uses `nameof(type)` in the `ArgumentOutOfRangeException`, a C# 6 feature. Replace with the literal string `"type"` as shown.
- Generics, `Dictionary`, `enum`, `var`, and `switch` are all valid C# 5. Add `using System;` and `using System.Collections.Generic;` explicitly (source relied on implicit usings).
