<!--icm
{
  "id": "dsa-sorting",
  "title": "Sorting algorithms: bubble, insertion, selection, merge",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "Comparison and C# 5 reference implementations of bubble, insertion, selection, and merge sort, with a Big-O table and guidance to prefer Array.Sort / List<T>.Sort in production.",
  "keywords": ["sorting", "sort", "bubble sort", "insertion sort", "selection sort", "merge sort", "big-o", "complexity", "stable sort", "divide and conquer", "Array.Sort", "List.Sort", "algorithm"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Sorting algorithms: bubble, insertion, selection, merge

Four classic comparison sorts, from the simple O(n^2) trio to merge sort's
O(n log n) divide-and-conquer. This is learning material; for real work use the
framework sorts (see below).

## What each one does

- **Bubble sort** - repeatedly walks the array swapping adjacent out-of-order
  pairs; the largest unsorted value "bubbles" to the end each pass. An
  early-exit flag stops once a pass makes no swaps.
- **Insertion sort** - grows a sorted prefix one element at a time, shifting the
  larger sorted elements right to open a slot for the next key. Fast on nearly
  sorted input.
- **Selection sort** - each pass scans the unsorted remainder for the minimum
  and swaps it into place. Always does the same work regardless of input order.
- **Merge sort** - splits the array in half, sorts each half recursively, then
  merges the two sorted halves. Predictable O(n log n) but needs extra memory.

## Big-O complexity

| Algorithm | Best | Average | Worst | Space | Stable |
|-----------|------|---------|-------|-------|--------|
| Bubble    | O(n) (with early-exit flag) | O(n^2) | O(n^2) | O(1) | yes |
| Insertion | O(n) (already sorted) | O(n^2) | O(n^2) | O(1) | yes |
| Selection | O(n^2) | O(n^2) | O(n^2) | O(1) | no  |
| Merge     | O(n log n) | O(n log n) | O(n log n) | O(n) | yes |

Notes: "stable" means equal keys keep their original relative order. Selection
sort is unstable because the long-range swap can jump an equal element past
another. Merge sort's O(n) space is the scratch arrays used while merging.

## When to use / when not

- **Production: prefer the framework sorts.** `Array.Sort(arr)` and
  `List<T>.Sort()` are O(n log n) (introsort: quicksort with a heapsort
  fallback) and heavily tuned. Reach for these by default. They sort in place
  and are not stable; when you need a stable sort use a stable LINQ ordering
  (`OrderBy`, which is documented stable) and materialize it with `ToList()`.
- **Insertion sort** is the one hand-rolled sort worth keeping: it is genuinely
  good for very small arrays and for nearly-sorted data, and it is what library
  sorts switch to under a small threshold.
- **Bubble and selection sort** are for teaching and tiny fixed inputs only.
  Do not ship them on data that can grow.
- **Merge sort** is worth hand-rolling when you need a guaranteed O(n log n)
  worst case and a stable result, or when sorting data that does not fit a
  single in-memory array (external/linked merge). Otherwise use the framework.

## C# 5 reference implementation

The O(n^2) sorts mutate the array in place. Merge sort returns a new sorted
array (it allocates scratch arrays anyway).

```csharp
using System;

static class Sorting
{
    public static void BubbleSort(int[] a)
    {
        if (a == null) return;
        int n = a.Length;
        for (int i = 0; i < n - 1; i++)
        {
            bool swapped = false;
            for (int j = 0; j < n - 1 - i; j++)
            {
                if (a[j] > a[j + 1])
                {
                    int t = a[j];
                    a[j] = a[j + 1];
                    a[j + 1] = t;
                    swapped = true;
                }
            }
            if (!swapped) break; // already sorted: stop early
        }
    }

    public static void InsertionSort(int[] a)
    {
        if (a == null) return;
        for (int i = 1; i < a.Length; i++)
        {
            int key = a[i];
            int j = i - 1;
            while (j >= 0 && a[j] > key)
            {
                a[j + 1] = a[j];
                j--;
            }
            a[j + 1] = key;
        }
    }

    public static void SelectionSort(int[] a)
    {
        if (a == null) return;
        int n = a.Length;
        for (int i = 0; i < n - 1; i++)
        {
            int min = i;
            for (int j = i + 1; j < n; j++)
            {
                if (a[j] < a[min]) min = j;
            }
            if (min != i)
            {
                int t = a[i];
                a[i] = a[min];
                a[min] = t;
            }
        }
    }

    public static int[] MergeSort(int[] a)
    {
        if (a == null) return null;
        if (a.Length <= 1) return a; // base case: stops the recursion

        int mid = a.Length / 2;
        int[] left = new int[mid];
        int[] right = new int[a.Length - mid];

        Array.Copy(a, 0, left, 0, mid);
        Array.Copy(a, mid, right, 0, a.Length - mid);

        left = MergeSort(left);
        right = MergeSort(right);
        return Merge(left, right);
    }

    private static int[] Merge(int[] left, int[] right)
    {
        int[] result = new int[left.Length + right.Length];
        int i = 0, j = 0, k = 0;

        while (i < left.Length && j < right.Length)
        {
            // <= keeps the sort stable: ties take from the left half first
            if (left[i] <= right[j]) result[k++] = left[i++];
            else result[k++] = right[j++];
        }
        while (i < left.Length) result[k++] = left[i++];
        while (j < right.Length) result[k++] = right[j++];

        return result;
    }
}
```

## Pitfalls / notes

- **Bubble bounds.** The inner loop must stop at `n - 1 - i`: comparing
  `a[j]` with `a[j + 1]` past that reads out of bounds, and re-scanning the
  already-sorted tail is wasted work. The original ported version looped the
  full width every pass with no early-exit flag; both are fixed here.
- **Merge sort recursion depth.** Depth is O(log n), so the stack is safe for
  realistic array sizes (a million elements is ~20 frames deep). It is the rare
  recursion that does not need converting to an explicit stack. See
  [[csharp-iteration]] for when recursion is and is not safe in .NET.
- **Split the right half by remainder, not a guessed size.** Use
  `new int[a.Length - mid]` so odd lengths are handled correctly; do not
  branch on even/odd.
- **Stability comes from the `<=` in the merge.** Changing it to `<` would make
  merge sort unstable. Keep it.
- **Selection sort is not stable** - state that explicitly if a caller relies on
  preserving the order of equal keys.
- For an in-place, idiomatic sort prefer `Array.Sort` / `List<T>.Sort` over any
  of these; see the "when to use" section.

## See also

- [[csharp-iteration]] - loops vs recursion, recursion depth, safe iteration.
- [[dsa-traversal]] - BFS and DFS, which also use Queue/Stack.
- [[dsa-stack-queue]] - the Queue/Stack structures merge and traversal build on.
