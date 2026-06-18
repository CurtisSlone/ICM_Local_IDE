<!--icm
{
  "id": "disposable-pattern",
  "title": "The IDisposable / Dispose pattern (deterministic cleanup)",
  "doc_type": "pattern",
  "summary": "How to release unmanaged/expensive resources deterministically in C#: implement IDisposable, the protected Dispose(bool) override, the finalizer-only-when-you-own-unmanaged rule, and `using`. C# 5 compatible.",
  "keywords": ["IDisposable", "Dispose", "using", "finalizer", "destructor", "GC.SuppressFinalize", "unmanaged resources", "SafeHandle", "deterministic cleanup"],
  "source": { "origin": "authored", "note": "distilled from the .NET 'Implement a Dispose method' guidance; signatures verbatim" }
}
-->
# The IDisposable / Dispose pattern

Use this when a type holds something that must be released promptly and deterministically: file
handles, sockets, native memory, OS handles, large buffers. The garbage collector frees *managed*
memory eventually; it does not promptly close *resources*. `IDisposable` gives callers a deterministic
release point, and `using` guarantees it runs even on exceptions.

## The minimal case (only managed disposables to release)

Most types only wrap other `IDisposable` fields. Then you do NOT need a finalizer.

```csharp
public class ReportWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private bool _disposed;

    public ReportWriter(string path) { _writer = new StreamWriter(path); }

    public void Dispose()
    {
        if (_disposed) return;
        _writer.Dispose();   // release the managed disposable we own
        _disposed = true;
    }
}
```

## The full pattern (you own an UNMANAGED resource directly)

Only when the type holds a raw native handle (an `IntPtr` from a P/Invoke call, etc.). The
`Dispose(bool)` override separates the two cases; the finalizer is the safety net if a caller forgets
`Dispose`.

```csharp
public class NativeBuffer : IDisposable
{
    private IntPtr _handle;     // an unmanaged resource we own
    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);   // we cleaned up; the finalizer need not run
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // release other managed IDisposable fields here
        }
        // release the unmanaged resource (runs in both the Dispose and finalizer paths)
        if (_handle != IntPtr.Zero) { /* e.g. Marshal.FreeHGlobal(_handle); */ _handle = IntPtr.Zero; }
        _disposed = true;
    }

    ~NativeBuffer() { Dispose(false); }   // finalizer: only the unmanaged branch runs
}
```

## Rules

- **Prefer `SafeHandle`** (e.g. `SafeFileHandle`) over a raw `IntPtr` + finalizer. A `SafeHandle`
  field makes your type the "only managed disposables" case above - no finalizer needed.
- **Write a finalizer ONLY if you directly own an unmanaged resource.** A finalizer that just frees
  managed objects is wrong - it adds GC cost and runs on a separate thread.
- **`Dispose` must be idempotent** - safe to call twice (the `_disposed` guard).
- **In `Dispose(false)` (the finalizer path) touch no other managed objects** - they may already be
  collected. Only release the unmanaged handle.
- **Call `GC.SuppressFinalize(this)`** in the public `Dispose` so a disposed object is not finalized.
- **Callers use `using`** so cleanup runs on every path:
  ```csharp
  using (var w = new ReportWriter("out.txt")) { /* ... */ }   // w.Dispose() runs here, even on throw
  ```

## C# 5 notes

- The C# 8 `using` *declaration* (`using var w = ...;`) is NOT available; use the `using (...) { }`
  *statement* form shown above. (See the C# 5 subset reference.)
- `IAsyncDisposable` / `await using` do not exist here either.
