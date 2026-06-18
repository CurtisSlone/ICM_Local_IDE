# patterns/behavioral/

How objects interact and divide responsibility. Several of these are language features in C# - show
the idiomatic form:

- **Strategy / Command** - a delegate: `Func<...>` / `Action<...>`. Reach for a full class hierarchy
  only when the strategy carries state or needs discovery.
- **Observer** - `event` / `EventHandler<T>` (or `IObservable<T>`), not a hand-rolled subscriber list.
- **Iterator** - `IEnumerable<T>` + `yield return`.
- **Template Method** - an `abstract` base class with overridable steps.
- **State / Mediator / Chain of Responsibility / Visitor / Memento / Interpreter** - classic forms;
  capture when the problem genuinely fits (e.g. a parser uses Interpreter; undo uses Memento).

Each file: `<!--icm {...}-->` block, C# 5 example, the rules, then `icm reindex windows-icm`.
