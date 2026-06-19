<!--icm
{
  "id": "mediator",
  "title": "Mediator pattern (centralize how a set of objects interact)",
  "doc_type": "pattern",
  "summary": "Routes communication between colleagues through a single mediator object so the colleagues stay decoupled and the interaction logic lives in one place.",
  "keywords": ["mediator", "colleague", "Mediator", "Colleague", "ManagerMediator", "decoupling", "hub", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/MediatorPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Mediator pattern

## Intent
Define an object that encapsulates how a set of objects interact. Colleagues send messages to the mediator instead of to each other, so they do not hold references to one another and the routing rules live in one place.

## When to use
- A set of objects communicate in well-defined but complex (many-to-many) ways.
- Reusing a component is hard because it refers to and talks to many others.
- You want to change interaction behavior by swapping mediators instead of editing every colleague.
- NOT when interactions are simple - a mediator can grow into a god object that concentrates too much logic.

## Participants
- Mediator abstraction - `Mediator` (declares `Send(string, Colleague)`)
- Concrete mediator - `ManagerMediator` (holds the colleagues, decides who gets notified)
- Colleague abstraction - `Colleague` (holds a `Mediator`, has `Send` and abstract `Notify`)
- Concrete colleagues - `Customer`, `Programmer`, `Tester`
- Client - `Program`

## Key code
```csharp
namespace MediatorPattern
{
    abstract class Mediator
    {
        public abstract void Send(string message, Colleague colleague);
    }
}

namespace MediatorPattern
{
    abstract class Colleague
    {
        protected Mediator mediator;

        public Colleague(Mediator mediator) { this.mediator = mediator; }

        // send routes through the mediator, not directly to another colleague
        public virtual void Send(string message) { this.mediator.Send(message, this); }

        public abstract void Notify(string message);
    }
}

namespace MediatorPattern
{
    class ManagerMediator : Mediator
    {
        public Colleague Customer { get; set; }
        public Colleague Programmer { get; set; }
        public Colleague Tester { get; set; }

        // all routing rules live here: customer -> programmer -> tester -> customer
        public override void Send(string message, Colleague colleague)
        {
            if (colleague == Customer)        { Programmer.Notify(message); }
            else if (colleague == Programmer) { Tester.Notify(message); }
            else                                Customer.Notify(message);
        }
    }
}

using System;
namespace MediatorPattern
{
    class Customer : Colleague
    {
        public Customer(Mediator mediator) : base(mediator) { }
        public override void Notify(string message)
        {
            Console.WriteLine(string.Format("Message to customer: {0}", message));
        }
    }
    // Programmer and Tester are identical except for the printed role name.
}
```

## How it wires (from Program.cs)
```csharp
namespace MediatorPattern
{
    class Program
    {
        static void Main(string[] args)
        {
            var mediator = new ManagerMediator();
            var customer = new Customer(mediator);
            var programmer = new Programmer(mediator);
            var tester = new Tester(mediator);

            // register colleagues with the mediator so it can route
            mediator.Customer = customer;
            mediator.Programmer = programmer;
            mediator.Tester = tester;

            customer.Send("We have an order, need to make a program");
            programmer.Send("I have done program, need to test it");
            tester.Send("I have done testing, here is ready program for you");
        }
    }
}
```

## C# 5 notes
- `Colleague` uses expression-bodied members (C# 6+):
  `public Colleague(Mediator mediator) => this.mediator = mediator;` and
  `public virtual void Send(string message) => this.mediator.Send(message, this);`.
  In C# 5 use ordinary blocks: `{ this.mediator = mediator; }` and `{ this.mediator.Send(message, this); }`.
- The `Notify` overrides use string interpolation `$"Message to customer: {message}"` (C# 6+); use `string.Format("Message to customer: {0}", message)` in C# 5.
- The colleague-identity routing relies on reference equality (`colleague == Customer`), which is plain C# 5.
