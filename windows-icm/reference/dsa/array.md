<!--icm
{
  "id": "dsa-array",
  "title": "Arrays: operations, complexity, common problems",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "Array and dynamic-array operations with Big-O, plus merge-two-sorted-arrays and reverse-a-string, as compile-verified C# 5.",
  "keywords": ["array", "dynamic array", "resizable array", "list", "push", "pop", "delete", "shift", "merge sorted arrays", "two pointers", "reverse string", "big-o", "amortized", "index out of range"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Arrays: operations, complexity, common problems

An array is a contiguous, fixed-size block of elements addressed by integer index. A dynamic array
(what `List<T>` is built on) wraps a backing array and grows it when full, giving amortized O(1)
append. This entry covers the core operations and two classic array problems.

## Core operations

- Read or write by index: direct address, O(1).
- Append (push) to the end: O(1) amortized when capacity is doubled; the occasional resize copies
  every element.
- Pop from the end: O(1).
- Insert or delete in the middle: O(n) because the tail must shift to keep elements contiguous.
- Search by value (unsorted): O(n) linear scan.

## Big-O complexity

| Operation | Time | Space |
|---|---|---|
| Access by index | O(1) | O(1) |
| Set by index | O(1) | O(1) |
| Push (append, amortized) | O(1) | O(1) amortized |
| Push (single resize event) | O(n) | O(n) |
| Pop (end) | O(1) | O(1) |
| Insert at middle/front | O(n) | O(1) |
| Delete at index (shift) | O(n) | O(1) |
| Search unsorted by value | O(n) | O(1) |
| Merge two sorted arrays (len m, n) | O(m + n) | O(m + n) |
| Reverse a string (len n) | O(n) | O(n) |

## When to use / when not

- Use an array or `List<T>` when you index by position, append/pop at the end, or iterate in order.
  This is the default sequential container.
- Avoid arrays when you frequently insert or remove at the front or middle: every such operation is
  O(n). A linked list or a different structure fits better.
- Avoid building a string with repeated `+=` in a loop: each concatenation allocates a new string, so
  the loop is O(n^2). Build a `char[]` (or `StringBuilder`) and create the string once.
- For lookup by key rather than position, use a hash table: see [[dsa-hash-table]].

## Reference implementation (C# 5)

```csharp
using System;

// A simple dynamic array (resizable) over a backing object array.
public class DynamicArray
{
    private object[] data;
    private int count;

    public DynamicArray()
    {
        this.data = new object[1];
        this.count = 0;
    }

    public int Length
    {
        get { return this.count; }
    }

    public object Get(int index)
    {
        if (index < 0 || index >= this.count)
        {
            throw new IndexOutOfRangeException("index out of range");
        }
        return this.data[index];
    }

    public void Set(int index, object item)
    {
        if (index < 0 || index >= this.count)
        {
            throw new IndexOutOfRangeException("index out of range");
        }
        this.data[index] = item;
    }

    public void Push(object item)
    {
        if (this.count == this.data.Length)
        {
            // Double the capacity so a sequence of pushes is amortized O(1).
            int newCapacity = this.data.Length * 2;
            object[] grown = new object[newCapacity];
            Array.Copy(this.data, grown, this.count);
            this.data = grown;
        }
        this.data[this.count] = item;
        this.count++;
    }

    public object Pop()
    {
        if (this.count == 0)
        {
            throw new InvalidOperationException("pop from empty array");
        }
        object popped = this.data[this.count - 1];
        this.data[this.count - 1] = null; // release the reference
        this.count--;
        return popped;
    }

    public object Delete(int index)
    {
        if (index < 0 || index >= this.count)
        {
            throw new IndexOutOfRangeException("index out of range");
        }
        object removed = this.data[index];
        for (int i = index; i < this.count - 1; i++)
        {
            this.data[i] = this.data[i + 1];
        }
        this.data[this.count - 1] = null;
        this.count--;
        return removed;
    }
}

public static class ArrayProblems
{
    // Merge two already-sorted arrays into one sorted array.
    public static int[] MergeSorted(int[] a, int[] b)
    {
        if (a == null) { a = new int[0]; }
        if (b == null) { b = new int[0]; }
        int[] result = new int[a.Length + b.Length];
        int i = 0, j = 0, k = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] <= b[j])
            {
                result[k] = a[i];
                i++;
            }
            else
            {
                result[k] = b[j];
                j++;
            }
            k++;
        }
        while (i < a.Length)
        {
            result[k] = a[i];
            i++;
            k++;
        }
        while (j < b.Length)
        {
            result[k] = b[j];
            j++;
            k++;
        }
        return result;
    }

    // Reverse a string. Building a char[] is O(n); repeated string += is O(n^2).
    public static string Reverse(string s)
    {
        if (s == null) { return null; }
        char[] chars = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            chars[i] = s[s.Length - 1 - i];
        }
        return new string(chars);
    }
}
```

## Pitfalls / notes

- The original `Push` grew the backing array by exactly one slot each time, making every append an
  O(n) copy and the whole sequence O(n^2). Doubling the capacity restores amortized O(1).
- The original `Get`, `Pop`, and `Delete` did no bounds or empty checks; an out-of-range index or a
  pop on an empty array silently misbehaved. Guard them and throw a clear exception.
- Null out the slot you remove (in `Pop` and `Delete`) so the array does not pin a reference the
  caller thinks is gone.
- Merge uses the two-pointer technique and relies on both inputs being sorted; using `<=` (not `<`)
  keeps the merge stable for equal values. Inputs must already be sorted - otherwise the output is
  not sorted.
- When shifting to delete, iterate forward and copy `data[i + 1]` into `data[i]`; stop at
  `count - 1` so you do not read past the live range. See [[csharp-iteration]] for loop choice and
  safe iteration (never mutate a collection during `foreach`).

## See also

- [[dsa-hash-table]] - O(1) average lookup by key instead of by position.
- [[csharp-iteration]] - which loop to use, bounds, and safe iteration.
- [[csharp-control-flow]] - guard clauses and conditionals.
