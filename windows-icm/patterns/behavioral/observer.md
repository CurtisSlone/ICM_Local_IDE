<!--icm
{
  "id": "observer",
  "title": "Observer pattern (notify dependents when subject state changes)",
  "doc_type": "pattern",
  "summary": "Defines a one-to-many dependency so that when one subject changes state all its registered observers are notified, here using .NET's IObservable<T>/IObserver<T>.",
  "keywords": ["observer", "publish subscribe", "pub sub", "IObservable", "IObserver", "Subscribe", "OnNext", "IDisposable", "Unsubscriber", "behavioral pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/ObserverPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Observer pattern

## Intent
Define a one-to-many dependency between objects so that when one object (the subject/provider) changes state, all its dependents (observers) are notified automatically. This example uses the built-in .NET interfaces `IObservable<T>` and `IObserver<T>` rather than C# events.

## When to use
- A change to one object requires changing an unknown number of others.
- You want loose coupling between the publisher and its subscribers.
- Subscribers should be able to join and leave at runtime.
- NOT when there is a single, fixed dependent - a direct call is simpler.

## Participants
- Subject / provider - `WeatherSupplier` (implements `IObservable<Weather>`)
- Observer - `WeatherMonitor` (implements `IObserver<Weather>`)
- Data / event payload - `Weather`
- Subscription token - `Unsubscriber<TWeather>` (implements `IDisposable`, removes the observer on `Dispose`)
- Client - `Program`

## Key code
```csharp
using System;
using System.Collections.Generic;

namespace ObserverPattern
{
    class WeatherSupplier : IObservable<Weather>
    {
        private readonly List<IObserver<Weather>> _observers;
        private List<Weather> Screens { get; }

        public WeatherSupplier()
        {
            _observers = new List<IObserver<Weather>>();
            Screens = new List<Weather>();
        }

        public IDisposable Subscribe(IObserver<Weather> observer)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
                foreach (var item in Screens) observer.OnNext(item); // replay cached state
            }
            return new Unsubscriber<Weather>(_observers, observer);   // token to cancel
        }

        public void WeatherConditions(double temp = 0, double humd = 0, double pres = 0)
        {
            var conditions = new Weather(humd, pres, temp);
            foreach (var item in _observers) item.OnNext(conditions); // push to all observers
        }
    }
}

using System;
namespace ObserverPattern
{
    sealed class WeatherMonitor : IObserver<Weather>
    {
        private IDisposable _cancellation;
        private readonly string _name;

        public WeatherMonitor(string name) { _name = name; }

        public void Subscribe(WeatherSupplier provider) { _cancellation = provider.Subscribe(this); }
        public void Unsubscribe() { _cancellation.Dispose(); }

        public void OnCompleted() { throw new NotImplementedException(); }
        public void OnError(Exception error) { Console.WriteLine("Error has occured"); }

        public void OnNext(Weather value)
        {
            Console.WriteLine(_name);
            if (_name.Contains("T")) Console.Write(string.Format("| Temperature : {0} Celsius |", value.Temperature));
            if (_name.Contains("P")) Console.Write(string.Format("| Pressure : {0} atm |", value.Pressure));
            if (_name.Contains("H")) Console.Write(string.Format("| Humidity : {0} % |", value.Humidity * 100));
            if (!(_name.Contains("T") || _name.Contains("P") || _name.Contains("H"))) OnError(new Exception());
            Console.WriteLine();
        }
    }
}

using System;
using System.Collections.Generic;
namespace ObserverPattern
{
    internal class Unsubscriber<TWeather> : IDisposable
    {
        private readonly List<IObserver<TWeather>> _observers;
        private readonly IObserver<TWeather> _observer;
        internal Unsubscriber(List<IObserver<TWeather>> observers, IObserver<TWeather> observer)
        {
            _observers = observers; _observer = observer;
        }
        public void Dispose()
        {
            if (_observers.Contains(_observer)) _observers.Remove(_observer);
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
namespace ObserverPattern
{
    static class Program
    {
        static void Main()
        {
            var provider = new WeatherSupplier();
            var observer1 = new WeatherMonitor("TP"); // wants Temperature + Pressure
            var observer2 = new WeatherMonitor("H");  // wants Humidity

            provider.WeatherConditions(32.0, 0.05, 1.5);  // no subscribers yet -> nothing prints
            observer1.Subscribe(provider);
            provider.WeatherConditions(33.5, 0.04, 1.7);  // observer1 notified
            observer2.Subscribe(provider);
            provider.WeatherConditions(37.5, 0.07, 1.2);  // both notified
        }
    }
}
```

## C# 5 notes
- This is the `IObservable<T>` / `IObserver<T>` form of Observer, not C# `events`. The subject pushes data via `observer.OnNext(value)`, and `Subscribe` returns an `IDisposable` (`Unsubscriber<T>`) used to detach. All of this works on C# 5 / .NET 4.x.
- The source uses string interpolation in `OnNext` (`$"| Temperature : {value.Temperature} Celsius |"`, C# 6+); use `string.Format(...)` in C# 5 as shown above.
- `Screens` is a getter-only auto-property (`private List<Weather> Screens { get; }`, C# 6+); in C# 5 use `{ get; private set; }` and assign it in the constructor (which it already does).
- Optional parameters with defaults on `WeatherConditions(double temp = 0, ...)` are valid in C# 5.
