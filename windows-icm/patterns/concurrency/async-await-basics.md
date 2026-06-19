<!--icm
{
  "id": "async-await-basics",
  "title": "Async/await basics (Task-returning methods done right)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "The C# 5 async method shape - async Task / async Task<T>, Task.Run for CPU-bound work, Task.WhenAll, awaiting IO - plus the anti-patterns (no async void, never .Result/.Wait() on a UI context, ConfigureAwait(false) in library code).",
  "keywords": ["async", "await", "Task", "Task<T>", "Task.Run", "Task.WhenAll", "ConfigureAwait", "async void", "deadlock", ".Result", ".Wait()", "CPU-bound", "IO-bound"],
  "source": { "origin": "authored", "note": "C# 5 / in-box .NET Framework 4.8; compiled with the in-box csc to verify" }
}
-->
# Async/await basics

## Intent

Write methods that release the calling thread while waiting (IO, timers, other tasks) and run
CPU-bound work off the caller's thread, using the async/await that shipped in C# 5. Callers compose
and await them, and exceptions surface naturally at the await.

## When to use

- The method does naturally async IO (network, disk, a `Task`-returning API): mark it `async Task` /
  `async Task<T>` and `await` the inner operation. Do NOT wrap IO in `Task.Run`.
- The method does CPU-bound work you want off the current thread (especially a UI thread): hand the
  work to `Task.Run` and `await` it.
- You have several independent tasks to run concurrently: start them all, then `await Task.WhenAll`.

## Key code

```csharp
using System;
using System.Threading.Tasks;

namespace Icm.Patterns.Concurrency
{
    public class AsyncBasics
    {
        // CPU-bound work: offload to the thread pool with Task.Run, then await it.
        public Task<int> SumAsync(int[] data)
        {
            return Task.Run(delegate
            {
                int total = 0;
                for (int i = 0; i < data.Length; i++) total += data[i];
                return total;
            });
        }

        // Naturally async work: await it directly, do NOT wrap in Task.Run.
        // In library code add ConfigureAwait(false) so we do not capture a sync context.
        public async Task<int> DelayedDoubleAsync(int value)
        {
            await Task.Delay(50).ConfigureAwait(false);
            return value * 2;
        }

        // Run independent tasks concurrently and gather the results.
        public async Task<int> TotalOfDoublesAsync(int[] values)
        {
            Task<int>[] tasks = new Task<int>[values.Length];
            for (int i = 0; i < values.Length; i++)
                tasks[i] = DelayedDoubleAsync(values[i]);

            int[] doubled = await Task.WhenAll(tasks).ConfigureAwait(false);

            int total = 0;
            for (int i = 0; i < doubled.Length; i++) total += doubled[i];
            return total;
        }

        // A console top level cannot be async in C# 5 (no async Main). Block ONCE here,
        // at the boundary, with GetAwaiter().GetResult() - never inside library code.
        public int RunFromSyncEntryPoint(int[] values)
        {
            return TotalOfDoublesAsync(values).GetAwaiter().GetResult();
        }
    }
}
```

## Notes

- **No `async void`** except a UI event handler. An `async void` method cannot be awaited and its
  exceptions escape onto the thread pool, crashing the process. Return `async Task` instead.
- **Never `.Result` / `.Wait()` on a captured UI/sync context** (WinForms/WPF). The continuation wants
  the UI thread, the UI thread is blocked waiting on the task, and you deadlock. Await instead. If you
  truly must block at a non-UI boundary (a C# 5 console has no `async Main`), block exactly once with
  `task.GetAwaiter().GetResult()`.
- **`ConfigureAwait(false)` in library code** so the continuation does not try to resume on the
  caller's sync context - both faster and deadlock-proof. Omit it in UI code that must resume on the
  UI thread.
- **`Task.Run` is for CPU-bound work only.** Wrapping IO in `Task.Run` just burns a thread pool thread
  while the real work is the IO.
- **NOT available in-box:** `ValueTask` / `ValueTask<T>` (NuGet `System.Threading.Tasks.Extensions`),
  `IAsyncEnumerable<T>` / `await foreach` (C# 8), and `async Main` (C# 7.1). Stick to `Task` /
  `Task<T>` and block at the boundary with `GetAwaiter().GetResult()`.
