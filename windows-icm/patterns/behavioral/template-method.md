<!--icm
{
  "id": "template-method",
  "title": "Template Method pattern (fix an algorithm skeleton, defer steps to subclasses)",
  "doc_type": "pattern",
  "summary": "Defines the skeleton of an algorithm in a base method while letting subclasses override specific steps without changing the algorithm's structure.",
  "keywords": ["template method", "Beverage", "Coffee", "Tea", "hook method", "abstract method", "algorithm skeleton", "IComparable", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/TemplatePattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Template Method pattern

## Intent
Define the skeleton of an algorithm in a method, deferring some steps to subclasses. Subclasses redefine certain steps of the algorithm without changing its overall structure. Here `Beverage.Prepare()` fixes the brewing sequence and subclasses fill in the variable steps.

## When to use
- Several classes share the same overall algorithm but differ in individual steps.
- You want to put the invariant parts of an algorithm in one place and let subclasses customize the rest.
- You want optional "hook" steps subclasses may or may not override (here the condiments step, gated by a flag).
- NOT when the whole algorithm varies - Strategy (full delegation) fits better than overriding fragments.

## Participants
- Abstract class with the template method - `Beverage` (`Prepare()` is the template; `Boil`/`Pour` fixed, `Brew`/`AddCondiments` abstract)
- Concrete classes - `Coffee`, `Tea`
- A second, smaller example - `Person : IComparable` (overriding the `CompareTo` step that `List.Sort` calls as its template)
- Client - `Program`

## Key code
```csharp
using System;

namespace TemplatePattern
{
    public abstract class Beverage
    {
        protected int _sugar;

        // Template method: fixed sequence, with an optional (hook) step.
        public void Prepare()
        {
            Boil();
            Brew();
            Pour();
            if (WantsCondiments)
                AddCondiments();
        }

        public bool WantsCondiments { private get; set; }  // controls the optional step

        protected abstract void Brew();                    // subclass must supply
        protected abstract void AddCondiments();           // subclass must supply

        private void Boil() { Console.WriteLine("Boling Water"); }  // fixed steps
        private void Pour() { Console.WriteLine("Pouring in Cup"); }

        public int AddSugar { get; set; }
        protected void Sugar() { }
    }
}

using System;
namespace TemplatePattern
{
    class Coffee : Beverage
    {
        protected override void Brew()          { Console.WriteLine("Add Coffee Grounds to water and boil"); }
        protected override void AddCondiments() { Console.WriteLine("Add Milk and Sugar"); }
    }
}

using System;
namespace TemplatePattern
{
    class Tea : Beverage
    {
        protected override void Brew()          { Console.WriteLine("Adding tea leaves to water and boil"); }
        protected override void AddCondiments()
        {
            Console.WriteLine("Adding Lemon and Sugar");
            Sugar();
        }

        // Tea hides the base members with 'new' to track sugar spoons.
        private new void Sugar() { Console.WriteLine(string.Format("adding {0} spoons of sugar", _sugar)); }
        public new int AddSugar { set { _sugar = value; } }
    }
}

// Second example: List<T>.Sort() is the algorithm; CompareTo is the deferred step.
using System;
namespace TemplatePattern.Comparable
{
    class Person : IComparable
    {
        public string Name { get; }
        public int Age { get; }
        public Person(string name, int age) { Name = name; Age = age; }

        public int CompareTo(object obj)
        {
            var other = (Person)obj;
            if (String.Compare(Name, other.Name, StringComparison.Ordinal) == 0)
                return Age.CompareTo(other.Age);
            return String.Compare(Name, other.Name, StringComparison.Ordinal);
        }

        public override string ToString() { return string.Format("{0} : {1} < ", Name, Age); }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;
using System.Collections.Generic;
using TemplatePattern.Comparable;

namespace TemplatePattern
{
    internal static class Program
    {
        static void Main()
        {
            var tea = new Tea();
            var coffee = new Coffee();

            tea.WantsCondiments = true;     // turns on the optional condiments step
            tea.AddSugar = 5;
            tea.Prepare();                  // Boil, Brew(tea), Pour, AddCondiments(tea)

            Console.WriteLine();
            coffee.WantsCondiments = true;
            coffee.Prepare();               // same skeleton, coffee's steps

            var people = new List<Person>
            {
                new Person("Ram", 25), new Person("Abishek", 12),
                new Person("Ram", 18), new Person("Abishek", 18)
            };
            people.Sort();                  // Sort is the template; Person.CompareTo is the step
        }
    }
}
```

## C# 5 notes
- `Tea.Sugar()` uses string interpolation (`$"adding {_sugar} spoons of sugar"`) and `Person.ToString()` uses `$"{Name} : {Age} < "` (both C# 6+); use `string.Format(...)` in C# 5 as shown.
- `Person` uses getter-only auto-properties (`public string Name { get; }`, C# 6+); in C# 5 use `{ get; private set; }` and set them in the constructor.
- `Beverage.Prepare()` as a non-virtual public method calling abstract steps is the Template Method itself; that, the `new`-hiding members, and `IComparable.CompareTo` are all valid C# 5.
- `Person` implements the non-generic `IComparable` (`CompareTo(object)`); the generic `IComparable<T>` would avoid the cast but is not required for `List<T>.Sort()`.
