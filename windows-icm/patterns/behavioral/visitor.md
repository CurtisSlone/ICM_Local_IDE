<!--icm
{
  "id": "visitor",
  "title": "Visitor pattern (add operations to an object structure without changing it)",
  "doc_type": "pattern",
  "summary": "Represents an operation to perform on the elements of an object structure, letting you add new operations without modifying the element classes via double dispatch.",
  "keywords": ["visitor", "IUnitVisitor", "Unit", "Apartment", "double dispatch", "Accept", "object structure", "composite traversal", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/VisitorPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Visitor pattern

## Intent
Represent an operation to be performed on the elements of an object structure. Visitor lets you define a new operation without changing the classes of the elements on which it operates, using double dispatch: the element's `Accept` calls back the visitor's type-specific `Visit` method.

## When to use
- An object structure contains many element types and you want to run operations across them.
- You want to add new operations often without editing every element class.
- Related behavior should be gathered in one visitor rather than spread across the elements.
- NOT when the set of element classes changes frequently - every new element forces a new method on every visitor.

## Participants
- Visitor interface - `IUnitVisitor` (one `Visit*` method per element type)
- Concrete visitors - `ApartmentVisitor`, `StudioVisitor`, `BedroomVisitor`, `LivingRoomVisitor`
- Element base (also the composite) - `Unit` (holds child `Unit[]`, has `Accept`)
- Concrete elements - `Apartment`, `Studio`, `Bedroom`, `LivingRoom`
- Client - `Program`

## Key code
```csharp
namespace VisitorPattern
{
    public interface IUnitVisitor
    {
        void VisitApartment(Apartment apartment);
        void VisitStudio(Studio studio);
        void VisitBedroom(Bedroom bedroom);
        void VisitLivingRoom(LivingRoom livingRoom);
    }
}

namespace VisitorPattern
{
    public abstract class Unit
    {
        private Unit[] _units;

        public Unit(params Unit[] units) { _units = units; }

        // base Accept walks children, so a visitor visits the whole composite
        public virtual void Accept(IUnitVisitor visitor)
        {
            foreach (var unit in _units) unit.Accept(visitor);
        }

        public abstract string ToString();
    }
}

namespace VisitorPattern
{
    public class Apartment : Unit
    {
        public Apartment(params Unit[] units) : base(units) { }

        public override void Accept(IUnitVisitor visitor)
        {
            visitor.VisitApartment(this);  // double dispatch: element picks the Visit* method
            base.Accept(visitor);          // then recurse into children
        }

        public override string ToString() { return "Apartment"; }
    }
}
// Studio, Bedroom, LivingRoom are identical except they call VisitStudio / VisitBedroom /
// VisitLivingRoom and return their own ToString text.

using System;
namespace VisitorPattern
{
    // A visitor implements every Visit* method but acts only on the type it cares about.
    public class LivingRoomVisitor : IUnitVisitor
    {
        public void VisitApartment(Apartment apartment) { }
        public void VisitStudio(Studio studio) { }
        public void VisitBedroom(Bedroom bedroom) { }
        public void VisitLivingRoom(LivingRoom livingRoom) { Console.WriteLine("This is the living room"); }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace VisitorPattern
{
    class Program
    {
        static void Main()
        {
            // build the object structure (Apartment composed of rooms)
            var apartment = new Apartment(new LivingRoom(), new Bedroom(), new Bedroom());
            var studio = new Studio(new LivingRoom(), new Bedroom());

            Console.WriteLine("Visiting an Apartment");
            apartment.Accept(new ApartmentVisitor());   // prints only for the apartment node
            apartment.Accept(new LivingRoomVisitor());  // prints once for the living room child
            apartment.Accept(new BedroomVisitor());     // prints once per bedroom child

            Console.WriteLine("Visiting a Studio");
            studio.Accept(new StudioVisitor());
            studio.Accept(new LivingRoomVisitor());
            studio.Accept(new BedroomVisitor());
        }
    }
}
```

## C# 5 notes
- This example is C# 5 compatible apart from implicit usings - the element files use no `using`, and the visitor and client files add `using System;` explicitly. No string interpolation, `?.`, expression-bodied members, or records appear.
- `params Unit[]` constructors, `virtual`/`override`, and the double-dispatch `Accept` -> `Visit*` calls are all valid C# 5.
- Quirk to keep verbatim: `Unit` declares `public abstract string ToString();` (abstract rather than `override`), and the concrete units use `public override string ToString()`. This compiles as the source intends; do not rename or "fix" it when porting.
