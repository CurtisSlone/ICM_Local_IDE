<!--icm
{
  "id": "iterator",
  "title": "Iterator pattern (sequential access without exposing the collection)",
  "doc_type": "pattern",
  "summary": "Provides a uniform way to traverse the elements of an aggregate without exposing how it stores them, so a client can walk an ArrayList and an array through the same interface.",
  "keywords": ["iterator", "IEnumerable", "IEnumerator", "GetEnumerator", "MoveNext", "Current", "aggregate", "traversal", "foreach", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/IteratorPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Iterator pattern

## Intent
Provide a way to access the elements of an aggregate object sequentially without exposing its underlying representation. In .NET this is the `IEnumerable`/`IEnumerator` contract that `foreach` consumes, so collections backed by an `ArrayList` and by a plain array can be traversed identically.

## When to use
- You want to traverse a collection without revealing whether it is backed by an array, list, tree, etc.
- You want several independent traversals of the same aggregate.
- You want clients to use `foreach` uniformly across different aggregate types.
- NOT needed when the built-in collection already implements `IEnumerable<T>` and you have no custom traversal logic - just iterate it directly.

## Participants
- Element / item - `Menu`
- Concrete aggregates - `BreakfastMenu` (backed by `ArrayList`), `DinnerMenu` (backed by `Menu[]`), each exposing an `Items` of type `IEnumerable`
- Iterable wrappers (return the enumerator) - `BreakfastMenuIterator`, `DinnerMenuIterator` (implement `IEnumerable`)
- Concrete iterators (the cursor) - `BreakfastMenuEnum`, `DinnerMenuEnum` (implement `IEnumerator`)
- Client - `Client` (the "waiter"), driven by `Program`

## Key code
```csharp
namespace IteratorPattern
{
    public class Menu
    {
        public string Name { get; }
        public string Description { get; }
        public bool Vegetarian { get; }
        public double Price { get; }
        public Menu(string name, string description, double price, bool vegetarian)
        {
            Name = name; Description = description; Price = price; Vegetarian = vegetarian;
        }
    }
}

using System.Collections;
namespace IteratorPattern
{
    // Aggregate hides its storage; exposes only IEnumerable Items.
    public class BreakfastMenu
    {
        private ArrayList _items;
        public IEnumerable Items { get { return new BreakfastMenuIterator(_items); } }
        public BreakfastMenu() { _items = new ArrayList(); /* AddItem(...) calls */ }
    }
}

using System.Collections;
namespace IteratorPattern
{
    class BreakfastMenuIterator : IEnumerable
    {
        private ArrayList _items;
        public BreakfastMenuIterator(ArrayList items) { _items = items; }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public IEnumerator GetEnumerator() { return new BreakfastMenuEnum(_items); }
    }
}

using System;
using System.Collections;
namespace IteratorPattern
{
    public class BreakfastMenuEnum : IEnumerator
    {
        private readonly ArrayList _items;
        private int _position = -1;
        public BreakfastMenuEnum(ArrayList items) { _items = items; }
        public void Dispose() { throw new System.NotImplementedException(); }
        public bool MoveNext() { _position++; return (_position < _items.Count); }
        public void Reset() { _position = -1; }
        object IEnumerator.Current { get { return Current; } }
        public Menu Current
        {
            get
            {
                try { return (Menu)_items[_position]; }
                catch (IndexOutOfRangeException) { throw new InvalidOperationException(); }
            }
        }
    }
}

// DinnerMenu / DinnerMenuIterator / DinnerMenuEnum are the same shape but backed by Menu[]
// instead of ArrayList (uses _items.Length and indexes the array directly).
```

## How it wires (from Program.cs and Client.cs)
```csharp
using System;
using System.Collections;

namespace IteratorPattern
{
    public class Client
    {
        private IEnumerable _breakfast;
        private IEnumerable _dinner;

        public Client(BreakfastMenu breakfast, DinnerMenu dinner)
        {
            this._breakfast = breakfast.Items;   // works the same for either menu type
            this._dinner = dinner.Items;
        }

        public void PrintMenu() { PrintMenu(_breakfast); PrintMenu(_dinner); }

        private void PrintMenu(IEnumerable iter)
        {
            foreach (var item in iter)           // foreach consumes the custom IEnumerator
            {
                var i = (Menu)item;
                Console.WriteLine(string.Format("{0}  Rs. {1} {2} \n {3} ",
                    i.Name, i.Price, (i.Vegetarian ? "*" : "x"), i.Description));
            }
        }
    }

    static class Program
    {
        private static void Main()
        {
            var breakfast = new BreakfastMenu();
            var dinner = new DinnerMenu();
            var waiter = new Client(breakfast, dinner);
            waiter.PrintMenu();
        }
    }
}
```

## C# 5 notes
- This example uses the non-generic `IEnumerable` / `IEnumerator` (System.Collections), not `IEnumerable<T>`, and hand-rolls the iterator rather than using a `yield return` generator. Both styles are valid in C# 5; a `yield`-based `GetEnumerator` would be the idiomatic shortcut and removes the need for the separate `*Enum` cursor classes.
- The source uses string interpolation in `Client.PrintMenu` (`$"{i.Name} ..."`, C# 6+) and the expression-bodied member `object IEnumerator.Current => Current;` (C# 6+). In C# 5 use `string.Format(...)` and a full property getter `object IEnumerator.Current { get { return Current; } }` as shown above.
- Getter-only auto-properties on `Menu` (`public string Name { get; }`) are C# 6+. In C# 5 use `{ get; private set; }` and set them in the constructor.
