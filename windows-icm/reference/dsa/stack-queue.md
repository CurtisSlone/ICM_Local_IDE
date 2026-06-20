<!--icm
{
  "id": "dsa-stack-queue",
  "title": "Stacks and queues: array, linked-list, and two-stack implementations",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "Stack (LIFO) via array and via linked list, queue (FIFO) via linked list, and a queue built from two stacks (amortized O(1)) in C# 5, with Big-O, decision guidance, and a compile-verified generic reference implementation.",
  "keywords": ["stack", "queue", "LIFO", "FIFO", "push", "pop", "peek", "enqueue", "dequeue", "array stack", "linked stack", "linked queue", "two stack queue", "amortized O(1)", "Stack<T>", "Queue<T>", "data structure"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Stacks and queues

A **stack** is last-in-first-out (LIFO): you push and pop at one end (the top). A **queue** is
first-in-first-out (FIFO): you enqueue at the back and dequeue from the front. Both are access-pattern
restrictions over an underlying store - an array or a linked list - and both have O(1) core
operations when implemented correctly.

## Core operations

- **Stack:** `Push(x)` add to top, `Pop()` remove and return top, `Peek()` read top, `Count`.
- **Queue:** `Enqueue(x)` add to back, `Dequeue()` remove and return front, `Peek()` read front,
  `Count`.

This doc covers four implementations:
1. **Stack via array** (`List<T>`): top is the last element.
2. **Stack via linked list**: push/pop at the head.
3. **Queue via linked list**: enqueue at the tail, dequeue at the head.
4. **Queue via two stacks**: an inbox and an outbox; items shift only when the outbox empties, giving
   amortized O(1) dequeue.

## Big-O complexity

| Operation                | Array stack    | Linked stack | Linked queue | Two-stack queue   |
|--------------------------|----------------|--------------|--------------|-------------------|
| Push / Enqueue           | O(1) amortized | O(1)         | O(1)         | O(1)              |
| Pop / Dequeue            | O(1)           | O(1)         | O(1)         | O(1) amortized    |
| Peek                     | O(1)           | O(1)         | O(1)         | O(1) amortized    |
| Count                    | O(1)           | O(1)         | O(1)         | O(1)              |
| Space                    | O(n)           | O(n)         | O(n)         | O(n)              |

Notes on the amortized entries:
- Array stack `Push` is O(1) amortized because the backing array doubles on growth; any single push
  that triggers a resize is O(n), but the cost spread over all pushes is O(1).
- Two-stack queue `Dequeue`/`Peek` are O(1) amortized: a dequeue that finds the outbox empty moves
  every element from inbox to outbox once (O(n)), but each element is moved at most once between
  enqueue and dequeue, so the per-operation cost averages O(1).

## When to use / when not

- **Stack via array (`List<T>`) is the default stack.** Contiguous, cache-friendly, no per-node
  pointer overhead. Prefer it unless you specifically need node-level operations.
- **Stack/queue via linked list when** you want guaranteed O(1) ends with no array resizing, or you
  are already working with linked nodes. A linked stack never pays a resize spike.
- **Queue via array is a trap if done naively:** dequeuing from the front of a plain `List<T>` is
  O(n) because it shifts every element. Use a linked list, a circular buffer, or two stacks instead.
- **Queue via two stacks when** you only have a stack primitive available, or as an interview-style
  construction. It is rarely the best choice in real code, but the amortized analysis is the point.
- **In real .NET code use `System.Collections.Generic.Stack<T>` and `Queue<T>`.** They are correct,
  fast, and dynamically sized. Write your own only to learn the structure. (`Queue<T>` internally is
  a circular buffer, not two stacks.)

## Reference implementation (C# 5, compile-verified)

```csharp
using System;
using System.Collections.Generic;

// Stack backed by a dynamic array (List<T>). LIFO. Top is the last element.
public class ArrayStack<T>
{
    private List<T> items;

    public ArrayStack()
    {
        this.items = new List<T>();
    }

    public int Count { get { return this.items.Count; } }
    public bool IsEmpty { get { return this.items.Count == 0; } }

    // O(1) amortized.
    public void Push(T value)
    {
        this.items.Add(value);
    }

    // O(1). Throws when empty.
    public T Pop()
    {
        if (this.items.Count == 0)
        {
            throw new InvalidOperationException("Pop from empty stack.");
        }
        int last = this.items.Count - 1;
        T value = this.items[last];
        this.items.RemoveAt(last);
        return value;
    }

    // O(1). Throws when empty.
    public T Peek()
    {
        if (this.items.Count == 0)
        {
            throw new InvalidOperationException("Peek on empty stack.");
        }
        return this.items[this.items.Count - 1];
    }
}

// Singly linked node shared by the linked-list stack and queue below.
public class SllNode<T>
{
    public T Value;
    public SllNode<T> Next;

    public SllNode(T value)
    {
        this.Value = value;
        this.Next = null;
    }
}

// Stack backed by a singly linked list. Push/pop at the top (head). LIFO.
public class LinkedStack<T>
{
    private SllNode<T> top;
    private int length;

    public LinkedStack()
    {
        this.top = null;
        this.length = 0;
    }

    public int Count { get { return this.length; } }
    public bool IsEmpty { get { return this.length == 0; } }

    // O(1).
    public void Push(T value)
    {
        SllNode<T> node = new SllNode<T>(value);
        node.Next = this.top;
        this.top = node;
        this.length++;
    }

    // O(1). Throws when empty.
    public T Pop()
    {
        if (this.top == null)
        {
            throw new InvalidOperationException("Pop from empty stack.");
        }
        SllNode<T> node = this.top;
        this.top = this.top.Next;
        this.length--;
        return node.Value;
    }

    // O(1). Throws when empty.
    public T Peek()
    {
        if (this.top == null)
        {
            throw new InvalidOperationException("Peek on empty stack.");
        }
        return this.top.Value;
    }
}

// Queue backed by a singly linked list. Enqueue at the tail, dequeue at the head. FIFO.
public class LinkedQueue<T>
{
    private SllNode<T> first;
    private SllNode<T> last;
    private int length;

    public LinkedQueue()
    {
        this.first = null;
        this.last = null;
        this.length = 0;
    }

    public int Count { get { return this.length; } }
    public bool IsEmpty { get { return this.length == 0; } }

    // O(1).
    public void Enqueue(T value)
    {
        SllNode<T> node = new SllNode<T>(value);
        if (this.length == 0)
        {
            this.first = node;
            this.last = node;
        }
        else
        {
            this.last.Next = node;
            this.last = node;
        }
        this.length++;
    }

    // O(1). Throws when empty. Resets last when the queue empties.
    public T Dequeue()
    {
        if (this.first == null)
        {
            throw new InvalidOperationException("Dequeue from empty queue.");
        }
        SllNode<T> node = this.first;
        this.first = this.first.Next;
        this.length--;
        if (this.length == 0)
        {
            this.last = null;
        }
        return node.Value;
    }

    // O(1). Throws when empty.
    public T Peek()
    {
        if (this.first == null)
        {
            throw new InvalidOperationException("Peek on empty queue.");
        }
        return this.first.Value;
    }
}

// Queue built from two stacks. Amortized O(1) dequeue: items only move
// from inbox to outbox when outbox is empty.
public class TwoStackQueue<T>
{
    private Stack<T> inbox;
    private Stack<T> outbox;

    public TwoStackQueue()
    {
        this.inbox = new Stack<T>();
        this.outbox = new Stack<T>();
    }

    public int Count { get { return this.inbox.Count + this.outbox.Count; } }
    public bool IsEmpty { get { return this.Count == 0; } }

    // O(1).
    public void Enqueue(T value)
    {
        this.inbox.Push(value);
    }

    // Amortized O(1). Throws when empty.
    public T Dequeue()
    {
        ShiftIfNeeded();
        if (this.outbox.Count == 0)
        {
            throw new InvalidOperationException("Dequeue from empty queue.");
        }
        return this.outbox.Pop();
    }

    // Amortized O(1). Throws when empty.
    public T Peek()
    {
        ShiftIfNeeded();
        if (this.outbox.Count == 0)
        {
            throw new InvalidOperationException("Peek on empty queue.");
        }
        return this.outbox.Peek();
    }

    private void ShiftIfNeeded()
    {
        if (this.outbox.Count == 0)
        {
            while (this.inbox.Count > 0)
            {
                this.outbox.Push(this.inbox.Pop());
            }
        }
    }
}
```

## Pitfalls / notes

- **Decide what empty does and be consistent.** The source returned a sentinel like `-100000` on an
  empty pop/dequeue, which silently corrupts data and only works for `int`. This port throws
  `InvalidOperationException` (the same thing `Stack<T>`/`Queue<T>` do). If you prefer a non-throwing
  API, add a `bool TryPop(out T value)` style method instead of a magic number.
- **A linked queue must reset `last` when it empties.** The source checked `length == 0` before
  decrementing (so the branch never fired) and left `last` dangling, which breaks the next enqueue.
  The corrected `Dequeue` decrements first, then nulls `last` when `length` hits 0.
- **Do not shift the two-stack queue on every dequeue.** Moving inbox to outbox unconditionally makes
  each dequeue O(n). Only refill the outbox when it is empty (`ShiftIfNeeded`); that is what makes it
  amortized O(1).
- **Never build a queue on `List<T>.RemoveAt(0)`** - that is O(n) per dequeue. Use a linked list, the
  two-stack trick, or a circular buffer.
- **In production use `Stack<T>` and `Queue<T>`** from `System.Collections.Generic`. The
  hand-written versions here are for learning and for the two-stack construction.
- The array stack push is O(1) amortized, not strictly O(1), because `List<T>` resizes. See
  [[csharp-iteration]] for related bounded-loop and growth notes.

## See also

- [[dsa-linked-list]] - the singly linked list these stack and queue types are built on.
- [[csharp-iteration]] - loop and growth conventions for these operations.
