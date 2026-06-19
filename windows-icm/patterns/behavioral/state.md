<!--icm
{
  "id": "state",
  "title": "State pattern (object changes behavior when its internal state changes)",
  "doc_type": "pattern",
  "summary": "Lets an object alter its behavior when its internal state changes by delegating each operation to a separate state object, replacing sprawling conditional logic.",
  "keywords": ["state", "IState", "GumballMachine", "state machine", "state transition", "NoQuarterState", "HasQuarterState", "SoldState", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/StatePattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# State pattern

## Intent
Allow an object to alter its behavior when its internal state changes; the object appears to change its class. Each state is its own object implementing a common interface, and the context delegates calls to the current state, which decides what happens and which state comes next.

## When to use
- An object's behavior depends on its state and must change at runtime.
- Operations have large multi-branch conditionals based on a state field.
- State transition rules should be localized rather than scattered.
- NOT for a couple of trivial flags where a simple `if` is clearer.

## Participants
- State interface - `IState` (`InsertQuarter`, `EjectQuarter`, `TurnCrank`, `Dispense`)
- Context - `GumballMachine` (holds all state instances and the current `State`, delegates to it)
- Concrete states - `NoQuarterState`, `HasQuarterState`, `SoldState`, `SoldOutState`, `WinnerState`
- Client - `Program`

(The repo also has a `Legacy/` subfolder showing the pre-refactor giant-switch version; it is the same gumball machine without the State pattern and is not part of the distilled pattern.)

## Key code
```csharp
namespace StatePattern
{
    public interface IState
    {
        void InsertQuarter();
        void EjectQuarter();
        void TurnCrank();
        void Dispense();
    }
}

using System;
namespace StatePattern
{
    public class GumballMachine
    {
        public int Count { get; private set; }

        public IState SoldOutState;
        public IState NoQuarterState;
        public IState HasQuarterState;
        public IState SoldState;
        public IState WinnerState;

        public IState State { get; set; }     // current state

        public GumballMachine(int count)
        {
            Count = count;
            SoldOutState = new SoldOutState(this);
            NoQuarterState = new NoQuarterState(this);
            HasQuarterState = new HasQuarterState(this);
            SoldState = new SoldState(this);
            WinnerState = new WinnerState(this);
            if (Count > 0) State = NoQuarterState;
        }

        // each action just delegates to the current state object
        public void InsertQuarter() { State.InsertQuarter(); }
        public void EjectQuarter()  { State.EjectQuarter(); }
        public void TurnCrank()     { State.TurnCrank(); State.Dispense(); }

        public void ReleaseBall()
        {
            Console.WriteLine("A ball comes rolling down");
            if (Count == 0) return;
            Count--;
        }
    }
}

using System;
namespace StatePattern
{
    public class NoQuarterState : IState
    {
        public GumballMachine Machine { get; }
        public NoQuarterState(GumballMachine machine) { Machine = machine; }

        public void InsertQuarter()
        {
            Console.WriteLine("Inserted a quarter");
            Machine.State = Machine.HasQuarterState;   // state transition lives in the state
        }
        public void EjectQuarter() { Console.Write("Can't eject anything"); }
        public void TurnCrank()    { Console.WriteLine("Can't turn crank without a quarter"); }
        public void Dispense()     { Console.WriteLine("Can't dispense"); }
    }
}

using System;
namespace StatePattern
{
    public class HasQuarterState : IState
    {
        private GumballMachine Machine { get; }
        readonly Random _random = new Random(DateTime.Now.Millisecond);
        public HasQuarterState(GumballMachine gumballMachine) { Machine = gumballMachine; }

        public void InsertQuarter() { Console.WriteLine("Can't insert more than one"); }
        public void EjectQuarter()  { Console.WriteLine("Quarter returned"); Machine.State = Machine.NoQuarterState; }
        public void TurnCrank()
        {
            Console.WriteLine("You turned the crank");
            var winner = _random.Next(10);
            if ((winner == 5) && (Machine.Count > 1)) Machine.State = Machine.WinnerState;
            else                                       Machine.State = Machine.SoldState;
        }
        public void Dispense() { Console.WriteLine("Can't do that"); }
    }
}

// SoldState.Dispense() releases a ball then moves to NoQuarterState (or SoldOutState if Count hits 0).
// WinnerState.Dispense() releases two balls. SoldOutState rejects everything.
```

## How it wires (from Program.cs)
```csharp
using System;

namespace StatePattern
{
    static class Program
    {
        public static void Main()
        {
            var gumballmachine = new GumballMachine(5);
            gumballmachine.InsertQuarter();   // NoQuarter -> HasQuarter
            gumballmachine.TurnCrank();       // HasQuarter -> Sold (or Winner) -> dispenses -> NoQuarter
            gumballmachine.InsertQuarter();
            gumballmachine.TurnCrank();
        }
    }
}
```

## C# 5 notes
- The states use getter-only auto-properties (`public GumballMachine Machine { get; }`, C# 6+) and the context uses `public int Count { get; private set; }` (C# 5 valid) plus `public IState State { get; set; }`. For the read-only `Machine` property in C# 5, change it to `{ get; private set; }` and assign it in the constructor (which the code already does).
- No string interpolation, `?.`, or other C# 6+ syntax in the state logic - apart from the getter-only auto-properties this is C# 5 compatible.
- Transitions are driven by each state assigning `Machine.State = Machine.SomeOtherState`, so the context never contains the branching logic.
