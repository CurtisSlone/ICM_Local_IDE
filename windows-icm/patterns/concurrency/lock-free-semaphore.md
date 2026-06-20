<!--icm
{
  "id": "lock-free-semaphore",
  "title": "Lock-free semaphore (Interlocked compare-and-swap counter)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "Bound concurrent access with an atomic CAS loop instead of a blocking wait: do { old = _count; new = old - 1; if (new < 0) return false; } while (CompareExchange(ref _count, new, old) != old). TryTake never blocks (returns false when empty); the retry loop must recompute from a freshly re-read old value each pass.",
  "keywords": ["lock-free", "Interlocked", "CompareExchange", "CAS", "compare-and-swap", "semaphore", "non-blocking", "TryTake", "atomic counter", "SemaphoreFullException", "ABA", "livelock", "wait-free", "Interlocked.Increment", "Interlocked.Decrement", "concurrency"],
  "source": { "origin": "ported", "url": "local repo: AsyncSocketServers (Dem0n13)", "note": "distilled to C# 5 / in-box; compile-verified with csc_check" }
}
-->
# Lock-free semaphore

## Intent

Bound concurrent access to a resource with an atomic counter instead of a blocking wait. A
`SemaphoreSlim` parks the calling thread (or suspends the async operation) until a slot frees up; a
lock-free semaphore never parks. It holds the slot count in a plain `int` and changes it with an
atomic compare-and-swap (`Interlocked.CompareExchange`) retry loop. `TryTake` either claims a slot
and returns `true`, or sees no slot free and returns `false` immediately. The caller decides what to
do with the failure (spin, do other work, give up) instead of being blocked.

## When to use

- Very short critical sections or a simple shared counter under contention where you do NOT want to
  block a thread or suspend an operation.
- You are fine with `TryTake` returning `false` and the caller handling that (retry later, do other
  work, back off) rather than waiting for a slot.
- A non-AI hot path - an object pool handing out buffers, a fast-path admission counter - where the
  cost of a kernel wait would dominate the work being guarded.

When NOT to use:

- When you actually need to BLOCK and wait for a slot. A lock-free `TryTake` cannot wait; use
  `SemaphoreSlim` and the lock-based [[throttle-with-semaphore]] instead.
- For complex multi-variable invariants. CAS protects one field at a time. If correctness depends on
  several fields changing together, a plain `lock` is clearer and safer than trying to coordinate
  several CAS loops.
- When you just need an unbounded counter with no ceiling. Reach for `Interlocked.Increment` /
  `Interlocked.Decrement` directly; the CAS loop only earns its keep because of the `< 0` / `> max`
  bounds check.

## Key code

```csharp
using System;
using System.Threading;

namespace Icm.Patterns.Concurrency
{
    // A non-blocking counting semaphore built on Interlocked.CompareExchange.
    // TryTake never blocks: it returns false when no slot is free.
    public sealed class LockFreeSemaphore
    {
        private readonly int _maxCount;
        private int _currentCount;

        public LockFreeSemaphore(int initialCount)
            : this(initialCount, int.MaxValue)
        {
        }

        public LockFreeSemaphore(int initialCount, int maxCount)
        {
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "maxCount must be positive");
            if (initialCount < 0 || initialCount > maxCount)
                throw new ArgumentOutOfRangeException("initialCount", "initialCount must be in [0, maxCount]");

            _currentCount = initialCount;
            _maxCount = maxCount;
        }

        // Racy snapshot. Do NOT branch on it for correctness; another thread may
        // change it the instant after you read it. Use it for logging only.
        public int CurrentCount
        {
            get { return _currentCount; }
        }

        public int MaxCount
        {
            get { return _maxCount; }
        }

        // The CAS retry loop. Read the field into a local, compute the new value,
        // then swap ONLY if the field still holds the value we read. If another
        // thread changed it in between, CompareExchange does not equal oldValue
        // and we loop to retry with the freshly re-read value.
        public bool TryTake()
        {
            int oldValue, newValue;
            do
            {
                oldValue = _currentCount;
                newValue = oldValue - 1;
                if (newValue < 0)
                    return false; // no slot free; do not block, just fail
            }
            while (Interlocked.CompareExchange(ref _currentCount, newValue, oldValue) != oldValue);
            return true;
        }

        public void Release()
        {
            Release(1);
        }

        public void Release(int releaseCount)
        {
            if (releaseCount < 1)
                throw new ArgumentOutOfRangeException("releaseCount", "releaseCount must be at least 1");

            int oldValue, newValue;
            do
            {
                oldValue = _currentCount;
                newValue = oldValue + releaseCount;
                if (newValue > _maxCount)
                    throw new SemaphoreFullException();
            }
            while (Interlocked.CompareExchange(ref _currentCount, newValue, oldValue) != oldValue);
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}/{2}", typeof(LockFreeSemaphore).Name, _currentCount, _maxCount);
        }
    }
}
```

The centerpiece is the CAS retry loop in `TryTake`:

```csharp
int oldValue, newValue;
do
{
    oldValue = _currentCount;       // read the field into a local
    newValue = oldValue - 1;        // compute the intended new value
    if (newValue < 0) return false; // bounds check on the snapshot, not the field
}
while (Interlocked.CompareExchange(ref _currentCount, newValue, oldValue) != oldValue);
return true;
```

Why the loop: between reading `_currentCount` into `oldValue` and writing the decremented value,
another thread can change `_currentCount`. `Interlocked.CompareExchange(ref field, newValue, oldValue)`
writes `newValue` ONLY if `field` still equals `oldValue`, and it returns whatever `field` held at the
moment of the compare. If that return value is not `oldValue`, someone changed the field first, our
`newValue` was computed from a stale read, and we lost the race. So we loop: re-read the now-current
value into `oldValue`, recompute, and try again until our compare wins. Exactly one thread wins each
round; the losers retry. No lock is ever taken.

## Rules for use

- **Capture old, then CAS. Never modify the field directly.** Read `_currentCount` into a local
  (`oldValue`), compute `newValue` from that local, and let `CompareExchange` perform the write. A
  bare `_currentCount = _currentCount - 1` (or `_currentCount--`) is a read-modify-write that is not
  atomic and will drop updates under contention.
- **Recompute from the freshly re-read old value every iteration.** The first statement inside the
  loop must re-read `_currentCount` into `oldValue`. Computing `newValue` once before the loop and
  reusing it would keep retrying a swap based on a stale snapshot.
- **`TryTake` is non-blocking by design.** It returns `false` when the count would go below zero. It
  does not wait, spin internally, or sleep. If you need to wait for a slot, this is the wrong
  primitive; use the blocking [[throttle-with-semaphore]].
- **`Release` past `MaxCount` throws `SemaphoreFullException`.** Releasing more than was taken pushes
  the count above the ceiling, which is a bug (over-release). Match every successful `TryTake` with at
  most one `Release`, and release only on the path that actually took a slot.
- **`CurrentCount` is a racy snapshot.** Read it for logging or a rough gauge, never to branch on for
  correctness. The value can change the instant after you read it. The only safe admission test is the
  CAS in `TryTake` itself.
- **Lock-free is not wait-free.** No thread holds a lock, so no thread blocks others, but heavy
  contention makes the CAS lose and retry repeatedly. Many threads hammering the same counter can livelock-ish
  into wasted spins. Keep the guarded body tiny so slots free up fast and contention windows stay
  short.
- **The ABA problem does not bite this counter, but it bites pointer/reference CAS.** Here the field
  is a numeric count; even if it goes `5 -> 4 -> 5` between our read and our compare, our invariant
  still holds. The danger case is CAS on a pointer or object reference, where a value can return to a
  prior bit pattern while the underlying object has changed (e.g. a freed-and-reused node). For those,
  use a versioned/tagged reference or `Interlocked` on a wrapper - not this bare pattern.
- **Prefer plain `Interlocked.Increment` / `Interlocked.Decrement` when there is no upper-bound
  check.** If you only need an atomic counter with no `< 0` / `> max` gate, the single-call increment
  and decrement are simpler and faster than a hand-written CAS loop. The loop is justified only by the
  bounds check that those single calls cannot express.

## Notes

- `SemaphoreFullException` lives in `System.Threading` in the in-box framework; no extra reference is
  needed beyond `System.dll`.
- The two-arg constructor defaults `maxCount` to `int.MaxValue`, giving a counter that effectively
  only guards the lower bound (never goes below zero) while still allowing a finite ceiling when you
  pass one.
- `ToString` is a convenience for logging the `current/max` snapshot; it reads the racy `_currentCount`
  and is fine for diagnostics only. Drop it if you do not want it.
- This is the non-blocking counterpart to [[throttle-with-semaphore]] (the `SemaphoreSlim`/lock-based
  blocking throttle). A common consumer is [[object-pool]], which uses `TryTake` to admit a borrow on
  the fast path. See the in-box concurrency primitives in [[concurrency-inbox]].
