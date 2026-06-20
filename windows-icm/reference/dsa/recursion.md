<!--icm
{
  "id": "dsa-recursion",
  "title": "Recursion: structure, base cases, and when to avoid it",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "How recursion works (base case plus recursive case on the call stack), its complexity, and why in C# you usually convert deep recursion to iteration because .NET has no guaranteed tail-call optimization.",
  "keywords": ["recursion", "base case", "recursive case", "call stack", "stack overflow", "StackOverflowException", "factorial", "fibonacci", "reverse string", "tail call", "iteration", "depth"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Recursion: structure, base cases, and when to avoid it

A recursive method solves a problem by calling itself on a smaller input until it reaches a case
small enough to answer directly. Useful when the problem is naturally self-similar, but in C# it
carries a real stack-overflow risk.

## The concept

Every correct recursion has two parts:

- **Base case** - the smallest input the method answers without recursing. This is what stops the
  recursion. A missing or wrong base case means the method never stops and the process dies.
- **Recursive case** - the method reduces the input toward the base case and calls itself.

Each call gets its own stack frame (parameters, locals, return address). The frames pile up on the
**call stack** as the recursion descends and unwind as each call returns. Recursion depth equals the
height of that stack. .NET frames are not free, and the stack is bounded (about 1 MB by default), so
depth is the cost that matters most in C#.

## Complexity

| Method | Time | Space (stack depth) | Notes |
|---|---|---|---|
| Factorial (recursive) | O(n) | O(n) | one call per level |
| Reverse string (recursive) | O(n^2) | O(n) | Substring copies each level, so n + (n-1) + ... |
| Reverse string (iterative) | O(n) | O(1) | bounded loop, preferred |
| Fibonacci (naive recursive) | O(2^n) | O(n) | recomputes overlapping subproblems |
| Fibonacci (accumulator recursive) | O(n) | O(n) | linear calls, still linear depth |

Naive Fibonacci is the cautionary case: `Fib(n)` calls `Fib(n-1)` and `Fib(n-2)`, which re-solve the
same subproblems over and over, branching into roughly 2^n calls. Adding memoization collapses that
to O(n); see [[dsa-dynamic-programming]].

## When to use, when not

Use recursion when depth is **bounded and shallow** and the shape is naturally recursive: tree and
graph walks, divide-and-conquer (depth about O(log n)), recursive-descent parsing.

Avoid it when depth could be large or unbounded. In C# this is not a style preference: the .NET
runtime does **not** guarantee tail-call optimization, so a deep recursion throws
`StackOverflowException`, which **cannot be caught** and tears down the process. The accumulator
Fibonacci below is tail-recursive in form but C# will not flatten it, so it still uses O(n) stack.

Rule of thumb: if recursion depth could exceed a few thousand, make it iterative or convert it to an
explicit `Stack<T>`. See [[csharp-iteration]] for the loops-vs-recursion decision and the
explicit-stack pattern.

## Reference implementation (C# 5)

```csharp
using System;
using System.Text;

static class Recursion
{
    // Factorial: n! = n * (n-1)!  Base case n <= 1 returns 1.
    public static long Factorial(int n)
    {
        if (n <= 1)
        {
            return 1;
        }
        return n * Factorial(n - 1);
    }

    // Naive recursive Fibonacci. O(2^n) time - recomputes overlapping subproblems.
    public static int Fib(int n)
    {
        if (n < 2)
        {
            return n;
        }
        return Fib(n - 1) + Fib(n - 2);
    }

    // Linear-time recursive Fibonacci via accumulators. O(n) calls, still O(n) stack depth.
    public static int FibAcc(int n, int curr, int prev)
    {
        if (n == 0)
        {
            return prev;
        }
        if (n == 1)
        {
            return curr;
        }
        return FibAcc(n - 1, curr + prev, curr);
    }

    // Recursive string reversal. Depth = str.Length, so it overflows on long strings.
    public static string ReverseRecursive(string str)
    {
        if (str == null || str.Length == 0)
        {
            return "";
        }
        return ReverseRecursive(str.Substring(1)) + str[0];
    }

    // Iterative reversal: bounded, no recursion depth risk. Prefer this.
    public static string ReverseIterative(string str)
    {
        if (str == null)
        {
            return "";
        }
        StringBuilder result = new StringBuilder(str.Length);
        for (int i = str.Length - 1; i >= 0; i--)
        {
            result.Append(str[i]);
        }
        return result.ToString();
    }
}
```

Call the accumulator form with the seeds: `Recursion.FibAcc(n, 1, 0)`.

## Pitfalls and notes

- **Missing or wrong base case = infinite recursion = stack overflow.** The original factorial used
  `if (number == 1)`, which never terminates for `0` or negative input; `n <= 1` is the safe guard.
- **Watch the boundary direction.** The recursive case must move strictly toward the base case
  (`n - 1`, `str.Substring(1)`). If it can step away or stand still, it never bottoms out.
- **Naive Fibonacci recomputes.** Each non-base call spawns two more; the same `Fib(k)` is evaluated
  many times. Memoize or tabulate to fix it - see [[dsa-dynamic-programming]].
- **Recursive string reverse is O(n^2) and O(n)-deep.** `Substring` allocates a new string each
  level. The iterative version is O(n) time, O(1) extra depth - prefer it for anything but tiny input.
- **No tail-call relief in C#.** Even tail-recursive shapes consume a frame per level. For deep work,
  iterate or use an explicit `Stack<T>` ([[csharp-iteration]]).
- **Overflow of the result type is separate from stack overflow.** `Factorial` returns `long`, but
  21! already overflows `long`; pick a wide enough type or guard the input range.

## See also

- [[csharp-iteration]] - loops vs recursion, tail calls, converting recursion to an explicit stack.
- [[dsa-dynamic-programming]] - memoization and tabulation that fix naive recursive Fibonacci.
