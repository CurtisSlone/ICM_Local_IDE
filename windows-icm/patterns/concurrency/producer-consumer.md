<!--icm
{
  "id": "producer-consumer",
  "title": "Producer/consumer with BlockingCollection<T> (the in-box channel)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "Hand work from producers to consumers with BlockingCollection<T> - the in-box stand-in for Channel<T> (which is NOT available) - using Add/CompleteAdding on the producer side and GetConsumingEnumerable on the consumer side.",
  "keywords": ["producer", "consumer", "BlockingCollection", "GetConsumingEnumerable", "CompleteAdding", "bounded queue", "Channel", "backpressure", "ConcurrentQueue", "pipeline"],
  "source": { "origin": "authored", "note": "C# 5 / in-box .NET Framework 4.8; compiled with the in-box csc to verify" }
}
-->
# Producer/consumer with BlockingCollection&lt;T&gt;

## Intent

Move items from one or more producers to one or more consumers through a thread-safe queue, with
blocking (and optional bounded capacity for backpressure) handled for you. `BlockingCollection<T>` is
the in-box replacement for `Channel<T>`.

## When to use

- You have a stage that generates items and a stage that processes them, and you want them to run
  concurrently without hand-rolling locks and condition variables.
- You want bounded backpressure: give the collection a capacity so a fast producer blocks on `Add`
  when consumers fall behind.
- A consumer should drain until the producer signals "no more items" - that is exactly what
  `CompleteAdding` plus `GetConsumingEnumerable` give you.

## Key code

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Icm.Patterns.Concurrency
{
    public class ProducerConsumer
    {
        // Produce 'count' items, consume them on a separate task, return the running total.
        public int Run(int count)
        {
            // Capacity bounds the queue: Add blocks when full (backpressure).
            BlockingCollection<int> queue = new BlockingCollection<int>(boundedCapacity: 16);

            // Consumer: GetConsumingEnumerable blocks for items and ends when CompleteAdding is called.
            Task<int> consumer = Task.Run(delegate
            {
                int total = 0;
                foreach (int item in queue.GetConsumingEnumerable())
                {
                    total += item;
                }
                return total;
            });

            // Producer: add items, then signal that no more will arrive.
            for (int i = 1; i <= count; i++)
            {
                queue.Add(i);
            }
            queue.CompleteAdding();

            // Wait for the consumer to drain and finish. (Console boundary: block once.)
            return consumer.GetAwaiter().GetResult();
        }
    }
}
```

## Notes

- **`CompleteAdding` is mandatory** to end the consumer loop. Without it, `GetConsumingEnumerable`
  blocks forever waiting for items that never come.
- **`GetConsumingEnumerable()` removes items as it yields them** - it is not a snapshot. Multiple
  consumers can each take from it; the collection hands each item to exactly one consumer.
- **Bounded capacity gives backpressure.** With a capacity, `Add` blocks while the queue is full, so a
  fast producer cannot run memory away from a slow consumer. Omit the capacity for an unbounded queue.
- **Dispose it** (`using` or `Dispose`) when done if it outlives the method; here it is local and
  short-lived. The default backing store is a `ConcurrentQueue<T>` (FIFO).
- **NOT available in-box:** `System.Threading.Channels` (`Channel<T>`, `ChannelReader`/`ChannelWriter`)
  ships only as a NuGet package; this environment has no NuGet. TPL Dataflow
  (`System.Threading.Tasks.Dataflow`) is likewise absent. `BlockingCollection<T>` covers the common
  bounded-queue and pipeline cases without either.
