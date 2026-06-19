<!--icm
{
  "id": "builder",
  "title": "Builder pattern (step-by-step construction of a complex object)",
  "doc_type": "pattern",
  "summary": "Separates the construction of a complex object from its representation so the same build steps can produce different objects, driven by an optional director.",
  "keywords": ["builder", "director", "IBuilder", "Cook", "Hamburger", "MyHamburgerBuilder", "WifesHamburgerBuilder", "creational pattern", "step-by-step construction"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/BuilderPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Builder pattern

## Intent
Build a complex product through a fixed sequence of small steps, where each concrete builder fills in the steps differently. A director (here `Cook`) owns the sequence so the client does not have to know the order. The same director can produce different products by swapping builders.

## When to use
- The object needs many parts assembled in a set order and you want to reuse that order across variations.
- You want to produce several different representations (here, different hamburgers) from the same construction process.
- Not for simple objects - if a constructor or object initializer is enough, a builder is overkill.

## Participants
- Builder interface - `IBuilder` (declares `AddIngredients`, `AddShape`, `AddSize`, `Reset`, `Build`)
- Concrete builders - `MyHamburgerBuilder`, `WifesHamburgerBuilder`
- Director - `Cook` (drives the steps in order, resets the builder on accept)
- Product - `Hamburger`

## Key code
```csharp
namespace BuilderPattern
{
    public interface IBuilder
    {
        void AddIngredients();
        void AddShape();
        void AddSize();
        void Reset();
        Hamburger Build();
    }

    public class Hamburger
    {
        public int Size { get; set; }
        public string Shape { get; set; }
        public string[] Ingredients { get; set; }

        public override string ToString()
        {
            var hamburger = "";
            foreach (var ingredient in Ingredients)
            {
                hamburger += ingredient + " ";
            }
            return "Ingredients: " + hamburger + ", Size: " + Size + ", Shape: " + Shape;
        }
    }

    public class MyHamburgerBuilder : IBuilder
    {
        private Hamburger _hamburger;

        public void AddIngredients()
        {
            _hamburger.Ingredients = new string[] { "Bread", "Meat", "Tomato", "Salad", "Mayonnaise" };
        }
        public void AddShape() { _hamburger.Shape = "Kite"; }
        public void AddSize() { _hamburger.Size = 10; }       // inches
        public void Reset() { _hamburger = new Hamburger(); }
        public Hamburger Build() { return _hamburger; }
    }

    // WifesHamburgerBuilder is identical in shape, with different ingredients/shape/size.

    // This class can also be called the Director
    public class Cook
    {
        private IBuilder _builder;

        public Cook(IBuilder builder) { AcceptBuilder(builder); }

        public void ChangeBuilder(IBuilder builder) { AcceptBuilder(builder); }

        public Hamburger Build()
        {
            _builder.AddIngredients();
            _builder.AddShape();
            _builder.AddSize();
            return _builder.Build();
        }

        private void AcceptBuilder(IBuilder builder)
        {
            _builder = builder;
            _builder.Reset();   // every accepted builder starts fresh
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace BuilderPattern
{
    class Program
    {
        static void Main()
        {
            var builder = new MyHamburgerBuilder();
            var cook = new Cook(builder);
            var myHamburger = cook.Build();

            cook.ChangeBuilder(new WifesHamburgerBuilder());
            var wifesHamburger = cook.Build();

            Console.WriteLine("My Hamburger: " + myHamburger);
            Console.WriteLine("My Wife's Hamburger: " + wifesHamburger);
        }
    }
}
```

## C# 5 notes
- The source relies on implicit usings; add `using System;` explicitly (needed for `Console`).
- The source uses string interpolation (`$"{ingredient} "`, `$"My Hamburger: {myHamburger}"`). C# 5 has no interpolation - use string concatenation as shown above, or `string.Format("My Hamburger: {0}", myHamburger)`.
- Everything else (auto-properties, `foreach`, `var`, `override ToString`) is C# 5 compatible.
