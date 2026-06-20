<!--icm
{
  "id": "async-socket-server",
  "title": "Async TCP server with SocketAsyncEventArgs",
  "doc_type": "pattern",
  "group": "networking",
  "summary": "Handle many concurrent TCP clients with low GC by reusing SocketAsyncEventArgs + buffers; the critical rule is that AcceptAsync/ReceiveAsync/SendAsync return false on synchronous completion (process inline now) and true on async (Completed fires later) - handle BOTH at every call site.",
  "keywords": ["SocketAsyncEventArgs", "async socket", "TCP server", "AcceptAsync", "ReceiveAsync", "SendAsync", "Completed", "LastOperation", "SocketAsyncOperation", "high throughput", "SetBuffer", "BytesTransferred", "SocketError", "echo server", "accept loop", "low allocation"],
  "source": { "origin": "ported", "url": "local repo: AsyncSocketServers (Dem0n13)", "note": "distilled to a C# 5 / in-box TCP echo server, NLog removed; compile-verified with csc_check" }
}
-->
# Async TCP server with SocketAsyncEventArgs

## Intent

Serve many concurrent TCP clients with as little per-connection allocation as possible. Instead of
the older `BeginAccept`/`EndAccept` callback pairs (which allocate an `IAsyncResult` per operation) or
a thread-per-client model (which burns a stack per connection), use `SocketAsyncEventArgs` (SAEA): a
reusable operation object that carries its own buffer. You hand the same SAEA back to the socket for
each accept, receive, and send, so a busy server settles into steady state with near-zero garbage.

## When to use

- A server that must scale to many simultaneous connections with low GC overhead, where allocation
  churn from per-operation callback objects or per-client threads would hurt.
- You are comfortable driving an explicit accept/receive/send state machine.

When NOT to use: a handful of connections, or a simple request/response service. For those a
`TcpListener` + `NetworkStream` with async/await (`listener.AcceptTcpClientAsync()`,
`stream.ReadAsync(...)`, `stream.WriteAsync(...)`) is far simpler and almost always enough. SAEA buys
you scale at the cost of a hand-written state machine; do not pay that cost without the scale to
justify it.

## The critical rule (handle sync AND async completion)

`socket.AcceptAsync(args)`, `socket.ReceiveAsync(args)`, and `socket.SendAsync(args)` each return a
`bool`:

- They return **false** when the operation completed **synchronously** (the data was already
  available, the send buffer had room). The `Completed` event will NOT fire. You must process the
  result **inline, right now**.
- They return **true** when the operation will complete **asynchronously**. The SAEA's `Completed`
  event will fire later on an I/O thread, and your handler processes the result there.

So every async socket call site must handle both paths:

```csharp
if (!socket.ReceiveAsync(args))
{
    ProcessReceive(args); // completed synchronously - handle it inline
}
// else: Completed will fire and the handler calls ProcessReceive
```

Forgetting the synchronous (false) path is the number one SAEA bug. It does not throw; the connection
just silently stalls, because you waited for a `Completed` event that already happened and never will.

## Key code

A self-contained C# 5 echo server: a listen socket, an accept loop that re-arms itself, one
`Completed` handler per connection that dispatches on `LastOperation`, and a receive handler that
echoes the bytes back. It creates one SAEA per connection for clarity; see Rules and Notes for
pooling them.

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Icm.Patterns.Networking
{
    // A compact TCP echo server built on SocketAsyncEventArgs (SAEA).
    // It accepts many clients, receives bytes, and echoes them straight back.
    // One SAEA is created per connection here for clarity; in production pool them
    // (see the object-pool pattern) to keep per-connection allocation near zero.
    public class EchoServer
    {
        private const int MaxBacklog = 100;

        private readonly IPEndPoint _endPoint;
        private readonly int _bufferSize;

        private Socket _listenSocket;
        private SocketAsyncEventArgs _acceptArgs;
        private int _stopped; // 0 = running, 1 = stopping (acts as the stop flag)

        public EchoServer(IPAddress ip, int port, int bufferSize)
        {
            _endPoint = new IPEndPoint(ip, port);
            _bufferSize = bufferSize;
        }

        // ---- lifecycle -------------------------------------------------------

        public void Start()
        {
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(_endPoint);
            _listenSocket.Listen(MaxBacklog);

            // One SAEA drives the accept loop; its Completed fires when an accept
            // finishes asynchronously.
            _acceptArgs = new SocketAsyncEventArgs();
            _acceptArgs.Completed += OnAcceptCompleted;

            Interlocked.Exchange(ref _stopped, 0);
            StartAccepting();
        }

        public void Stop()
        {
            Interlocked.Exchange(ref _stopped, 1);
            if (_listenSocket != null)
            {
                // Closing the listener aborts the pending accept (SocketError.OperationAborted).
                _listenSocket.Close();
            }
        }

        // ---- accept loop -----------------------------------------------------

        private void StartAccepting()
        {
            if (Interlocked.CompareExchange(ref _stopped, 0, 0) == 1) return;

            // Reuse the same accept SAEA each time round the loop.
            _acceptArgs.AcceptSocket = null;

            // THE critical rule: AcceptAsync returns false when it completed
            // SYNCHRONOUSLY. The Completed event will NOT fire, so we must process
            // the result inline right now. It returns true when the operation will
            // complete asynchronously and Completed will fire later.
            if (!_listenSocket.AcceptAsync(_acceptArgs))
            {
                ProcessAccept(_acceptArgs);
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs args)
        {
            ProcessAccept(args);
        }

        private void ProcessAccept(SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                Socket client = args.AcceptSocket;

                // One SAEA per in-flight operation. Build a per-connection SAEA with
                // its own reusable buffer and bind it to the accepted socket.
                SocketAsyncEventArgs io = new SocketAsyncEventArgs();
                io.SetBuffer(new byte[_bufferSize], 0, _bufferSize);
                io.AcceptSocket = client;
                io.Completed += OnIoCompleted;

                StartReceiving(io);
            }
            else if (args.SocketError == SocketError.OperationAborted)
            {
                // Listener was closed by Stop(); do not re-arm.
                return;
            }

            // Re-arm: keep accepting the next connection regardless of this one.
            StartAccepting();
        }

        // ---- receive / send --------------------------------------------------

        private void StartReceiving(SocketAsyncEventArgs io)
        {
            // Same false/true rule on every async socket call.
            if (!io.AcceptSocket.ReceiveAsync(io))
            {
                ProcessReceive(io);
            }
        }

        // Single Completed handler for the connection; dispatch on the last operation.
        private void OnIoCompleted(object sender, SocketAsyncEventArgs io)
        {
            switch (io.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(io);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(io);
                    break;
                default:
                    CloseConnection(io);
                    break;
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs io)
        {
            // Always check SocketError on completion before trusting the buffer.
            if (io.SocketError != SocketError.Success)
            {
                CloseConnection(io);
                return;
            }

            // BytesTransferred == 0 on a receive means the peer closed gracefully.
            if (io.BytesTransferred == 0)
            {
                CloseConnection(io);
                return;
            }

            // Echo: send back exactly the bytes we just received.
            io.SetBuffer(io.Offset, io.BytesTransferred);

            if (!io.AcceptSocket.SendAsync(io))
            {
                ProcessSend(io);
            }
        }

        private void ProcessSend(SocketAsyncEventArgs io)
        {
            if (io.SocketError != SocketError.Success)
            {
                CloseConnection(io);
                return;
            }

            // Reset the buffer window to its full size and wait for more bytes.
            io.SetBuffer(0, _bufferSize);
            StartReceiving(io);
        }

        // ---- teardown --------------------------------------------------------

        private void CloseConnection(SocketAsyncEventArgs io)
        {
            Socket client = io.AcceptSocket;
            if (client != null)
            {
                try
                {
                    client.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                    // Peer may already be gone; closing anyway is fine.
                }
                client.Close();
            }

            // With a pool you would return io here instead of disposing it.
            io.AcceptSocket = null;
            io.Completed -= OnIoCompleted;
            io.Dispose();
        }
    }
}
```

## Rules for use

- **Handle sync AND async completion at EVERY async socket call.** `AcceptAsync`, `ReceiveAsync`, and
  `SendAsync` all return `false` for synchronous completion - process the result inline; `true` means
  `Completed` will fire. Missing the `false` path silently stalls the connection. This is the rule
  that matters most.
- **One SAEA per in-flight operation.** A single SAEA cannot drive two overlapping operations. Here
  each connection owns one SAEA and runs receive then send strictly in sequence; if you ever do
  full-duplex (read and write at once) you need a separate SAEA per direction.
- **Preallocate and reuse the buffer via `SetBuffer`.** Allocate the byte array once per SAEA, not per
  operation. Use the `SetBuffer(offset, count)` overload to re-window the same array between receive
  and send (echo above). Do not allocate a fresh buffer on each receive.
- **Pool the SAEA and its buffer.** This doc creates one SAEA per accept for readability; in a real
  high-throughput server, take a SAEA from a pool on accept and return it to the pool on teardown so
  the steady state allocates nothing. See [[object-pool]]. A common refinement is one big pinned
  buffer carved into per-SAEA slices to reduce GC pinning pressure.
- **Always check `SocketError` on completion.** Before trusting `BytesTransferred` or the buffer,
  confirm `args.SocketError == SocketError.Success`. On anything else, tear the connection down.
- **`BytesTransferred == 0` on a receive means the peer closed.** Treat it as a graceful disconnect
  and tear down - do not loop calling receive on a closed socket.
- **On disconnect or error: Shutdown, then Close, then return the SAEA.** `Shutdown(SocketShutdown.Both)`
  can throw `SocketException` if the peer is already gone - catch and ignore it, then `Close()`. With a
  pool, return the SAEA to the pool instead of disposing it.
- **Use a stop flag (or `CancellationToken`) to end the accept loop.** Check it at the top of the
  accept loop and stop re-arming. Closing the listen socket also surfaces as
  `SocketError.OperationAborted` on the pending accept - treat that as "stop", not an error to log.

## Notes

- **Why SAEA over Begin/End or threads.** `BeginReceive`/`EndReceive` allocate an `IAsyncResult` (and
  usually a closure) per operation; a busy server generates constant garbage. Thread-per-client costs
  about a megabyte of stack per connection and does not scale to thousands. SAEA reuses one operation
  object plus one buffer per connection, so it hits a no-allocation steady state.
- **`SetBuffer` has two overloads.** `SetBuffer(byte[] buffer, int offset, int count)` assigns the
  underlying array (call once at setup). `SetBuffer(int offset, int count)` only re-windows the
  already-assigned array (call to point at the bytes to send, then to reset for the next receive).
- **Reentrancy and the re-arm loop.** When an operation completes synchronously you process it on the
  current thread, which can call the next operation, which can also complete synchronously. A long run
  of synchronous completions can deepen the stack. High-scale implementations guard against this; for
  most workloads the OS interleaves enough async completions that it is not a problem.
- **This is a stream, not a message protocol.** TCP gives you a byte stream with no message
  boundaries; one `ReceiveAsync` may return a partial message or several concatenated. Echo does not
  care, but any real protocol needs framing (length prefix or delimiter) on top of this loop.
- **Threading.** `Completed` handlers run on I/O completion threads, so per-connection state touched
  from both a synchronous-completion path and a `Completed` path may need synchronization. Keeping one
  SAEA strictly sequential per connection (as here) avoids most of it.

See also [[object-pool]] (pool the SAEA + buffers - the key to the no-allocation steady state),
[[lock-free-semaphore]] (bound the number of accepted connections without locking),
[[async-await-basics]] (the simpler `TcpListener` + `NetworkStream` alternative for small servers),
and [[cancellation]] (a `CancellationToken` to stop the accept loop cleanly).
