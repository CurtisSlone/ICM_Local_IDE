<!--icm
{
  "id": "optimistic-concurrency-control",
  "title": "Optimistic concurrency control (version token, detect-and-retry)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "Avoid locking by giving each record a version token; a write supplies the version it read and is applied only if the stored version still matches, else the write is rejected as a conflict and the caller re-reads, re-applies, and retries. When to use OCC vs pessimistic locking, the version-token variants (rowversion / incrementing int / ETag), the rules, and a C# 5 in-memory compare-and-swap implementation.",
  "keywords": ["optimistic concurrency", "occ", "concurrency control", "version", "rowversion", "timestamp", "ETag", "If-Match", "lost update", "compare and swap", "conflict", "retry", "DbUpdateConcurrencyException", "pessimistic locking", "409 conflict"],
  "source": { "origin": "ported", "url": "local repo: optimistic-concurrency-control (ASP.NET Core ETag/If-Match demo)", "note": "distilled from an ASP.NET Core + EF-style web API into a C# 5 in-memory compare-and-swap; pattern + rules, compile-verified with csc_check" }
}
-->
# Optimistic concurrency control

## Intent

Let many readers and writers proceed without locking, and catch the rare conflicting write at commit
time instead of preventing it up front. Each record carries a **version token**. A writer reads the
record (and its version), prepares a change, and submits the change **together with the version it
read**. The store applies the write **only if the stored version still equals that version**; if some
other writer committed in between, the versions differ and the write is **rejected as a conflict**.
The loser re-reads the now-current data, re-applies its change, and retries.

This prevents the **lost update** bug (two writers both read v1, both write, the second silently
clobbers the first) without holding a lock across the read-think-write gap.

## When to use OCC vs pessimistic locking

Use **optimistic** (this pattern) when:
- Conflicts are **rare** (low contention, read-heavy data).
- There is a long or unbounded gap between read and write - a user editing a form, a stateless HTTP
  request/response - where holding a lock is impractical or impossible.
- The work spans processes/machines and there is no shared lock to take.

Use **pessimistic** (a lock / `SemaphoreSlim` / DB row lock; see [[throttle-with-semaphore]]) when:
- Contention is **high** (many writers hitting the same row) - OCC then degrades into a retry storm.
- A conflict is expensive to redo, or the critical section is short and in-process.

OCC trades blocking for occasional **retry**; it detects conflicts, it does not prevent them.

## Version-token variants

- **Database rowversion / EF `[Timestamp]`** - an opaque `byte[]` the DB bumps on every update; EF puts
  it in the `WHERE` clause and throws `DbUpdateConcurrencyException` when zero rows match. The standard
  choice with a relational store.
- **An incrementing `int Version`** - the write is `UPDATE ... SET ..., Version = Version + 1 WHERE Id
  = @id AND Version = @old`; zero rows affected means a conflict. Simple and explicit.
- **A content hash / ETag (over HTTP)** - `GET` returns an `ETag` header (a hash of the resource); the
  client sends it back as `If-Match` on `PUT`; the server compares it to the current resource's hash
  and returns **412 Precondition Failed / 409 Conflict** on mismatch. Works without a version column
  but hashes the whole object on every request.

## Rules for use

- **Never write without checking the token.** A blind write is exactly the lost update OCC exists to
  stop.
- **The compare-and-swap must be atomic at the store.** A database does this with a conditional
  `UPDATE ... WHERE Version = @old` (one statement). An in-memory store must hold a lock around
  "check version, then swap" - otherwise two threads can both pass the check (see the `lock` below).
  So OCC across processes still relies on the DB's atomic conditional update.
- **On conflict, surface it - never silently overwrite.** Return a conflict (409/412, or
  `DbUpdateConcurrencyException`); do not retry by force-writing.
- **The caller handles the conflict:** re-read fresh data, re-apply the change to it, and retry.
  **Bound the retries** (e.g. 3) with a little backoff; after that, give up and surface to the user.
- **Bump the version on every successful write**, so the next stale writer is detected.
- **Prefer a dedicated version column over hashing the whole object** when you control the schema -
  cheaper and unambiguous.
- Wrong tool under heavy write contention - the retries can dominate; switch to pessimistic locking.

## C# 5 in-memory implementation

The store-side compare-and-swap (held atomic by a `lock`) and the caller-side retry loop:

```csharp
using System;
using System.Collections.Generic;

namespace Icm.Patterns.Concurrency
{
    // A record plus its version token. The version changes on every successful write.
    public class Versioned<T>
    {
        public int Version { get; set; }
        public T Value { get; set; }
    }

    // In-memory optimistic store: a write must present the version it read and is applied only if the
    // stored version still matches. The compare-and-swap is atomic because it runs under a lock.
    public class OptimisticStore<T>
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Guid, Versioned<T>> _data = new Dictionary<Guid, Versioned<T>>();

        public Guid Add(T value)
        {
            lock (_gate)
            {
                Guid id = Guid.NewGuid();
                Versioned<T> v = new Versioned<T>();
                v.Version = 1;
                v.Value = value;
                _data[id] = v;
                return id;
            }
        }

        // Return a COPY so the caller cannot mutate the stored version behind the store's back.
        public Versioned<T> Get(Guid id)
        {
            lock (_gate)
            {
                Versioned<T> current;
                if (!_data.TryGetValue(id, out current)) return null;
                Versioned<T> copy = new Versioned<T>();
                copy.Version = current.Version;
                copy.Value = current.Value;
                return copy;
            }
        }

        // Apply only if expectedVersion still matches. true = applied (version bumped); false = conflict.
        public bool TryUpdate(Guid id, int expectedVersion, T newValue)
        {
            lock (_gate)                 // check-then-swap must be atomic
            {
                Versioned<T> current;
                if (!_data.TryGetValue(id, out current)) return false;
                if (current.Version != expectedVersion) return false;   // someone wrote first -> conflict
                current.Version = current.Version + 1;
                current.Value = newValue;
                return true;
            }
        }
    }

    public static class OptimisticUpdate
    {
        // Caller-side conflict handling: read, apply change, try, and on conflict re-read and retry,
        // bounded. Returns false if the record is gone or all attempts conflicted.
        public static bool WithRetry<T>(OptimisticStore<T> store, Guid id, Func<T, T> change, int maxAttempts)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Versioned<T> snapshot = store.Get(id);
                if (snapshot == null) return false;
                T updated = change(snapshot.Value);
                if (store.TryUpdate(id, snapshot.Version, updated)) return true;
                // conflict: the loop re-reads the now-newer version and re-applies the change
            }
            return false;   // gave up after maxAttempts -> surface to the caller
        }
    }
}
```

## Notes

- The `lock` here is the in-process stand-in for the database's atomic conditional `UPDATE`. With a
  real DB you would not lock in your code - you would let the `WHERE Version = @old` do the atomic
  check and treat "zero rows affected" (or `DbUpdateConcurrencyException`) as the conflict signal.
- Returning a copy from `Get` matters: if you hand out the live object, the caller can mutate
  `Value`/`Version` and defeat the check.
- For the HTTP/ETag variant, the same logic moves to headers: `ETag` out on read, `If-Match` in on
  write, `412`/`409` on mismatch - no version column needed, at the cost of hashing the whole object.

Related: [[throttle-with-semaphore]] (pessimistic side and the atomic-section lock), [[concurrency-inbox]].
