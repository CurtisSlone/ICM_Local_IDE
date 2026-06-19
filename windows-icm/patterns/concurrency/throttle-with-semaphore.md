<!--icm
{
  "id": "throttle-with-semaphore",
  "title": "Throttle concurrency with SemaphoreSlim (bound simultaneous work)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "Cap how many operations run at once with SemaphoreSlim(maxConcurrent): await WaitAsync() before the work and Release() in a finally, so you bound simultaneous file/process/network calls.",
  "keywords": ["SemaphoreSlim", "WaitAsync", "Release", "throttle", "concurrency limit", "bound concurrency", "rate limit", "max parallel", "finally", "Task.WhenAll"],
  "source": { "origin": "authored", "note": "C# 5 / in-box .NET Framework 4.8; compiled with the in-box csc to verify" }
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
