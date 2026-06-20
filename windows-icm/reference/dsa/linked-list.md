<!--icm
{
  "id": "dsa-linked-list",
  "title": "Linked lists (singly and doubly): operations and complexity",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "Singly and doubly linked lists in C# 5: append/prepend/insert/remove/reverse, their Big-O, when a linked list beats an array or List<T>, and a compile-verified generic reference implementation.",
  "keywords": ["linked list", "singly linked list", "doubly linked list", "node", "head", "tail", "append", "prepend", "insert", "remove", "reverse", "traverse", "LinkedList<T>", "pointer", "O(1) insert", "O(n) traversal", "data structure"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Linked lists (singly and doubly)

A linked list stores elements in separate node objects, each holding a value and a reference to the
next node (and, in a doubly linked list, the previous node). There is no contiguous block and no
index arithmetic: you reach element k by walking k links from the head.

## Core operations

- **Append** - add to the tail. O(1) when you keep a `tail` pointer.
- **Prepend** - add to the head. O(1).
- **Insert at index** - splice a node in. O(n) to walk to the spot, the relink itself is O(1).
- **RemoveAt index** - unsplice a node. O(n) to walk, O(1) relink.
- **Traverse / search** - follow `Next` from the head. O(n).
- **Reverse** - flip every `Next` link. O(n).

A **singly** linked list links one way (head to tail) and can only walk forward. A **doubly** linked
list adds a `Previous` link, so it can walk backward, delete a known node in O(1) without finding its
predecessor, and reach a tail-side index by walking from the tail.

## Big-O complexity

| Operation                  | Singly      | Doubly      | Notes |
|----------------------------|-------------|-------------|-------|
| Index access (get k-th)    | O(n)        | O(n)        | No random access; doubly can start from the nearer end |
| Append (tail)              | O(1)        | O(1)        | Requires a kept `tail` pointer |
| Prepend (head)             | O(1)        | O(1)        | |
| Insert at index            | O(n)        | O(n)        | Walk is O(n); relink is O(1) |
| Remove at index            | O(n)        | O(n)        | Walk is O(n); relink is O(1) |
| Remove a known node        | O(n)        | O(1)        | Singly must find the predecessor; doubly has `Previous` |
| Search by value           | O(n)        | O(n)        | |
| Reverse                    | O(n)        | O(n)        | |
| Space (whole structure)    | O(n)        | O(n)        | Doubly uses one extra pointer per node |

Auxiliary space for the operations above is O(1) (the corrected `Reverse` below is iterative and
in-place; a recursive reverse would be O(n) stack and can overflow on a long list - see
[[csharp-iteration]]).

## When to use / when not

- **Use a linked list when** you do many inserts or removes at the ends (or at a node you already
  hold a reference to) and rarely need random access by index. A queue or a stack maps cleanly onto
  list ends.
- **Prefer an array or `List<T>` when** you need indexed access, you iterate a lot, or you care about
  memory and cache behavior. Contiguous storage is far more cache-friendly; a linked list pays a
  pointer per node and scatters nodes across the heap, so even O(n) array scans usually beat O(n)
  list walks in practice.
- **Doubly over singly when** you need backward traversal or O(1) deletion of a node you already
  hold. Otherwise singly is lighter (one fewer pointer per node).
- **In real .NET code, reach for `System.Collections.Generic.LinkedList<T>` first** (a doubly linked
  list with O(1) `AddFirst`/`AddLast`/`Remove(node)`). Write your own only to learn the structure or
  when you need behavior the BCL type does not give you.

## Reference implementation (C# 5, compile-verified)

```csharp
using System;

public class SinglyNode<T>
{
    public T Value;
    public SinglyNode<T> Next;

    public SinglyNode(T value)
    {
        this.Value = value;
        this.Next = null;
    }
}

public class SinglyLinkedList<T>
{
    private SinglyNode<T> head;
    private SinglyNode<T> tail;
    private int length;

    public SinglyLinkedList()
    {
        this.head = null;
        this.tail = null;
        this.length = 0;
    }

    public int Count { get { return this.length; } }
    public SinglyNode<T> Head { get { return this.head; } }
    public SinglyNode<T> Tail { get { return this.tail; } }

    // O(1): append at tail.
    public void Append(T value)
    {
        SinglyNode<T> node = new SinglyNode<T>(value);
        if (this.length == 0)
        {
            this.head = node;
            this.tail = node;
        }
        else
        {
            this.tail.Next = node;
            this.tail = node;
        }
        this.length++;
    }

    // O(1): prepend at head.
    public void Prepend(T value)
    {
        SinglyNode<T> node = new SinglyNode<T>(value);
        if (this.length == 0)
        {
            this.head = node;
            this.tail = node;
        }
        else
        {
            node.Next = this.head;
            this.head = node;
        }
        this.length++;
    }

    // O(n): insert so the new node ends up at position index.
    public void Insert(int index, T value)
    {
        if (index <= 0)
        {
            Prepend(value);
            return;
        }
        if (index >= this.length)
        {
            Append(value);
            return;
        }

        SinglyNode<T> leader = NodeAt(index - 1);
        SinglyNode<T> node = new SinglyNode<T>(value);
        node.Next = leader.Next;
        leader.Next = node;
        this.length++;
    }

    // O(n): remove the node at position index.
    public void RemoveAt(int index)
    {
        if (this.length == 0)
        {
            return;
        }
        if (index < 0)
        {
            index = 0;
        }
        if (index > this.length - 1)
        {
            index = this.length - 1;
        }

        if (index == 0)
        {
            this.head = this.head.Next;
            this.length--;
            if (this.length == 0)
            {
                this.tail = null;
            }
            return;
        }

        SinglyNode<T> leader = NodeAt(index - 1);
        SinglyNode<T> toRemove = leader.Next;
        leader.Next = toRemove.Next;
        if (toRemove == this.tail)
        {
            this.tail = leader;
        }
        this.length--;
    }

    // O(n): reverse in place.
    public void Reverse()
    {
        if (this.length < 2)
        {
            return;
        }
        SinglyNode<T> previous = null;
        SinglyNode<T> current = this.head;
        this.tail = this.head;
        while (current != null)
        {
            SinglyNode<T> nextTemp = current.Next;
            current.Next = previous;
            previous = current;
            current = nextTemp;
        }
        this.head = previous;
    }

    // O(n): node at position index, or null if out of range.
    private SinglyNode<T> NodeAt(int index)
    {
        if (index < 0 || index >= this.length)
        {
            return null;
        }
        SinglyNode<T> current = this.head;
        for (int i = 0; i < index; i++)
        {
            current = current.Next;
        }
        return current;
    }
}

public class DoublyNode<T>
{
    public T Value;
    public DoublyNode<T> Next;
    public DoublyNode<T> Previous;

    public DoublyNode(T value)
    {
        this.Value = value;
        this.Next = null;
        this.Previous = null;
    }
}

public class DoublyLinkedList<T>
{
    private DoublyNode<T> head;
    private DoublyNode<T> tail;
    private int length;

    public DoublyLinkedList()
    {
        this.head = null;
        this.tail = null;
        this.length = 0;
    }

    public int Count { get { return this.length; } }
    public DoublyNode<T> Head { get { return this.head; } }
    public DoublyNode<T> Tail { get { return this.tail; } }

    // O(1): append at tail.
    public void Append(T value)
    {
        DoublyNode<T> node = new DoublyNode<T>(value);
        if (this.length == 0)
        {
            this.head = node;
            this.tail = node;
        }
        else
        {
            node.Previous = this.tail;
            this.tail.Next = node;
            this.tail = node;
        }
        this.length++;
    }

    // O(1): prepend at head.
    public void Prepend(T value)
    {
        DoublyNode<T> node = new DoublyNode<T>(value);
        if (this.length == 0)
        {
            this.head = node;
            this.tail = node;
        }
        else
        {
            node.Next = this.head;
            this.head.Previous = node;
            this.head = node;
        }
        this.length++;
    }

    // O(n): insert so the new node ends up at position index.
    public void Insert(int index, T value)
    {
        if (index <= 0)
        {
            Prepend(value);
            return;
        }
        if (index >= this.length)
        {
            Append(value);
            return;
        }

        DoublyNode<T> follower = NodeAt(index);
        DoublyNode<T> leader = follower.Previous;
        DoublyNode<T> node = new DoublyNode<T>(value);

        node.Previous = leader;
        node.Next = follower;
        leader.Next = node;
        follower.Previous = node;
        this.length++;
    }

    // O(n): remove the node at position index. Maintains head, tail, and both links.
    public void RemoveAt(int index)
    {
        if (this.length == 0)
        {
            return;
        }
        if (index < 0)
        {
            index = 0;
        }
        if (index > this.length - 1)
        {
            index = this.length - 1;
        }

        DoublyNode<T> toRemove = NodeAt(index);
        DoublyNode<T> before = toRemove.Previous;
        DoublyNode<T> after = toRemove.Next;

        if (before == null)
        {
            this.head = after;
        }
        else
        {
            before.Next = after;
        }

        if (after == null)
        {
            this.tail = before;
        }
        else
        {
            after.Previous = before;
        }

        this.length--;
    }

    // O(n): node at index, walking from the nearer end.
    private DoublyNode<T> NodeAt(int index)
    {
        if (index < 0 || index >= this.length)
        {
            return null;
        }
        DoublyNode<T> current;
        if (index <= this.length / 2)
        {
            current = this.head;
            for (int i = 0; i < index; i++)
            {
                current = current.Next;
            }
        }
        else
        {
            current = this.tail;
            for (int i = this.length - 1; i > index; i--)
            {
                current = current.Previous;
            }
        }
        return current;
    }
}
```

## Pitfalls / notes

- **Maintain the `tail` pointer on every mutation.** The common source bug is `RemoveAt(0)` (or any
  removal that empties the list or deletes the last node) leaving `tail` dangling and `length` wrong.
  The implementation above updates `head`, `tail`, and `length` in every branch.
- **Reverse must reset `tail`.** After reversing, the old head becomes the tail. A reverse that walks
  `second.Next` without a null check throws on a 0- or 1-element list; the version above returns
  early when `length < 2`.
- **Index clamping vs. throwing.** The source clamped out-of-range indices into bounds; this port
  keeps that lenient behavior so calls do not throw. If you want strict APIs, replace the clamps with
  `throw new ArgumentOutOfRangeException("index")`.
- **Do not reverse or walk a long list recursively.** .NET has no guaranteed tail-call optimization,
  so deep recursion overflows the stack. Keep these iterative. See [[csharp-iteration]].
- **In production prefer `LinkedList<T>`** from `System.Collections.Generic` unless you are
  implementing the structure for its own sake.

## See also

- [[dsa-stack-queue]] - stacks and queues built on top of a singly linked list.
- [[csharp-iteration]] - why these operations are loops, not recursion.
