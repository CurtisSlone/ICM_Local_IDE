<!--icm
{
  "id": "object-pool",
  "title": "Object pool (reuse expensive objects to cut allocation)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "Recycle expensive-to-allocate objects (large byte[] buffers, SocketAsyncEventArgs) with a thread-safe Pool<T>: Rent on demand (create on miss), Return when done, reset between uses, bounded by a max-size cap; includes the IDisposable PooledItem/using idiom and the reset/bound/no-double-return rules.",
  "keywords": ["object pool", "pooling", "reuse", "ConcurrentStack", "GC pressure", "buffer reuse", "rent return", "SocketAsyncEventArgs", "max capacity", "reset on return", "double return", "use after return", "IDisposable handle", "using idiom"],
  "source": { "origin": "ported", "url": "local repo: AsyncSocketServers (Dem0n13)", "note": "distilled to a simpler C# 5 / in-box pool; compile-verified with csc_check" }
}
-->
# Object pool

## Intent

Recycle objects that are expensive to allocate so a hot path reuses instances instead of churning the
garbage collector. The canonical cases are large `byte[]` buffers, `SocketAsyncEventArgs`, and
connection-like handles: things that cost real time or memory to build and are needed over and over.
You rent an instance on demand (create one on a miss), do your work, return it when done, and reset its
state between uses so the next renter gets a clean object.

## When to use

- A hot path allocates many short-lived, expensive-to-create objects: a per-request buffer, a
  per-connection `SocketAsyncEventArgs`, a parser or serializer that is costly to spin up.
- You have measured GC pressure (high gen-0 collection rate, allocation spikes) traced to that churn.
  Measure first; pooling is an optimization, not a default.

When NOT to use:

- The objects are cheap to allocate. Pooling adds real complexity and a class of nasty bugs
  (use-after-return, double-return, dirty state) and buys nothing for a small object the GC handles for
  free.
- The lifetime is unclear or shared. If you cannot say exactly when an object is done being used, you
  cannot safely return it, and a use-after-return corruption is hard to find.
- The BCL already pools for you. Prefer the in-box facility when one exists rather than a hand-rolled
  pool that you now have to keep correct.

## Key code

A clean generic pool: a `ConcurrentStack<T>` for storage, a `Func<T>` factory for misses, an optional
`Action<T>` reset applied on return, and a max-size cap. `Rent()` reuses or creates; `Return(T)` resets
and stores when there is room, otherwise drops the instance.

```csharp
using System;
using System.Collections.Concurrent;

namespace Icm.Patterns.Concurrency
{
    // A simple, thread-safe object pool. Rent reuses a stored instance or makes one on a miss;
    // Return resets the instance and stores it, dropping it once the pool is full.
    public class Pool<T> where T : class
    {
        private readonly ConcurrentStack<T> _store;
        private readonly Func<T> _factory;   // makes a fresh T on a miss
        private readonly Action<T> _reset;   // clears state before reuse (may be null)
        private readonly int _maxSize;       // cap on retained instances

        public Pool(Func<T> factory, Action<T> reset, int maxSize)
        {
            if (factory == null)
                throw new ArgumentNullException("factory");
            if (maxSize < 1)
                throw new ArgumentOutOfRangeException("maxSize", "maxSize must be at least 1");

            _store = new ConcurrentStack<T>();
            _factory = factory;
            _reset = reset;
            _maxSize = maxSize;
        }

        // Number of instances currently sitting idle in the pool.
        public int CountAvailable
        {
            get { return _store.Count; }
        }

        // Take an instance: reuse one if available, otherwise build a new one.
        public T Rent()
        {
            T item;
            if (_store.TryPop(out item))
                return item;
            return _factory();
        }

        // Give an instance back. Reset it, then keep it only if there is room.
        // Over the cap we let it go and the GC reclaims it. NEVER return the same item twice.
        public void Return(T item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (_reset != null)
                _reset(item);

            if (_store.Count < _maxSize)
                _store.Push(item);
            // else: pool is full, drop the instance (do not store, do not throw).
        }
    }

    // The using idiom: a small disposable handle that returns its item to the pool on Dispose.
    // It turns "remember to call Return" into a using block the compiler enforces.
    public struct PooledItem<T> : IDisposable where T : class
    {
        private readonly Pool<T> _pool;
        private T _item;

        internal PooledItem(Pool<T> pool, T item)
        {
            _pool = pool;
            _item = item;
        }

        public T Value
        {
            get { return _item; }
        }

        public void Dispose()
        {
            if (_item != null)
            {
                _pool.Return(_item);
                _item = null; // guard against a double return on a second Dispose
            }
        }
    }

    // Extension that hands back a using-friendly handle instead of a raw instance.
    public static class PoolExtensions
    {
        public static PooledItem<T> RentScoped<T>(this Pool<T> pool) where T : class
        {
            if (pool == null)
                throw new ArgumentNullException("pool");
            return new PooledItem<T>(pool, pool.Rent());
        }
    }

    // Example: pool 64 KB buffers for a hot read path. Reset is a no-op here because the
    // consumer overwrites the bytes it reads; if you ever hand the buffer to untrusted code
    // or read fewer bytes than you compare, clear it on return instead.
    public static class BufferPoolExample
    {
        private static readonly Pool<byte[]> _buffers =
            new Pool<byte[]>(delegate { return new byte[64 * 1024]; }, null, 256);

        public static int UseOnce()
        {
            using (PooledItem<byte[]> handle = _buffers.RentScoped())
            {
                byte[] buffer = handle.Value;
                buffer[0] = 1;
                return buffer.Length;
                // Dispose returns the buffer to the pool here.
            }
        }

        // The explicit Rent/Return form, for when a using scope does not fit the lifetime.
        public static int UseManual()
        {
            byte[] buffer = _buffers.Rent();
            try
            {
                buffer[0] = 2;
                return buffer.Length;
            }
            finally
            {
                _buffers.Return(buffer); // return on every path; never touch buffer after this
            }
        }
    }
}
```

## Rules for use

- **Reset state on return (or on rent).** A reused object must never be dirty. Clear counters, buffers,
  flags, and any field that carried the previous user's data, or you leak one request's data into the
  next and produce bugs that only show up under reuse. The `Action<T> reset` hook is where this lives;
  if your object cannot be cleaned cheaply, it is a poor candidate for pooling.
- **Bound the pool.** Without a max capacity a pool grows without limit and becomes a memory leak that
  looks like a cache. Decide what happens on a return when full. The pool above drops the instance
  (create-anyway-and-drop-on-return), which keeps `Rent` cheap and never blocks; the alternatives are to
  block until a slot frees or to fail. Pick one deliberately and document it.
- **Never use an object after returning it, and never return the same object twice.** Use-after-return
  hands you an object another renter now owns; a double-return puts the same instance in the pool twice,
  so two renters get the same object and corrupt each other. The `using` / `PooledItem<T>` idiom exists
  to make single-return the easy path. The handle above nulls its reference on `Dispose` so an
  accidental second `Dispose` is a no-op.
- **Make `Rent` and `Return` thread-safe.** A pool is shared concurrency state. Use a lock-free
  concurrent collection (`ConcurrentStack<T>` or `ConcurrentBag<T>`) or guard a plain stack with a
  `lock`. The example uses `ConcurrentStack<T>`, which gives LIFO reuse (the most recently returned
  instance is likely still hot in cache).
- **Pool instances, not garbage.** Clear references the pooled object holds before storing it, or the
  pool pins whatever graph those references reach. A pooled buffer wrapper that still points at a large
  result keeps that result alive for the lifetime of the pool. Null out such fields in the reset hook.

## Notes

- **The cap-on-return policy is a real design choice.** Dropping on a full return (as here) is the
  simplest and keeps the hot path non-blocking, at the cost of re-allocating under a burst that exceeds
  the cap. Blocking on rent (wait for a free instance) bounds total allocation hard but can stall a hot
  path; failing fast surfaces exhaustion to the caller. Match the policy to whether allocation or
  latency is the thing you are protecting.
- **The richer slot-tracking variant.** The original `Pool<T>` this is distilled from goes further:
  every object is wrapped in a `PoolSlot<T>` handle that records whether it is currently in the pool, so
  a double-return throws instead of silently corrupting, and total allocations are capped with a
  lock-free semaphore (see [[lock-free-semaphore]]) rather than only the retained-count cap. It also
  supports a blocking `WaitPop` on exhaustion, trimming the pool down to a target size, and a `WaitAll`
  that blocks until every rented object is back. That status tracking is the safer choice when callers
  are not disciplined about single-return; the version above trades it for simplicity and leans on the
  `using` idiom instead.
- **`SocketAsyncEventArgs` is the motivating case.** An async socket server allocates one
  `SocketAsyncEventArgs` (and often a receive buffer) per in-flight operation; under load that is a
  storm of short-lived allocations. Pooling the SAEA and its buffer is the standard fix and is exactly
  what the source repo does. See [[async-socket-server]] for the consumer.
- **In-box only.** `ConcurrentStack<T>` and `ConcurrentBag<T>` are in the in-box framework; no NuGet is
  needed. (.NET Core later added a general-purpose `ObjectPool<T>` and `ArrayPool<T>`; neither is
  available here, which is why this hand-rolled pool exists.)

See also [[lock-free-semaphore]] (used by the original to cap total allocations), [[async-socket-server]]
(the consumer pooling `SocketAsyncEventArgs`), [[disposable-pattern]] (the `using`/`Dispose` return
handle), and the in-box concurrency primitives in [[concurrency-inbox]].
