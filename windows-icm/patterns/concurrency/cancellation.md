<!--icm
{
  "id": "cancellation",
  "title": "Cooperative cancellation (CancellationToken / CancellationTokenSource)",
  "doc_type": "pattern",
  "group": "concurrency",
  "summary": "Cooperatively cancel async and loop work with CancellationToken/CancellationTokenSource: check the token in loops, pass it into Task.Run and Task.Delay, and use new CancellationTokenSource(timeoutMs) for timeouts.",
  "keywords": ["CancellationToken", "CancellationTokenSource", "cooperative cancellation", "ThrowIfCancellationRequested", "IsCancellationRequested", "timeout", "Cancel", "OperationCanceledException", "Task.Run", "Task.Delay"],
  "source": { "origin": "authored", "note": "C# 5 / in-box .NET Framework 4.8; compiled with the in-box csc to verify" }
}
-->
# Cooperative cancellation

## Intent

Let a caller ask running work to stop early - on a timeout, a user pressing Cancel, or a shutdown -
using the standard `CancellationToken`. Cancellation is cooperative: the worker periodically observes
the token and bails out; nothing forcibly aborts a thread.

## When to use

- A long loop, an async operation, or a `Task.Run` body that a caller may want to abandon.
- You want a timeout: construct `new CancellationTokenSource(timeoutMs)` and the token trips itself.
- You are calling token-aware APIs (`Task.Delay`, IO with a token overload, `WaitAsync`): pass the
  token so the wait itself unblocks on cancel.

## Key code

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Icm.Patterns.Concurrency
{
    public class Cancellation
    {
        // A loop that checks the token cooperatively and throws OperationCanceledException on cancel.
        public Task<long> SumUpToAsync(int n, CancellationToken token)
        {
            return Task.Run(delegate
            {
                long total = 0;
                for (int i = 1; i <= n; i++)
                {
                    token.ThrowIfCancellationRequested();   // cooperative check
                    total += i;
                }
                return total;
            }, token);
        }

        // An async wait that the token can interrupt: pass the token into Task.Delay.
        public async Task<bool> WaitWithTimeoutAsync(int waitMs, int timeoutMs)
        {
            // The source cancels itself after timeoutMs.
            CancellationTokenSource cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await Task.Delay(waitMs, cts.Token).ConfigureAwait(false);
                return true;   // completed before the timeout
            }
            catch (OperationCanceledException)
            {
                return false;  // the timeout fired first
            }
            finally
            {
                cts.Dispose();
            }
        }

        // Caller-driven cancel: hand out a token, cancel it when you decide to stop.
        public bool CancelImmediately(int n)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            Task<long> work = SumUpToAsync(n, cts.Token);
            cts.Cancel();
            try
            {
                work.GetAwaiter().GetResult();
                return false;  // finished before observing the cancel
            }
            catch (OperationCanceledException)
            {
                return true;   // observed the cancel
            }
            finally
            {
                cts.Dispose();
            }
        }
    }
}
```

## Notes

- **Cancellation is cooperative.** The worker must check the token (`ThrowIfCancellationRequested` or
  `IsCancellationRequested`); a token cannot force-stop tight code that never looks at it.
- **`ThrowIfCancellationRequested` throws `OperationCanceledException`** - the convention for "I was
  cancelled". Passing the token as the second arg to `Task.Run` lets the task surface as `Canceled`
  rather than `Faulted`.
- **Timeouts:** `new CancellationTokenSource(timeoutMs)` (or `cts.CancelAfter(ms)`) trips the token
  automatically. Token-aware waits like `Task.Delay(ms, token)` then end promptly with
  `OperationCanceledException` instead of running to completion.
- **Dispose the `CancellationTokenSource`** when you own it (it can hold a timer). Pass the read-only
  `CancellationToken`, not the source, to workers - only the owner should cancel.
- **NOT available in-box:** nothing extra - `CancellationToken`, `CancellationTokenSource`, the
  timeout constructor, and the token overloads of `Task.Delay`/`Task.Run` are all in the in-box
  framework.
