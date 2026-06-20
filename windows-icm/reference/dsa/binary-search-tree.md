<!--icm
{
  "id": "dsa-binary-search-tree",
  "title": "Binary search tree: insert, lookup, remove and complexity",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "An ordered binary tree where each node's left subtree is smaller and right subtree is larger: insert/lookup/remove are O(log n) when balanced but degrade to O(n) on a degenerate (sorted-insert) tree; remove must handle the leaf, one-child, and two-child (in-order successor) cases. Iterative C# 5 reference implementation.",
  "keywords": ["binary search tree", "bst", "tree", "insert", "lookup", "search", "remove", "delete", "in-order successor", "balanced", "degenerate", "unbalanced", "node", "O(log n)", "recursion depth"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Binary search tree: insert, lookup, remove and complexity

A binary search tree (BST) is an ordered binary tree: for every node, all values in its left
subtree are smaller and all values in its right subtree are larger. That ordering invariant lets
you find, insert, and remove a value by walking down one path instead of scanning everything.

## Core operations

- **Insert.** Walk from the root: go left when the new value is smaller, right otherwise, until you
  reach an empty slot, and hang the new node there. (This implementation sends duplicates right.)
- **Lookup.** Same walk; return the node when the value matches, or null when you fall off the tree.
- **Remove.** Find the node and its parent, then re-link around it. Three shapes:
  - **Leaf** (no children): replace the slot with null.
  - **One child:** replace the slot with that child.
  - **Two children:** replace the node with its in-order successor (the leftmost node of the right
    subtree), detach the successor from where it was, and give it the removed node's two subtrees.

The whole structure is just nodes; there is no array backing. A `BstNode` holds a value plus left
and right references.

## Big-O complexity

`n` is the number of nodes; `h` is the tree height. Balanced means `h` is about `log n`; degenerate
(for example inserting already-sorted values) means `h` is `n` and the tree behaves like a linked
list.

| Operation | Balanced (avg) | Worst case (degenerate) |
|-----------|----------------|--------------------------|
| Insert    | O(log n)       | O(n)                     |
| Lookup    | O(log n)       | O(n)                     |
| Remove    | O(log n)       | O(n)                     |
| Space     | O(n)           | O(n)                     |

Note: a plain BST has **no self-balancing**. Insert sorted or near-sorted data and every operation
degrades to O(n). If you need a guaranteed O(log n), use a self-balancing variant (red-black /
AVL); the in-box `SortedDictionary<K,V>` / `SortedSet<T>` are red-black trees.

## When to use / when not

Use a BST when:

- You need ordered data with fast insert, delete, and membership all at once, and you can keep it
  reasonably balanced (or use a balanced variant).
- You want in-order traversal to yield sorted output for free.
- You need range or nearest-neighbour queries (find everything between two keys, find the next
  larger value).

Do not use a BST when:

- You only need membership or key lookup and ordering does not matter. A `Dictionary<K,V>` /
  `HashSet<T>` gives O(1) average and is simpler.
- Your inserts arrive sorted and you cannot balance. You will build a degenerate O(n) chain. Use a
  balanced tree or sort plus binary search over an array instead.
- The data is static. Sort once into an array and binary-search it.

## Reference implementation (C# 5)

Iterative insert, lookup, and remove. The remove handles the not-found, leaf, one-child, and
two-child cases. Self-contained: includes the node type.

```csharp
using System;

public class BstNode
{
    public BstNode Left { get; set; }
    public BstNode Right { get; set; }
    public int Value { get; set; }

    public BstNode(int value)
    {
        this.Left = null;
        this.Right = null;
        this.Value = value;
    }
}

public class BinarySearchTree
{
    private BstNode root;

    public BinarySearchTree()
    {
        this.root = null;
    }

    // Insert a value. Duplicates go to the right subtree.
    public void Insert(int value)
    {
        BstNode newNode = new BstNode(value);
        if (this.root == null)
        {
            this.root = newNode;
            return;
        }

        BstNode current = this.root;
        while (true)
        {
            if (value < current.Value)
            {
                if (current.Left == null)
                {
                    current.Left = newNode;
                    return;
                }
                current = current.Left;
            }
            else
            {
                if (current.Right == null)
                {
                    current.Right = newNode;
                    return;
                }
                current = current.Right;
            }
        }
    }

    // Return the node holding value, or null if not present.
    public BstNode Lookup(int value)
    {
        BstNode current = this.root;
        while (current != null)
        {
            if (value < current.Value)
            {
                current = current.Left;
            }
            else if (value > current.Value)
            {
                current = current.Right;
            }
            else
            {
                return current;
            }
        }
        return null;
    }

    // Remove value if present. Handles all four cases:
    // not found, leaf, one child, two children.
    public void Remove(int value)
    {
        BstNode nodeToRemove = this.root;
        BstNode parent = null;

        // Search for the node and its parent.
        while (nodeToRemove != null && nodeToRemove.Value != value)
        {
            parent = nodeToRemove;
            if (value < nodeToRemove.Value)
            {
                nodeToRemove = nodeToRemove.Left;
            }
            else
            {
                nodeToRemove = nodeToRemove.Right;
            }
        }

        if (nodeToRemove == null)
        {
            // Value not in the tree; nothing to do.
            return;
        }

        // Pick the subtree that replaces nodeToRemove.
        BstNode replacement;
        if (nodeToRemove.Right != null)
        {
            replacement = nodeToRemove.Right;
            if (replacement.Left == null)
            {
                // Right child has no left subtree: it takes the spot directly.
                replacement.Left = nodeToRemove.Left;
            }
            else
            {
                // Find the leftmost node in the right subtree (in-order successor).
                BstNode replacementParent = nodeToRemove;
                while (replacement.Left != null)
                {
                    replacementParent = replacement;
                    replacement = replacement.Left;
                }
                // Detach the successor and splice in both subtrees.
                replacementParent.Left = replacement.Right;
                replacement.Left = nodeToRemove.Left;
                replacement.Right = nodeToRemove.Right;
            }
        }
        else if (nodeToRemove.Left != null)
        {
            // Only a left child.
            replacement = nodeToRemove.Left;
        }
        else
        {
            // Leaf: no children, so the slot becomes empty.
            replacement = null;
        }

        // Re-link the replacement into the parent (or the root).
        if (parent == null)
        {
            this.root = replacement;
        }
        else if (parent.Left == nodeToRemove)
        {
            parent.Left = replacement;
        }
        else
        {
            parent.Right = replacement;
        }
    }
}
```

## Pitfalls / notes

- **The leaf and not-found cases are easy to miss in remove.** The original port left `replacement`
  defaulting only when there was a right or left child, so removing a leaf dropped through with the
  wrong value, and searching for an absent value walked off the tree into a null-reference. This
  version guards both: the search loop has a `nodeToRemove != null` condition and returns early when
  the value is absent, and the leaf case explicitly sets `replacement = null`.
- **Two-children remove must detach the successor.** When the in-order successor is deeper than the
  immediate right child, set its old parent's left to the successor's right child before re-linking,
  or you duplicate a subtree.
- **Keep these operations iterative.** A recursive insert/lookup/remove on a degenerate BST recurses
  to depth `n`; .NET does not guarantee tail-call optimization, so a deep enough tree throws an
  uncatchable `StackOverflowException`. The loops here stay flat regardless of height. See
  [[csharp-iteration]] for iteration-vs-recursion and converting deep recursion to an explicit
  stack.
- **Duplicates.** This implementation routes equal values right. If you need set semantics, check
  `Lookup` first or reject equal keys in `Insert`.

## See also

- [[csharp-iteration]] - loops vs recursion, recursion depth, safe iteration.
- [[dsa-traversal]] - in-order / pre-order / post-order and BFS/DFS walks of a tree.
- [[dsa-stack-queue]] - the stack/queue an iterative traversal uses.
- [[dsa-graph]] - a BST is a tree, which is a special case of a graph.
