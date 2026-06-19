<!--icm
{
  "id": "singleton",
  "title": "Singleton pattern (one shared instance, lazily and safely created)",
  "doc_type": "pattern",
  "summary": "Ensures a class has exactly one instance and gives a global access point to it, using Lazy<T> for thread-safe lazy initialization.",
  "keywords": ["singleton", "ChocolateBoiler", "Lazy<T>", "GetInstance", "thread-safe", "private constructor", "creational pattern", "global access point"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/SingletonPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Singleton pattern

## Intent
Guarantee that a class has only one instance and provide a single global access point to it. Here `ChocolateBoiler` holds boiler state that must not be duplicated; every caller gets the same object via `GetInstance()`. Construction is private so no one else can `new` it.

## When to use
- Exactly one instance must coordinate shared state or a shared resource (a boiler, a connection pool, a config).
- You need a well-known global access point but want lazy, thread-safe creation.
- Not as a substitute for passing dependencies - singletons are global state and complicate testing and concurrency. Prefer injection where practical.

## Participants
- Singleton - `ChocolateBoiler` (private constructor, static accessor, static cached instance)
- Lazy holder - `System.Lazy<ChocolateBoiler>` (provides thread-safe, deferred construction)
- Nested state enum - `Status` (`Empty`, `InProgress`, `Boiled`), declared in a second `partial` file

## Key code
```csharp
using System;

namespace SingletonPattern
{
    internal partial class ChocolateBoiler
    {
        // Lazy<T> is thread-safe by default (ExecutionAndPublication):
        // the factory runs at most once even under concurrent first access.
        private static readonly Lazy<ChocolateBoiler> _singleton =
            new Lazy<ChocolateBoiler>(() => new ChocolateBoiler());

        public static ChocolateBoiler GetInstance() => _singleton.Value;

        private Status _boiler;

        private ChocolateBoiler()   // private: only GetInstance can create it
        {
            Console.WriteLine("Starting");
            _boiler = Status.Empty;
        }

        public void Fill()
        {
            if (!IsEmpty) return;
            Console.WriteLine("Filling...");
            _boiler = Status.InProgress;
        }

        public void Drain()
        {
            if (!IsBoiled) return;
            Console.WriteLine("Draining...");
            _boiler = Status.Empty;
        }

        public void Boil()
        {
            if (IsBoiled || IsEmpty) return;
            Console.WriteLine("Boiling...");
            _boiler = Status.Boiled;
        }

        private bool IsEmpty  => (_boiler == Status.Empty);
        private bool IsBoiled => (_boiler == Status.Boiled);
    }

    // Status.cs - second partial declaration of the same class:
    internal partial class ChocolateBoiler
    {
        private enum Status { Empty, InProgress, Boiled }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace SingletonPattern
{
    static class Program
    {
        static void Main()
        {
            try
            {
                var chocoEggs = ChocolateBoiler.GetInstance();
                chocoEggs.Fill();
                chocoEggs.Boil();
                chocoEggs.Drain();
            }
            catch (Exception)
            {
                Console.Write("Oops");
            }
        }
    }
}
```

## Thread-safety angle (from SingletonPattern.Tests)
The NUnit test asserts the single-instance contract: two `GetInstance()` calls return the same reference.
```csharp
namespace SingletonPattern.Tests
{
    [TestFixture]
    public class SingletonPatternTests
    {
        [Test]
        public void GetInstance_CreateInstanceTwice_AreEqual()
        {
            var firstAttemptInstance  = ChocolateBoiler.GetInstance();
            var secondAttemptInstance = ChocolateBoiler.GetInstance();
            Assert.That(firstAttemptInstance, Is.EqualTo(secondAttemptInstance));
        }
    }
}
```
The thread-safety comes from `Lazy<T>`: its default mode (`LazyThreadSafetyMode.ExecutionAndPublication`) ensures the value factory runs once and the result is published atomically, so concurrent first callers all observe the same instance without explicit locking or double-checked locking.

## C# 5 notes
- `Lazy<T>` exists since .NET 4, so the thread-safe holder works under the C# 5 toolchain. The accessor and the two computed properties use C# 6 expression-bodied member syntax:
  ```csharp
  public static ChocolateBoiler GetInstance() => _singleton.Value;
  private bool IsEmpty => (_boiler == Status.Empty);
  ```
  In C# 5 write full bodies:
  ```csharp
  public static ChocolateBoiler GetInstance() { return _singleton.Value; }
  private bool IsEmpty  { get { return _boiler == Status.Empty; } }
  private bool IsBoiled { get { return _boiler == Status.Boiled; } }
  ```
- `partial class`, nested `enum`, `private` constructor, and `static readonly` fields are all C# 5 compatible.
- The test relies on implicit usings for the NUnit namespace; add `using NUnit.Framework;` if porting (separate test project, not part of the C# 5 in-box build).
