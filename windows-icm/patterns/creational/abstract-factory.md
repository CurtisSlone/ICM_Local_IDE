<!--icm
{
  "id": "abstract-factory",
  "title": "Abstract Factory pattern (create families of related objects)",
  "doc_type": "pattern",
  "summary": "Provides an interface for creating a whole family of related products without naming their concrete classes, so swapping the factory swaps the entire family.",
  "keywords": ["abstract factory", "IIngredientsFactory", "NyIngredientsFactory", "ChicagoIngredientsFactory", "IDough", "ICheese", "ISauce", "IVeggies", "IClam", "product family", "creational pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/FactoryPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Abstract Factory pattern

## Intent
Define one factory interface that creates a whole family of related products (dough, cheese, sauce, veggies, clam). Each concrete factory produces a regionally consistent set of ingredients. Client code (the `Pizza` subclasses) depends only on the abstract product interfaces, so choosing a different factory swaps every ingredient at once with no client changes.

## When to use
- A system must be configured with one of several families of products, and you must guarantee the products in a family are used together (no mixing NY dough with Chicago cheese).
- You want to isolate concrete product classes from client code.
- Not when you only have a single product to create - that is plain Factory Method or a simple factory.

## Participants
- Abstract factory - `IIngredientsFactory` (declares `CreateDough`, `CreateVeggies`, `CreateSauce`, `CreateCheese`, `CreateClam`)
- Concrete factories - `NyIngredientsFactory`, `ChicagoIngredientsFactory`
- Abstract products - `IDough`, `ICheese`, `ISauce`, `IVeggies`, `IClam` (each exposes `string Name { get; }`)
- Concrete products - `ThinCrust`/`DeepDish` (dough), `Mozarella`/`Parmesan` (cheese), `CherryTomato`/`PlumTomato` (sauce), `Onion`/`Pepper`/`Olive`/`Cucumber` (veggies), `FrozenClam`/`FreshClam` (clam)
- Client - the `Pizza` subclasses, which hold an `IIngredientsFactory` and call its create methods in `Prepare()`

## Key code
```csharp
using System.Collections.Generic;

namespace FactoryPattern
{
    // Abstract product interfaces - all share the same shape:
    public interface IDough   { string Name { get; } }
    public interface ICheese  { string Name { get; } }
    public interface ISauce   { string Name { get; } }
    public interface IVeggies { string Name { get; } }
    public interface IClam    { string Name { get; } }

    // Concrete product example (C# 6 expression-bodied; see notes for C# 5 form):
    internal class ThinCrust  : IDough  { public string Name => "Thin Crust"; }
    internal class Mozarella  : ICheese { public string Name => "Mozarella"; }

    // The abstract factory: creates one whole family.
    interface IIngredientsFactory
    {
        IDough CreateDough();
        IEnumerable<IVeggies> CreateVeggies();
        ISauce CreateSauce();
        ICheese CreateCheese();
        IClam CreateClam();
    }

    // Concrete factory - note explicit interface implementation.
    internal class NyIngredientsFactory : IIngredientsFactory
    {
        ICheese IIngredientsFactory.CreateCheese() { return new Mozarella(); }
        IClam   IIngredientsFactory.CreateClam()   { return new FrozenClam(); }
        IDough  IIngredientsFactory.CreateDough()  { return new ThinCrust(); }
        ISauce  IIngredientsFactory.CreateSauce()  { return new CherryTomato(); }

        IEnumerable<IVeggies> IIngredientsFactory.CreateVeggies()
        {
            IVeggies[] arr = { new Onion(), new Pepper(), new Olive() };
            return arr;
        }
    }

    // ChicagoIngredientsFactory mirrors this, returning Parmesan, FreshClam,
    // DeepDish, PlumTomato, and { Onion, Cucumber, Pepper }.
}
```

## How it wires (from Program.cs)
```csharp
// The abstract factory is selected inside each Factory Method creator, not in Main.
// NyPizzaFactory.Create() does:
//     IIngredientsFactory ingredients = new NyIngredientsFactory();
//     pizza = new CheesePizza(ingredients);   // pizza depends only on the interface
//
// Main only triggers an order; the family choice rides along with the factory:
using System;

namespace FactoryPattern
{
    static class Program
    {
        static void Main()
        {
            var yankees = new NyPizzaFactory();
            yankees.Order(PizzaType.Cheese);   // CheesePizza.Prepare() calls
                                               // ingredients.CreateDough().Name, etc.

            var cubs = new ChicagoPizzaFactory();
            cubs.Order(PizzaType.Clam);
        }
    }
}
```

## C# 5 notes
- Add `using System.Collections.Generic;` for `IEnumerable<IVeggies>`. The source relies on implicit usings.
- The concrete product classes use C# 6 expression-bodied properties: `public string Name => "Thin Crust";`. In C# 5 write a property with a getter body:
  ```csharp
  public string Name { get { return "Thin Crust"; } }
  ```
  Apply the same change to `Mozarella`, `Parmesan`, `DeepDish`, `CherryTomato`, `PlumTomato`, `Onion`, `Pepper`, `Olive`, `Cucumber`, `FrozenClam`, `FreshClam`.
- Explicit interface implementation (`ICheese IIngredientsFactory.CreateCheese()`), collection initializers (`IVeggies[] arr = { ... }`), and `var` are all C# 5 compatible.
- No interpolation, records, or target-typed `new()` in this layer.
