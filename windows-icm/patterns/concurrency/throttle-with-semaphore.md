<!--icm
{
  "id": "throttle-with-semaphore",
  "title": "Throttle concurrency with SemaphoreSlim (bound simultaneous work)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "Cap how many operations run at once with SemaphoreSlim(maxConcurrent): await WaitAsync() then Release() in a finally; includes a keyed one-gate-per-name variant and the acquire/release correctness rules (release only the slot you actually acquired - the classic timeout-then-release over-release bug).",
  "keywords": ["SemaphoreSlim", "WaitAsync", "Release", "throttle", "concurrency limit", "bound concurrency", "rate limit", "max parallel", "finally", "Task.WhenAll", "keyed throttle", "named lock", "per-key", "ConcurrentDictionary", "SemaphoreFullException", "WaitAsync timeout", "concurrency rules"],
  "source": { "origin": "authored", "note": "C# 5 / in-box .NET Framework 4.8; compiled with the in-box csc to verify. Keyed variant + rules adapted from a named-semaphore library (Ultranaco.Threading / SemaphoreLock), correcting its over-release and sync-Wait-in-async bugs." }
}
-->
# Throttle concurrency with SemaphoreSlim

## Intent

Run many async operations but allow only N of them in flight at once. A `SemaphoreSlim(maxConcurrent)`
is a counter of available slots: each operation `await`s a slot before starting and releases it when
done, so the Nth+1 operation waits until one finishes.

## When to use

- You have a list of items to process asynchronously but unbounded concurrency would overwhelm
  something - too many open file handles, too many spawned processes, an API rate limit, a connection
  pool.
- You want to start everything and `Task.WhenAll`, but with a ceiling on simultaneity.
- You need an async wait for the slot (`WaitAsync`) so waiting does not block a thread.

## Key code

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Icm.Patterns.Concurrency
{
    public class Throttle
    {
        private readonly SemaphoreSlim _gate;

        public Throttle(int maxConcurrent)
        {
            _gate = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        // Process every item, but never more than maxConcurrent at the same time.
        public async Task<int> ProcessAllAsync(int[] items)
        {
            Task<int>[] tasks = new Task<int>[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                tasks[i] = ProcessOneAsync(items[i]);
            }

            int[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

            int total = 0;
            for (int i = 0; i < results.Length; i++) total += results[i];
            return total;
        }

        private async Task<int> ProcessOneAsync(int item)
        {
            // Wait for a free slot. WaitAsync does not block a thread while waiting.
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Stand-in for the bounded work (a file read, a spawned process, an HTTP call).
                await Task.Delay(10).ConfigureAwait(false);
                return item * 2;
            }
            finally
            {
                // ALWAYS release in finally so an exception cannot leak a slot.
                _gate.Release();
            }
        }
    }
}
```

## Notes

- **`Release()` must be in a `finally`.** If the work throws and you skip the release, that slot is
  gone forever and you slowly starve to a deadlock. The `try`/`finally` guarantees the counter is
  restored on every path.
- **`WaitAsync()` vs `Wait()`:** `WaitAsync` returns a `Task` you `await`, so a waiting operation does
  not tie up a thread. Use it in async code. The synchronous `Wait()` blocks the calling thread - fine
  in genuinely synchronous code, wrong on a UI thread.
- **Pair `WaitAsync` with one `Release`** - it is not reentrant per logical operation. One acquire,
  one release.
- **`WaitAsync(CancellationToken)`** lets a pending acquire be cancelled; combine with the
  cancellation pattern when waits should be abandonable. There is also a timeout overload.
- **NOT available in-box:** nothing extra - `SemaphoreSlim` and its `WaitAsync` are in the in-box
  framework. (`Channel<T>`, an alternative way to bound a pipeline, is the absent NuGet-only API.)

## Keyed throttle (one gate per name)

Sometimes you do not want one global limit but an independent limit per resource - per file, per user,
per host. Keep a `SemaphoreSlim` per key in a `ConcurrentDictionary`: calls with the same key contend;
calls with different keys run freely.

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Icm.Patterns.Concurrency
{
    // One independent gate per key. Same key contends; different keys are independent.
    public static class KeyedThrottle
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _gates =
            new ConcurrentDictionary<string, SemaphoreSlim>();

        private static SemaphoreSlim GateFor(string key, int maxConcurrent)
        {
            return _gates.GetOrAdd(key, delegate(string k) { return new SemaphoreSlim(maxConcurrent, maxConcurrent); });
        }

        // No timeout: the wait always succeeds eventually, so releasing in finally is always correct.
        public static async Task RunAsync(string key, int maxConcurrent, Func<Task> work)
        {
            SemaphoreSlim gate = GateFor(key, maxConcurrent);
            await gate.WaitAsync().ConfigureAwait(false);
            try { await work().ConfigureAwait(false); }
            finally { gate.Release(); }
        }

        // With a timeout you might NOT get a slot. Capture the result and release ONLY if acquired.
        public static async Task<bool> TryRunAsync(string key, int maxConcurrent, int timeoutMs, Func<Task> work)
        {
            SemaphoreSlim gate = GateFor(key, maxConcurrent);
            bool acquired = await gate.WaitAsync(timeoutMs).ConfigureAwait(false);
            if (!acquired) return false;          // did not acquire -> must NOT release
            try { await work().ConfigureAwait(false); return true; }
            finally { gate.Release(); }
        }
    }
}
```

## Rules for use

- **One acquire, one release - and release ONLY on the path that acquired.** The classic bug in
  hand-rolled semaphore locks: call `Wait(timeout)` / `WaitAsync(timeout)`, ignore the returned
  `bool`, and release anyway. If the wait timed out you never took a slot; releasing then pushes the
  count above its maximum and throws `SemaphoreFullException` (or silently admits too many). Capture
  the timeout result and release only when it is `true`.
- **Release in `finally`** so an exception in the work cannot leak a slot (a leaked slot slowly starves
  the gate to a deadlock).
- **In async code use `WaitAsync`, never the blocking `Wait`.** Calling the synchronous `Wait` from an
  async method ties up - and can deadlock - a thread-pool thread. A common mistake is an async wrapper
  whose acquire path still calls the sync `Wait`.
- **Rethrow with `throw;`, not `throw ex;`.** `throw ex;` resets the stack trace to the rethrow point
  and hides where the error really came from.
- **`SemaphoreSlim` is not reentrant.** The same logical operation acquiring twice can deadlock against
  itself once the count is exhausted. One logical unit of work takes one slot.
- **A per-key dictionary of semaphores grows unbounded** if keys are transient. Fine for a fixed, small
  key set; otherwise evict idle gates or use a bounded cache.
- **`using`/`Dispose` to release is safe ONLY when acquisition cannot fail** (no timeout). If the
  acquire can time out, a `using` that always releases on dispose reintroduces the over-release bug.

See also [[producer-consumer]] (bounding a pipeline another way), [[cancellation]] (abandonable waits),
and the in-box concurrency primitives in [[concurrency-inbox]].
