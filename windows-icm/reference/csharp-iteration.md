<!--icm
{
  "id": "csharp-iteration",
  "title": "Iteration conventions: loop choice, loops vs recursion, safe iteration",
  "doc_type": "reference",
  "group": "csharp",
  "summary": "When to use for / foreach / while / do-while, when to prefer iteration over recursion (.NET has no guaranteed tail-call optimization, so deep recursion overflows the stack), and how to iterate safely: never mutate a collection during foreach, keep every loop bounded, and convert deep/unbounded recursion to an explicit stack.",
  "keywords": ["loop", "for", "foreach", "while", "do while", "recursion", "iteration", "stack overflow", "tail call", "InvalidOperationException", "safe iteration", "bounded loop", "off by one", "RemoveAll", "Stack"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc iteration conventions for the local model" }
}
-->
# Iteration conventions

Decision guide for writing loops and recursion that compile under C# 5 and do not hang or overflow.

## Which loop

- **`foreach` - the default for reading a collection.** Use it whenever you visit every element and
  do not need the index. Clearer, no off-by-one, no bounds math.
  ```csharp
  foreach (TodoItem item in items) { Console.WriteLine(item.Title); }
  ```
- **`for` - when you need the index or controlled stepping.** Indexing arrays/lists by position,
  counting, reverse iteration, skipping, or mutating a list by index.
  ```csharp
  for (int i = 0; i < board.Length; i++) { ... }          // need i
  for (int i = list.Count - 1; i >= 0; i--) { ... }       // safe removal (see below)
  ```
- **`while` - condition-first, may run zero times.** The end is not a simple count: read until EOF,
  poll a state, consume tokens until exhausted (the Expr evaluator advances a token position with
  `while (pos < tokens.Count && ...)`).
- **`do/while` - body runs at least once, condition checked after.** Rare. Only when you genuinely
  need one guaranteed pass (e.g. prompt-then-validate). Otherwise prefer `while`.

## Loops vs recursion

Default to **iteration**. The .NET runtime does **not** guarantee tail-call optimization, so a
recursive method that goes deep throws `StackOverflowException` - which **cannot be caught** and kills
the process.

- **Recursion is fine when depth is bounded and shallow** and the shape is naturally recursive:
  tree/graph walks, divide-and-conquer (depth ~O(log n)), recursive-descent parsing (the `Evaluator`
  in the Expr project is the right use - expression nesting is shallow).
- **Convert to an explicit `Stack<T>`/`Queue<T>` when depth could be large or unbounded:** walking a
  deep directory tree, a long linked list, deeply nested data. Rule of thumb: if recursion depth could
  exceed a few thousand, make it iterative.
  ```csharp
  var stack = new Stack<Node>();
  stack.Push(root);
  while (stack.Count > 0) { Node n = stack.Pop(); /* visit */ foreach (Node c in n.Children) stack.Push(c); }
  ```

## Safe iteration

- **Never modify a collection while `foreach`-ing it** - it throws `InvalidOperationException`. To
  remove while iterating, do one of:
  - iterate **backwards** with a `for` loop and `RemoveAt(i)`;
  - collect the items to remove, then remove them after the loop;
  - use `list.RemoveAll(delegate(T x) { return shouldRemove(x); });` (C# 5: use an anonymous delegate
    or a named method, not an expression lambda body if you are avoiding lambdas).
- **Keep bounds correct:** loop `i < arr.Length` / `i < list.Count`; re-read `Count` if the loop body
  changes it.
- **Bound every loop's termination.** A `while`/recursion driven by external or uncertain input must
  have a guaranteed exit. If the exit depends on data that might be malformed, add a max-iteration cap
  so a bad input cannot hang. (This is exactly why the host's repair `loop` node has `maxIterations`.)
- **Avoid `while (true)` / `for (;;)`** unless there is an unmissable `break`; prefer a real condition.
- **Do not enumerate a lazy `IEnumerable` twice** (re-runs the query, or fails on a one-shot stream).
  Materialize once with `ToList()` if you need to iterate more than once or take `.Count`.
- Watch integer overflow when accumulating over large counts - use `long` if a sum can exceed `int`.
