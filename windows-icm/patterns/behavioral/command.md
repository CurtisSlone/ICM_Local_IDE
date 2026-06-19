<!--icm
{
  "id": "command",
  "title": "Command pattern (encapsulate a request as an object)",
  "doc_type": "pattern",
  "summary": "Wraps a request and its receiver in an object so invokers can parameterize, queue, log, undo, and macro requests without knowing what they do.",
  "keywords": ["command", "ICommand", "invoker", "receiver", "undo", "MacroCommand", "RemoteControl", "NoCommand", "null object", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/CommandPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Command pattern

## Intent
Encapsulate a request as an object, letting you parameterize clients with different requests, support undo, and compose requests into macros. The invoker (remote) holds commands and triggers `Execute`/`Undo` without knowing the receiver (light, garage).

## When to use
- You want to decouple the object that invokes an operation from the one that performs it.
- You need undo/redo, queuing, logging, or macro (composite) operations.
- You want to assign behavior to slots/buttons at runtime.
- NOT for a single direct method call with no need for these features.

## Participants
- Command interface - `ICommand` (`Execute`, `Undo`)
- Concrete commands - `LightOnCommand`, `LightOffCommand`, `GarageDoorOpenCommand`, `GarageDoorCloseCommand`
- Null Object command - `NoCommand` (safe default, avoids null checks)
- Macro/composite command - `MacroCommand` (runs an array of commands)
- Receivers - `Light`, `Garage`
- Invoker - `RemoteControl` (slots, push on/off, undo)
- Helper struct - `OnOffStruct` (pairs the on and off command for a slot)
- Client - `Program`

## Key code
```csharp
namespace CommandPattern
{
    internal interface ICommand
    {
        void Execute();
        void Undo();
    }
}

using System;
namespace CommandPattern
{
    internal class NoCommand : ICommand
    {
        public void Execute() { Console.WriteLine("No Command Assigned"); }
        public void Undo() { Execute(); }
    }
}

namespace CommandPattern
{
    internal class LightOnCommand : ICommand
    {
        private readonly Light _light;
        public LightOnCommand(Light l) { _light = l; }
        public void Execute() { _light.On(); }
        public void Undo() { _light.Off(); }
    }
}
// LightOffCommand, GarageDoorOpenCommand, GarageDoorCloseCommand are the mirror image:
// Execute does one action on the receiver, Undo does the opposite.

namespace CommandPattern
{
    internal class MacroCommand : ICommand
    {
        private readonly ICommand[] _commands;
        public MacroCommand(ICommand[] commands) { _commands = commands; }
        public void Execute() { foreach (var item in _commands) item.Execute(); }
        public void Undo()    { foreach (var item in _commands) item.Undo(); }
    }
}

namespace CommandPattern
{
    internal struct OnOffStruct
    {
        public ICommand On;
        public ICommand Off;
    }
}

namespace CommandPattern
{
    internal class RemoteControl
    {
        private readonly ICommand[] _offCommand;
        private readonly ICommand[] _onCommand;
        private ICommand _undoCommand;

        public RemoteControl(int slots)
        {
            _onCommand = new ICommand[slots];
            _offCommand = new ICommand[slots];
            var none = new NoCommand();      // Null Object default for every slot
            _undoCommand = none;
            for (var i = 0; i < slots; i++)
            {
                _onCommand[i] = none;
                _offCommand[i] = none;
            }
        }

        public OnOffStruct this[int i]       // indexer assigns both commands of a slot
        {
            set
            {
                _onCommand[i] = value.On;
                _offCommand[i] = value.Off;
            }
        }

        public void PushOn(int slot)  { _onCommand[slot].Execute();  _undoCommand = _offCommand[slot]; }
        public void PushOff(int slot) { _offCommand[slot].Execute(); _undoCommand = _onCommand[slot]; }
        public void PushUndo()        { _undoCommand.Execute(); }
    }
}

// Receiver example:
using System;
namespace CommandPattern
{
    public class Light
    {
        private readonly string _name;
        public Light(string name) { _name = name; }
        internal void On()  { Console.WriteLine(_name + " Light On"); }
        internal void Off() { Console.WriteLine(_name + " Light Off"); }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace CommandPattern
{
    internal static class Program
    {
        private static void Main()
        {
            var remote = new RemoteControl(3);

            var bike = new Garage("Bike");
            var bikeDoorClose = new GarageDoorCloseCommand(bike);
            var bikeDoorOpen = new GarageDoorOpenCommand(bike);

            var car = new Garage("Car");
            var carDoorClose = new GarageDoorCloseCommand(car);
            var carDoorOpen = new GarageDoorOpenCommand(car);

            var garageButton = new OnOffStruct { On = bikeDoorOpen, Off = bikeDoorClose };
            remote[0] = garageButton;
            remote.PushOn(0);
            remote.PushUndo();
            remote.PushUndo();
            remote.PushOff(0);

            Console.WriteLine();
            var light = new Light("Hall");

            ICommand[] partyOn  = { new LightOffCommand(light), bikeDoorOpen, carDoorOpen };
            ICommand[] partyOff = { new LightOnCommand(light),  bikeDoorClose, carDoorClose };

            remote[2] = new OnOffStruct { On = new MacroCommand(partyOn), Off = new MacroCommand(partyOff) };
            remote.PushOn(2);
            Console.WriteLine();
            remote.PushOff(2);
        }
    }
}
```

## C# 5 notes
- The receivers `Light` and `Garage` use string interpolation `$"{_name} Light On"` (C# 6+). In C# 5 use `_name + " Light On"` or `string.Format("{0} Light On", _name)`.
- Everything else (the `ICommand` interface, concrete commands, `NoCommand` as a Null Object, `MacroCommand`, the `OnOffStruct`, the indexer, object-initializer syntax) is valid C# 5.
- `NoCommand` is the Null Object pattern: `RemoteControl` fills every slot with it so `PushOn`/`PushOff` never call a null command.
