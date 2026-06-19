<!--icm
{
  "id": "decorator",
  "title": "Decorator pattern (add responsibilities to an object by wrapping it at runtime)",
  "doc_type": "pattern",
  "summary": "Wraps an object in one or more decorators that share its type and add behaviour, letting you layer responsibilities at runtime instead of subclassing every combination.",
  "keywords": ["decorator", "wrapper", "Beverage", "CondimentDecorator", "Espresso", "MochaCondiment", "WhipCondiment", "runtime composition", "structural pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/DecoratorPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Decorator pattern

## Intent
Attach additional responsibilities to an object dynamically. Decorators share the component's type and hold a reference to a wrapped component, so they can be stacked to extend behaviour without changing the base class and without a subclass per combination.

## When to use
- You want to add or combine features at runtime (a coffee with mocha, mocha, and whip) where a class per combination would explode.
- The added behaviour should be transparent to clients, which still see the base type.
- Not ideal when there are many small decorators making debugging hard, or when behaviour is fixed at compile time (plain subclassing is simpler).

## Participants
- Component (abstract base type all participants share) - `Beverage` (`Description`, `Cost()`)
- Concrete Components (the wrapped-by-default objects) - `Espresso`, `DarkRoast`, `HouseBlend`
- Decorator (abstract; same type as Component, forces re-implementing Description) - `CondimentDecorator`
- Concrete Decorators (each holds a `Beverage` and adds to Cost/Description) - `MochaCondiment`, `WhipCondiment`

## Key code
```csharp
namespace DecoratorPattern
{
    // Component
    abstract class Beverage
    {
        protected string _description = "No Description";
        public abstract string Description { get; }
        public abstract double Cost();
    }

    // Decorator base: same type as Beverage, so a decorator can wrap any Beverage
    // (including another decorator).
    abstract class CondimentDecorator : Beverage
    {
        public abstract override string Description { get; }
    }

    // Concrete component
    class Espresso : Beverage
    {
        public Espresso()
        {
            _description = "Espresso";
        }

        public override string Description
        {
            get { return _description; }
        }

        public override double Cost()
        {
            return 1.99;
        }
    }

    // Concrete decorator: wraps a Beverage and adds to its cost/description.
    class MochaCondiment : CondimentDecorator
    {
        Beverage _beverage;

        public MochaCondiment(Beverage beverage)
        {
            this._beverage = beverage;
        }

        public override string Description
        {
            get
            {
                if (_beverage.Description.StartsWith("Mocha"))
                {
                    return "Double " + _beverage.Description;
                }
                else
                    return "Mocha " + _beverage.Description;
            }
        }

        public override double Cost()
        {
            return 0.2 + _beverage.Cost();
        }
    }

    // WhipCondiment is the same shape with "Whip" and a 0.15 surcharge.
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace DecoratorPattern
{
    static class Program
    {
        static void Main()
        {
            Beverage beverage = new Espresso();
            Console.WriteLine(beverage.Description + " $" + beverage.Cost());

            // Stack decorators: each wraps the previous Beverage.
            Beverage beverage2 = new DarkRoast();
            beverage2 = new MochaCondiment(beverage2);
            beverage2 = new MochaCondiment(beverage2);   // -> "Double Mocha ..."
            beverage2 = new WhipCondiment(beverage2);
            Console.WriteLine(beverage2.Description + " $" + beverage2.Cost());

            Beverage beverage3 = new HouseBlend();
            beverage3 = new MochaCondiment(beverage3);
            beverage3 = new WhipCondiment(beverage3);
            Console.WriteLine(beverage3.Description + " $" + beverage3.Cost());
        }
    }
}
```

## C# 5 notes
- The concrete components and `MochaCondiment.Description` are fine, except the components use expression-bodied properties (`public override string Description => _description;`), a C# 6 feature. Rewrite as a property with a getter body:
  ```csharp
  public override string Description
  {
      get { return _description; }
  }
  ```
  Apply to `Espresso`, `DarkRoast`, and `HouseBlend`. `MochaCondiment` and `WhipCondiment` already use a full getter body and need no change.
- `abstract override string Description { get; }` on `CondimentDecorator` is valid C# 5.
- Add `using System;` in `Program.cs` (uses `Console`); the other files do not need it. Source relied on implicit usings.
