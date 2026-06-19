<!--icm
{
  "id": "factory-method",
  "title": "Factory Method pattern (defer product creation to subclasses)",
  "doc_type": "pattern",
  "summary": "Defines a creator with a fixed algorithm that calls an abstract factory method, letting each subclass decide which concrete product to instantiate.",
  "keywords": ["factory method", "PizzaFactory", "NyPizzaFactory", "ChicagoPizzaFactory", "Pizza", "creator", "creational pattern", "template plus creation"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/FactoryPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Factory Method pattern

## Intent
A base creator class defines the steps of an operation (`Order`) but leaves the actual object creation to an abstract method (`Create`). Subclasses override that method to pick the concrete product. This lets the regional factories vary which `Pizza` they build without changing the ordering algorithm.

## When to use
- A class cannot anticipate which concrete type it must create, and you want subclasses to supply that decision.
- You want to keep a fixed processing algorithm in the base class while varying only the product instantiation.
- Not when a single product type is enough - a plain constructor or simple factory function is simpler.

## Participants
- Creator (abstract) - `PizzaFactory` (defines `Order`, declares `protected abstract Pizza Create(PizzaType type)`)
- Concrete creators - `NyPizzaFactory`, `ChicagoPizzaFactory`
- Product (abstract) - `Pizza`
- Concrete products - `CheesePizza`, `ClamPizza`, `VeggiePizza`

Note: in this example each concrete `Pizza` is itself fed an `IIngredientsFactory` (the Abstract Factory layer - see abstract-factory.md). The two patterns are wired together here.

## Key code
```csharp
using System;

namespace FactoryPattern
{
    // Helper.cs declares the discriminator enum
    internal enum PizzaType { Cheese, Clam }

    abstract class Pizza
    {
        public string Color;

        internal abstract void Prepare();
        internal void Bake()  { Console.WriteLine("Baking at 135 degree Celsius for 20 minutes"); }
        internal void Cut()   { Console.WriteLine("Cutting into diagonal pieces"); }
        internal void Box()   { Console.WriteLine("Putting pizza in " + Color + " coloured box"); }

        public string Name { protected get; set; }
    }

    // The creator: fixed algorithm in Order(), creation deferred to Create().
    abstract class PizzaFactory
    {
        public Pizza Order(PizzaType type)
        {
            var pizza = Create(type);
            pizza.Prepare();
            pizza.Bake();
            pizza.Cut();
            pizza.Box();
            return pizza;
        }

        protected abstract Pizza Create(PizzaType type);
    }

    class NyPizzaFactory : PizzaFactory
    {
        private readonly string nyStyleCheese = "NY Style Cheese";
        private readonly string nyStyleClam   = "NY Style Clam";
        private readonly string nyStyleVeggie = "NY Style Veggie";

        protected override Pizza Create(PizzaType type)
        {
            Pizza pizza;
            IIngredientsFactory ingredients = new NyIngredientsFactory();

            if (type == PizzaType.Cheese)
            {
                pizza = new CheesePizza(ingredients);
                pizza.Name = nyStyleCheese;
            }
            else if (type == PizzaType.Clam)
            {
                pizza = new ClamPizza(ingredients);
                pizza.Name = nyStyleClam;
            }
            else
            {
                pizza = new VeggiePizza(ingredients);
                pizza.Name = nyStyleVeggie;
            }
            pizza.Color = "blue";
            return pizza;
        }
    }

    // ChicagoPizzaFactory is the same shape, using new ChicagoIngredientsFactory(),
    // Chicago names, and pizza.Color = "red".

    class CheesePizza : Pizza
    {
        readonly IIngredientsFactory _ingredients;
        public CheesePizza(IIngredientsFactory ing) { _ingredients = ing; }

        internal override void Prepare()
        {
            Console.WriteLine("Preparing " + Name + " Using");
            Console.Write("Dough: " + _ingredients.CreateDough().Name +
                          ", Cheese: " + _ingredients.CreateCheese().Name +
                          ", Sauce: " + _ingredients.CreateSauce().Name + ", Veggies: ");
            Console.WriteLine();
            foreach (var val in _ingredients.CreateVeggies())
            {
                Console.Write(val.Name + " ");
            }
            Console.WriteLine();
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace FactoryPattern
{
    static class Program
    {
        static void Main()
        {
            Console.WriteLine("Yankees fan orders:");
            var yankees = new NyPizzaFactory();
            yankees.Order(PizzaType.Cheese);

            Console.WriteLine();
            Console.WriteLine("Cubs fan orders:");
            var cubs = new ChicagoPizzaFactory();
            cubs.Order(PizzaType.Clam);
        }
    }
}
```

## C# 5 notes
- Add `using System;` (for `Console`) and `using System.Collections.Generic;` (for `IEnumerable<IVeggies>` on the ingredients factory). The source relies on implicit usings.
- `PizzaFactory.cs`, `NYPizzaFactory.cs`, `ChicagoPizzaFactory.cs`, and the `Pizza` subclasses are already C# 5 compatible (abstract classes, `internal`/`protected` members, `override`, `var`, `foreach`).
- The source `NYPizzaFactory.cs` uses an object initializer with `new CheesePizza(ingredients) { Name = nyStyleCheese }`. Object initializers are C# 3 - fine. The Chicago variant already uses the property-assignment form shown above.
- `Helper.cs` carries `using System.Linq;` and `using System.Threading.Tasks;` that are unused here; harmless in C# 5.
- No interpolation, expression-bodied members, or other C# 6+ syntax in this layer.
