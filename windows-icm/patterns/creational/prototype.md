<!--icm
{
  "id": "prototype",
  "title": "Prototype pattern (create new objects by cloning an existing one)",
  "doc_type": "pattern",
  "summary": "Creates new objects by copying an existing instance through a clone operation, so the client produces objects without depending on their concrete classes.",
  "keywords": ["prototype", "clone", "ICloneable", "IFigure", "Circle", "Rectangle", "copy constructor", "creational pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/PrototypePattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Prototype pattern

## Intent
Produce new objects by cloning a prototype instance rather than calling a constructor directly. Each concrete type knows how to copy itself via `Clone()`, so the client can duplicate any figure through the common `IFigure` interface without knowing its concrete class.

## When to use
- Object creation is expensive or the concrete type should be hidden, and you have a configured instance to copy.
- You want to add or remove product types at runtime by registering prototypes.
- Not when objects are cheap to construct and few in kind - a constructor or factory is simpler. Watch for shallow vs deep copy: this example clones value fields, which is a correct deep copy for these types.

## Participants
- Prototype interface - `IFigure` (extends `System.ICloneable`, adds `void GetInfo()`)
- Concrete prototypes - `Circle`, `Rectangle` (each implements `object Clone()` by `new`-ing itself from its own fields)
- Client - `Program.Main`, which clones through the `IFigure` reference

## Key code
```csharp
using System;

namespace PrototypePattern
{
    interface IFigure : ICloneable   // ICloneable supplies object Clone();
    {
        void GetInfo();
    }

    class Circle : IFigure
    {
        readonly int _radius;
        public Circle(int r) { _radius = r; }

        public object Clone()
        {
            return new Circle(_radius);   // copy this instance's state into a new one
        }

        public void GetInfo()
        {
            Console.WriteLine("Circle with radius " + _radius);
        }
    }

    class Rectangle : IFigure
    {
        readonly int _width;
        readonly int _height;
        public Rectangle(int w, int h) { _width = w; _height = h; }

        public object Clone()
        {
            return new Rectangle(_width, _height);
        }

        public void GetInfo()
        {
            Console.WriteLine("Rectangle height " + _height + " and width " + _width);
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace PrototypePattern
{
    class Program
    {
        static void Main(string[] args)
        {
            IFigure figure = new Rectangle(30, 40);
            IFigure clonedFigure = (IFigure)figure.Clone();   // Clone() returns object, cast back
            figure.GetInfo();
            clonedFigure.GetInfo();

            figure = new Circle(30);
            clonedFigure = (IFigure)figure.Clone();
            figure.GetInfo();
            clonedFigure.GetInfo();

            Console.Read();
        }
    }
}
```

## C# 5 notes
- The source relies on implicit usings; add `using System;` (for `ICloneable` and `Console`).
- The source uses string interpolation in `GetInfo()` (`$"Circle with radius {_radius}"`). C# 5 has no interpolation - use concatenation as shown, or `string.Format("Circle with radius {0}", _radius)`.
- `ICloneable`, `readonly` fields, interface inheritance, and the cast on `Clone()` are all C# 5 compatible. No other C# 6+ syntax is present.
