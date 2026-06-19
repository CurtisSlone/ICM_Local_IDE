# patterns/concurrency/

Concurrency and async patterns for C# 5 / in-box .NET Framework 4.8. Every example here is compiled
against the in-box csc. Ground rules and the in-box-vs-NuGet gaps live in
`reference/dotnet/concurrency-inbox`.

Current entries:
- `async-await-basics` - the async method shape + the anti-patterns (no `async void`, no `.Result` on a UI context).
- `producer-consumer` - `BlockingCollection<T>` (the in-box stand-in for `Channel<T>`, which is not available).
- `parallel-aggregate` - `Parallel.For` over chunks merged with `ConcurrentBag<T>` / `Interlocked`.
- `cancellation` - cooperative cancellation with `CancellationToken(Source)`, incl. timeouts.
- `throttle-with-semaphore` - bound concurrent operations with `SemaphoreSlim`.

Reminder: `ValueTask`, `Channel<T>`, and TPL Dataflow are NOT in-box (NuGet only). Each file: an
`<!--icm {...}-->` block, a compiled C# 5 example, then `icm reindex windows-icm`.
