<!--icm
{
  "id": "facade",
  "title": "Facade pattern (one simple entry point over a complex subsystem)",
  "doc_type": "pattern",
  "summary": "Provides a single high-level interface that orchestrates several subsystem classes, so clients call one method instead of wiring the subsystem themselves.",
  "keywords": ["facade", "subsystem wrapper", "HomeTheatreFacade", "Dimmer", "DvdPlayer", "Dvd", "simplified interface", "structural pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/FacadePattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Facade pattern

## Intent
Provide a unified, higher-level interface to a set of interfaces in a subsystem. The facade does not hide the subsystem classes; it offers a convenient front (`WatchMovie`, `Pause`, `Resume`) that sequences their calls, so clients do not have to know the steps.

## When to use
- A subsystem has many moving parts (dimmer, dvd player, dvd) and a common task requires coordinating several of them in order.
- You want to reduce coupling between clients and subsystem internals while still allowing direct access for advanced use.
- Not the right pattern when you need to convert one interface to another (that is Adapter) or add behaviour transparently (Decorator).

## Participants
- Facade (the simple front; holds and orchestrates subsystem objects) - `HomeTheatreFacade`
- Subsystem classes - `Dimmer` (`Dim(int)`, `Off()`), `DvdPlayer` (`On()`, `Insert(Dvd)`, `Play()`, `Pause()`, `Resume()`), `Dvd` (`Movie`)
- Client - `Program`

## Key code
```csharp
using System;

namespace FacadePattern
{
    public class Dimmer
    {
        internal void Dim(int val)
        {
            Console.WriteLine(val == 10 ? "Turning Lights On" : string.Format("Dimming lights to {0}", val));
        }

        internal void Off()
        {
            Console.WriteLine("Switching off lights");
        }
    }

    public class Dvd
    {
        public Dvd(string name)
        {
            Movie = name;
        }
        public string Movie { get; set; }
    }

    public class DvdPlayer
    {
        private Dvd _dvd;
        private int _time = 0;

        public void On()
        {
            Console.WriteLine("DVD Player powered on");
        }

        public void Insert(Dvd dvd)
        {
            _dvd = dvd;
            Console.WriteLine(string.Format("Inserting {0}", dvd.Movie));
        }

        public void Play()
        {
            Console.WriteLine(string.Format("Playing {0}", _dvd.Movie));
        }

        public void Pause()
        {
            Console.WriteLine(string.Format("Pausing at {0}", _time = (new Random()).Next(_time, _time + 120)));
        }

        public void Resume()
        {
            Console.WriteLine(string.Format("Resuming from {0}", _time));
        }
    }

    // Facade: each method drives several subsystem objects in the right order.
    public class HomeTheatreFacade
    {
        private Dimmer _dimmer;
        private Dvd _dvd;
        private DvdPlayer _dvdPlayer;

        public HomeTheatreFacade(Dimmer dimmer, Dvd dvd, DvdPlayer dvdPlayer)
        {
            _dvd = dvd;
            _dimmer = dimmer;
            _dvdPlayer = dvdPlayer;
        }

        public void WatchMovie()
        {
            _dimmer.Dim(5);
            _dvdPlayer.On();
            _dvdPlayer.Insert(_dvd);
            _dvdPlayer.Play();
        }

        public void Pause()
        {
            _dimmer.Dim(10);
            _dvdPlayer.Pause();
        }

        public void Resume()
        {
            _dimmer.Dim(5);
            _dvdPlayer.Resume();
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace FacadePattern
{
    internal static class Program
    {
        private static void Main()
        {
            var dimmer = new Dimmer();
            var dvdPlayer = new DvdPlayer();
            var dvd = new Dvd("Gone with the Wind 2 : Electric Bugaloo");
            var homeTheater = new HomeTheatreFacade(dimmer, dvd, dvdPlayer);

            homeTheater.WatchMovie();   // one call instead of dim + on + insert + play
            Console.WriteLine();
            homeTheater.Pause();
            Console.WriteLine();
            homeTheater.Resume();
            Console.WriteLine();
            homeTheater.Pause();
        }
    }
}
```

## C# 5 notes
- `Dimmer.Dim` and `Dimmer.Off`, and `DvdPlayer.On/Play/Pause/Resume`, use expression-bodied members (`internal void Off() => ...`), a C# 6 feature. Rewrite each as a normal method body with `Console.WriteLine(...)` inside braces (shown above).
- The console output uses string interpolation `$"..."` (C# 6). Replace with `string.Format(...)` as shown.
- Auto-property `Movie { get; set; }` and the conditional/ternary in `Dim` are valid C# 5.
- Add `using System;` explicitly where `Console`/`Random` are used (source relied on implicit usings).
