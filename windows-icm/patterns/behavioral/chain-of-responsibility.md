<!--icm
{
  "id": "chain-of-responsibility",
  "title": "Chain of Responsibility pattern (pass a request along a chain of handlers)",
  "doc_type": "pattern",
  "summary": "Decouples a sender from its receivers by giving each handler a chance to process a request or forward it to the next handler in the chain.",
  "keywords": ["chain of responsibility", "CoR", "handler chain", "IHandler", "BaseHandler", "successor", "pipeline", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/ChainOfResponsibilityPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Chain of Responsibility pattern

## Intent
Give more than one object a chance to handle a request by linking the handlers into a chain. Each handler either handles the request or passes it to its successor, so the sender does not need to know which handler will respond.

## When to use
- More than one object may handle a request and the handler is not known up front.
- You want to issue a request without naming the receiver explicitly.
- The set of handlers and their order should be configurable at runtime.
- NOT when every request always has exactly one known handler - a simple call or dictionary dispatch is clearer.

## Participants
- Handler interface - `IHandler` (declares `Handle` and `AddChain`)
- Base handler holding the successor - `BaseHandler` (abstract, stores `_nextInLine`)
- Concrete handlers - `AdditionHandler`, `SubtractionHandler`, `MultiplicationHandler`
- Client - `Program`

## Key code
```csharp
namespace ChainOfResponsibilityPattern {
    public interface IHandler {
        void AddChain(IHandler handler);
        double? Handle(double[] values, string action);
    }
}

namespace ChainOfResponsibilityPattern {
    public abstract class BaseHandler : IHandler {
        public void AddChain(IHandler handler) {
            _nextInLine = handler;
        }

        public abstract double? Handle(double[] values, string action);

        protected IHandler _nextInLine;
    }
}

namespace ChainOfResponsibilityPattern {
    public class AdditionHandler : BaseHandler {
        public override double? Handle(double[] values, string action) {
            if (action.ToLower() == "add") {
                double result = 0.0;
                foreach (var value in values) {
                    result += value;
                }
                return result;
            }
            else {
                // forward to successor; null when chain end is reached
                return _nextInLine == null ? (double?)null : _nextInLine.Handle(values, action);
            }
        }
    }
}

// SubtractionHandler ("minus") and MultiplicationHandler ("multiply") follow the same shape:
// handle the action they own, otherwise delegate to _nextInLine.
```

## How it wires (from Program.cs)
```csharp
using System;

namespace ChainOfResponsibilityPattern {
    class Program {
        static void Main(string[] args) {
            // create handlers
            var additionHandler = new AdditionHandler();
            var subtractionHandler = new SubtractionHandler();
            var multiplicationHander = new MultiplicationHandler();
            // build the chain: addition -> subtraction -> multiplication
            subtractionHandler.AddChain(multiplicationHander);
            additionHandler.AddChain(subtractionHandler);
            // always enter the chain at the head (additionHandler)
            double[] numbers = new double[] { 2, 3, 4, 5 };
            var additionResult = additionHandler.Handle(numbers, "Add");
            var subtractionResult = additionHandler.Handle(numbers, "Minus");
            var multResult = additionHandler.Handle(numbers, "Multiply");
            var divisionResult = additionHandler.Handle(numbers, "divide"); // not in the chain -> null

            Console.WriteLine("Addition = {0}", additionResult);
            Console.WriteLine("Subtraction = {0}", subtractionResult);
            Console.WriteLine("Multiplication = {0}", multResult);
            Console.WriteLine("Division = {0}", divisionResult);
        }
    }
}
```

## C# 5 notes
- The source uses the null-conditional operator `?.` (C# 6+): `_nextInLine?.Handle(values, action)`. In C# 5 replace it with an explicit null check, e.g. `_nextInLine == null ? (double?)null : _nextInLine.Handle(values, action)`. The `(double?)` cast is needed because the two branches of `?:` must agree on type.
- `double?` (Nullable<double>) is fine in C# 5.
- Apart from `?.` the example is C# 5 compatible; it relies on implicit usings only in that `System` is added in Program.cs but the handler files use none.
