<!--icm
{
  "id": "dsa-dynamic-programming",
  "title": "Dynamic programming: memoization vs tabulation, and why it beats naive recursion",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "Dynamic programming solves overlapping subproblems once and reuses the result, turning naive O(2^n) Fibonacci into O(n); covers memoization (top-down) vs tabulation (bottom-up) with compile-verified C# 5.",
  "keywords": ["dynamic programming", "dp", "memoization", "tabulation", "top-down", "bottom-up", "overlapping subproblems", "optimal substructure", "fibonacci", "cache", "Dictionary", "recursion", "complexity"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Dynamic programming: memoization vs tabulation

Dynamic programming (DP) is recursion that stops repeating work. When a problem solves the same
subproblems many times, DP computes each one once, stores the answer, and reuses it.

## The concept

DP applies when a problem has both:

- **Overlapping subproblems** - the recursion solves the same inputs repeatedly. Naive Fibonacci
  recomputes `Fib(k)` an exponential number of times; that repetition is what DP removes.
- **Optimal substructure** - the answer is built from the answers to smaller subproblems
  (`Fib(n) = Fib(n-1) + Fib(n-2)`).

Two ways to apply it:

- **Memoization (top-down)** - keep the recursion, add a cache. Before computing a subproblem, check
  the cache; on a miss, compute and store. Natural to write, but still uses recursion (so O(n) stack
  depth here) and the cache holds all solved entries.
- **Tabulation (bottom-up)** - drop the recursion. Iterate from the base cases upward, filling a
  table (or just the last few values). No call stack, and you can often keep only what the next step
  needs, dropping space to O(1).

## Complexity: why DP wins

| Approach | Time | Space | Why |
|---|---|---|---|
| Naive recursive Fibonacci | O(2^n) | O(n) stack | branches into ~2^n calls, recomputing subproblems |
| DP memoized (top-down) | O(n) | O(n) cache + O(n) stack | each of n subproblems computed once |
| DP tabulated (bottom-up) | O(n) | O(1) | iterate up, keep only the last two values |

The contrast is the whole point. Naive `Fib(50)` is on the order of 10^10 calls; the iterative DP is
50 additions. Same recurrence, exponential versus linear, because DP solves each subproblem once
instead of re-descending the call tree. Bottom-up also drops the O(n) recursion stack to O(1), which
matters in C# where deep recursion risks `StackOverflowException` (see [[csharp-iteration]]).

## When to use, when not

Use DP when you see the same subproblem being solved repeatedly and the result depends only on the
inputs (Fibonacci, coin change, edit distance, longest common subsequence, knapsack).

Prefer **tabulation** in C# when you can express the order of subproblems as a loop: it avoids the
call stack entirely and is the safest default ([[csharp-iteration]]). Reach for **memoization** when
the recursive shape is clearer or the subproblem space is sparse (you only touch some of it). But
remember memoization still recurses, so for deep inputs convert it to bottom-up or an explicit stack.

Do not bother with DP when subproblems do not overlap (plain divide-and-conquer like a single-pass
binary search gains nothing) or when a direct closed form exists.

## Reference implementation (C# 5)

```csharp
using System;
using System.Collections.Generic;

static class FibonacciDP
{
    // Top-down DP (memoization): recursion plus a cache of solved subproblems.
    // O(n) time, O(n) space. The cache turns the O(2^n) naive tree into n unique calls.
    private static readonly Dictionary<int, long> _cache = new Dictionary<int, long>();

    public static long FibMemo(int n)
    {
        if (n < 2)
        {
            return n;
        }
        if (_cache.ContainsKey(n))
        {
            return _cache[n];
        }
        long value = FibMemo(n - 1) + FibMemo(n - 2);
        _cache[n] = value;
        return value;
    }

    // Bottom-up DP (tabulation), iterative. O(n) time, O(1) space.
    // No recursion, so no stack-depth risk. This is the version to reach for.
    public static long FibIterative(int n)
    {
        if (n < 2)
        {
            return n;
        }
        long prev = 0;
        long curr = 1;
        for (int i = 2; i <= n; i++)
        {
            long next = prev + curr;
            prev = curr;
            curr = next;
        }
        return curr;
    }
}
```

## Pitfalls and notes

- **The memo cache is the whole trick.** Check it before computing and store after. The original
  ported code checked the cache before the base-case guard, so it never cached `0` or `1` and could
  return the wrong default for unseen small inputs; check the base case first, then the cache.
- **Memoization still recurses.** `FibMemo` fills the cache top-down, so the first call descends to
  depth n. For large n that is still a stack risk in C# ([[csharp-iteration]]); the iterative form has
  none.
- **A shared static cache persists across calls and is not thread-safe.** Fine for single-threaded
  use; if multiple threads call it, guard the `Dictionary` or give each caller its own cache.
- **Result type overflows fast.** Fibonacci grows exponentially; `Fib(93)` already overflows `long`.
  The original ported sample computed `Fib(1000)` into an `int`, which silently overflows. Use a wide
  type and bound the input, or move to `System.Numerics.BigInteger` if you truly need large values.
- **Tabulation can usually shrink space.** Full DP often needs a table, but Fibonacci only depends on
  the last two values, so O(1) space suffices. Look for that reduction whenever the recurrence has a
  fixed, small window.

## See also

- [[dsa-recursion]] - base cases, the call stack, and the naive recursive Fibonacci this fixes.
- [[csharp-iteration]] - why bottom-up iteration is the safer default in C# (no tail-call optimization).
