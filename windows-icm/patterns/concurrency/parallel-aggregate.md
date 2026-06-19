<!--icm
{
  "id": "parallel-aggregate",
  "title": "Parallel aggregate (fan out over chunks, merge results)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "Split a large input into chunks, process them with Parallel.For/ForEach across cores, and merge the per-chunk results with Interlocked counters or a ConcurrentBag<T> - e.g. scanning a big byte[] in parallel.",
  "keywords": ["Parallel.For", "Parallel.ForEach", "data parallelism", "Interlocked", "ConcurrentBag", "thread-local", "aggregate", "partition", "chunk", "byte[] scan", "PLINQ"],
  "source": { "origin": "authored", "note": "C# 5 / in-box .NET Framework 4.8; compiled with the in-box csc to verify" }
}
-->
# Parallel aggregate

## Intent

Use all cores to process a large input by partitioning it into chunks, running the chunks
concurrently with `Parallel.For` / `Parallel.ForEach`, and combining the partial results without a
hot shared lock. Two merge strategies: an `Interlocked` counter for a single scalar, or a
`ConcurrentBag<T>` collecting per-chunk objects.

## When to use

- The work is CPU-bound and the items (or chunks) are independent - a parallel speedup is available.
- You are scanning or reducing a large array/collection (counting matches in a `byte[]`, summing,
  hashing blocks) and can express the merge as an associative combine.
- The input is large enough that the partition/scheduling overhead pays off. For tiny inputs a plain
  loop is faster.

## Key code

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Icm.Patterns.Concurrency
{
    public class ParallelAggregate
    {
        // Count occurrences of 'needle' in a large buffer using Parallel.For with a
        // thread-local subtotal, merged once per partition via Interlocked.
        public long CountByte(byte[] buffer, byte needle)
        {
            long total = 0;

            Parallel.For<long>(
                0,
                buffer.Length,
                delegate() { return 0L; },                  // localInit: per-thread subtotal
                delegate(int i, ParallelLoopState state, long local)
                {
                    if (buffer[i] == needle) local++;
                    return local;
                },
                delegate(long local)                         // localFinally: merge once per partition
                {
                    Interlocked.Add(ref total, local);
                });

            return total;
        }

        // Process chunks and collect a result object per chunk into a ConcurrentBag, then merge.
        public long SumOfChunkSums(int[] data, int chunkSize)
        {
            ConcurrentBag<long> partials = new ConcurrentBag<long>();

            List<int> starts = new List<int>();
            for (int start = 0; start < data.Length; start += chunkSize) starts.Add(start);

            Parallel.ForEach(starts, delegate(int start)
            {
                int end = Math.Min(start + chunkSize, data.Length);
                long sum = 0;
                for (int i = start; i < end; i++) sum += data[i];
                partials.Add(sum);
            });

            long total = 0;
            foreach (long p in partials) total += p;
            return total;
        }
    }
}
```

## Notes

- **Do not write to a shared variable inside the loop body without synchronization** - that is a data
  race. Use the `localInit`/`localFinally` overload so each partition keeps a private subtotal and
  merges once at the end (one `Interlocked.Add` per partition, not per element).
- **`Interlocked`** (`Add`, `Increment`, `CompareExchange`) is the cheapest way to combine a single
  scalar; **`ConcurrentBag<T>`** (or `ConcurrentQueue<T>`) is the lock-free place to collect per-chunk
  objects you merge afterward.
- **`ConcurrentBag<T>` is unordered.** If you need results in input order, key them (e.g. a
  `ConcurrentDictionary<int, T>` by chunk index) and reassemble.
- **PLINQ** (`source.AsParallel().Sum()`) is an alternative for simple reductions and is in-box; reach
  for `Parallel.For` when you need explicit chunking or thread-local accumulation.
- **NOT available in-box:** nothing extra is needed here - `Parallel`, `Interlocked`, and the
  concurrent collections are all in the in-box framework. (TPL Dataflow, a different parallel model,
  is the absent one.)
