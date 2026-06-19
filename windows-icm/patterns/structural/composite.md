<!--icm
{
  "id": "composite",
  "title": "Composite pattern (treat individual objects and groups of objects uniformly)",
  "doc_type": "pattern",
  "summary": "Builds a tree of objects where leaves and containers share one type, so client code can operate on a single item or a whole nested structure with the same call.",
  "keywords": ["composite", "tree structure", "part-whole hierarchy", "MenuComponent", "Menu", "MenuItem", "leaf and composite", "structural pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/CompositePattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Composite pattern

## Intent
Compose objects into tree structures to represent part-whole hierarchies. Composite lets clients treat individual objects (leaves) and compositions of objects (containers) uniformly through one shared type.

## When to use
- You have a recursive, nested structure (menus containing menus and items, files in folders, UI element trees).
- You want client code to ignore the difference between a single element and a group.
- Trade-off: the shared base exposes child-management operations (Add/Remove/GetChild) that are meaningless on leaves; this example throws `NotImplementedException` on the base so leaves inherit a safe default.

## Participants
- Component (shared abstract base; declares common ops and default child-management) - `MenuComponent`
- Leaf (no children) - `MenuItem`
- Composite (holds child `MenuComponent`s, overrides Add/Remove/GetChild and recurses in Print) - `Menu`
- Client (holds a `MenuComponent`, calls one op) - `Client` / `Program`

## Key code
```csharp
using System;
using System.Collections.Generic;

namespace CompositePattern
{
    // Component: one type for both leaves and composites. Child-management has
    // safe defaults so leaves do not need to implement them.
    public class MenuComponent
    {
        public virtual void Add(MenuComponent component)
        {
            throw new NotImplementedException();
        }

        public virtual void Remove(MenuComponent component)
        {
            throw new NotImplementedException();
        }

        public virtual MenuComponent GetChild(int i)
        {
            throw new NotImplementedException();
        }

        public virtual string Name { get; }
        public virtual string Description { get; }
        public virtual bool Vegetarian { get; }
        public virtual double Price { get; }

        public virtual void Print()
        {
            throw new NotImplementedException();
        }
    }

    // Leaf
    public class MenuItem : MenuComponent
    {
        public MenuItem(string name, string description, double price, bool isveg)
        {
            Name = name;
            Description = description;
            Price = price;
            Vegetarian = isveg;
        }

        public override string Name { get; }
        public override string Description { get; }
        public override double Price { get; }
        public override bool Vegetarian { get; }

        public override void Print()
        {
            Console.WriteLine(string.Format("{0} : {1}  {2} \n {3}",
                Name, Price, Vegetarian ? '+' : '*', Description));
        }
    }

    // Composite: holds children and recurses.
    public class Menu : MenuComponent
    {
        List<MenuComponent> _components = new List<MenuComponent>();

        public Menu(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public override void Add(MenuComponent component)
        {
            _components.Add(component);
        }

        public override void Remove(MenuComponent component)
        {
            _components.Remove(component);
        }

        public override MenuComponent GetChild(int i)
        {
            return _components[i];
        }

        public override string Name { get; }
        public override string Description { get; }

        public override void Print()
        {
            Console.WriteLine(Name);
            Console.WriteLine("___________");
            foreach (var menuComponent in _components)
            {
                menuComponent.Print();   // recurses into leaves and sub-menus alike
            }
            Console.WriteLine();
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
namespace CompositePattern
{
    static class Program
    {
        public static void Main()
        {
            var breakfast = new Menu("Breakfast", "Pancake House");
            var lunch = new Menu("Lunch", "Deli Diner");
            var dinner = new Menu("Dinner", "Dinneroni");
            var dessert = new Menu("Dessert", "Ice Cream");
            var menu = new Menu("All", "McDonalds");

            breakfast.Add(new MenuItem("Waffles", "Butterscotch waffles", 140, false));
            breakfast.Add(new MenuItem("Corn Flakes", "Kellogs", 80, true));

            lunch.Add(new MenuItem("Burger", "Cheese and Onion Burger", 250, true));
            lunch.Add(new MenuItem("Sandwich", "Chicken Sandwich", 280, false));

            dinner.Add(new MenuItem("Pizza", "Cheese and Tomato Pizza", 210, true));
            dinner.Add(new MenuItem("Pasta", "Chicken Pasta", 280, false));

            dessert.Add(new MenuItem("Ice Cream", "Vanilla and Chocolate", 120, true));
            dessert.Add(new MenuItem("Cake", "Choclate Cake Slice", 180, false));

            dinner.Add(dessert);   // a composite nested inside another composite
            menu.Add(breakfast);
            menu.Add(lunch);
            menu.Add(dinner);

            menu.Print();          // one call walks the whole tree
        }
    }
}
```

## C# 5 notes
- Getter-only auto-properties assigned from the constructor (`public override string Name { get; }`) are a C# 6 feature. In C# 5 this does not compile. Use a private backing field with a public read-only property:
  ```csharp
  private readonly string _name;
  public override string Name { get { return _name; } }
  // assign _name = name; in the constructor
  ```
  Apply the same change to `Description`, `Price`, and `Vegetarian` in both `Menu` and `MenuItem`. The base `MenuComponent` getter-only properties have no setter and no body, which also needs a backing-field form under C# 5.
- `MenuItem.Print` uses string interpolation `$"{Name} : {Price} ..."`, a C# 6 feature. Replace with `string.Format(...)` as shown above.
- Add `using System;` and `using System.Collections.Generic;` explicitly (source relied on implicit usings).
