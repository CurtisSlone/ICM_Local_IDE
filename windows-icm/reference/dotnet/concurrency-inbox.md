<!--icm
{
  "id": "concurrency-inbox",
  "title": "Concurrency and async in-box (C# 5 + .NET FW 4.8)",
  "doc_type": "reference",
  "summary": "What async/concurrency APIs are available under C# 5 and the in-box framework (Task, async/await, Parallel, BlockingCollection, SemaphoreSlim, Lazy), what is NOT (ValueTask, Channel, Dataflow, IAsyncEnumerable, async Main), and the async rules a model gets wrong.",
  "keywords": ["async", "await", "Task", "CancellationToken", "Parallel", "PLINQ", "BlockingCollection", "SemaphoreSlim", "ConcurrentDictionary", "Lazy", "ValueTask", "Channel", "Dataflow", "async void", "deadlock", "ConfigureAwait"],
  "group": "dotnet",
  "source": { "origin": "authored", "verified": "ValueTask/Channels/Dataflow assemblies confirmed absent from the in-box framework dir on this machine", "note": "C# 5 introduced async/await; later async language features are not available" }
}
-->
# Concurrency and async in-box

This environment is **C# 5** (in-box `csc`) on **.NET Framework 4.8**. `async`/`await` exists (it
shipped in C# 5), and the TPL is in-box. Several modern async APIs are NOT here and need NuGet, which
this environment does not have.

## Available (use these)

- **`async` / `await`**, `System.Threading.Tasks.Task` / `Task<T>`, `Task.Run`, `Task.WhenAll`,
  `Task.WhenAny`, `Task.Delay`, `TaskCompletionSource<T>`.
- **Cancellation**: `CancellationToken`, `CancellationTokenSource` (incl. `new CancellationTokenSource(timeoutMs)`).
- **Progress**: `IProgress<T>` / `Progress<T>`.
- **Data parallelism**: `Parallel.For` / `Parallel.ForEach` / `Parallel.Invoke`; PLINQ `AsParallel`.
- **Concurrent collections**: `ConcurrentDictionary`, `ConcurrentQueue`, `ConcurrentBag`,
  `BlockingCollection<T>` (bounded producer/consumer).
- **Sync primitives**: `lock` / `Monitor`, `Interlocked`, `SemaphoreSlim` (with `WaitAsync`),
  `ReaderWriterLockSlim`, `ManualResetEventSlim`, `Mutex` (cross-process), `Lazy<T>` (thread-safe init).
- `ThreadPool`, `Thread`, `BackgroundWorker`.

## NOT available (do not use)

- `ValueTask` / `ValueTask<T>` (NuGet `System.Threading.Tasks.Extensions`).
- `Channel<T>` (NuGet `System.Threading.Channels`) - **use `BlockingCollection<T>` instead**.
- TPL **Dataflow** (`System.Threading.Tasks.Dataflow`).
- `IAsyncEnumerable<T>` / `await foreach` (C# 8), `await using` / `IAsyncDisposable` (C# 8).
- `async Main` (C# 7.1), local functions (C# 7). At a console top level, block once with
  `task.GetAwaiter().GetResult()`.

## The async rules a model gets wrong

- **No `async void`** except a UI event handler. Return `async Task` so callers can await and observe
  exceptions.
- **Never `.Result` / `.Wait()` on a captured UI/sync context** (WinForms/WPF) - it deadlocks. Await
  instead; in non-UI library code use `await someTask.ConfigureAwait(false)`.
- **`Task.Run` is for CPU-bound work.** Do not wrap naturally async IO in `Task.Run`.
- **Don't fire-and-forget** without observing the returned `Task`'s exceptions.
- **WinForms UI thread**: touch controls only on the UI thread; marshal back with
  `Control.Invoke` / `BeginInvoke` (see the GUI references).

## Useful in-box shapes

- **Timeout**: `var cts = new CancellationTokenSource(5000);` then pass `cts.Token`, or
  `await Task.WhenAny(work, Task.Delay(5000))`.
- **Throttle N concurrent operations**: a `SemaphoreSlim(maxConcurrent)` with `await sem.WaitAsync()` /
  `sem.Release()` in a `finally`.
- **Producer/consumer pipeline**: `BlockingCollection<T>` with `GetConsumingEnumerable()` (the in-box
  stand-in for channels).
- **Parallel aggregate**: `Parallel.For` over chunks writing into a `ConcurrentBag<T>` or with
  `Interlocked` counters.
