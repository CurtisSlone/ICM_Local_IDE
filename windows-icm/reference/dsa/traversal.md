<!--icm
{
  "id": "dsa-traversal",
  "title": "Graph traversal: breadth-first search and depth-first search",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "BFS (Queue) versus DFS (Stack or recursion) over an adjacency-list graph, with a complexity table, when to use each, and self-contained C# 5 implementations including a visited set to handle cycles.",
  "keywords": ["traversal", "graph", "tree", "BFS", "breadth first search", "DFS", "depth first search", "queue", "stack", "visited set", "shortest path", "adjacency list", "topological", "algorithm"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Graph traversal: breadth-first search and depth-first search

The two ways to visit every reachable node from a start node. BFS explores in
rings of increasing distance; DFS goes as deep as it can before backtracking.

## What each one does

- **BFS (breadth-first search)** - visits the start, then all its neighbors,
  then their neighbors, and so on. Driven by a **Queue** (FIFO): a node's
  neighbors are enqueued and processed after every nearer node. On an unweighted
  graph the order of first-visit is shortest-path order.
- **DFS (depth-first search)** - follows one path as far as possible, then
  backtracks and tries the next branch. Driven by a **Stack** (LIFO) iteratively,
  or by the call stack recursively. Natural fit for "is there a path", cycle
  detection, and topological ordering.

Both need a **visited set** on any graph that can have cycles or multiple paths
to a node; without it they loop forever or revisit nodes. (A strict tree has
neither, so tree code often omits it - but a general graph needs it.)

## BFS vs DFS

| Aspect | BFS | DFS |
|--------|-----|-----|
| Data structure | Queue (FIFO) | Stack (LIFO) or recursion |
| Time | O(V + E) | O(V + E) |
| Space | O(V) (queue can hold a whole level) | O(V) (stack / recursion depth) |
| Visit order | nearest first (by edge count) | deepest first along each branch |
| Shortest path (unweighted) | yes, finds it | no |
| Memory shape | wide graphs cost more (broad levels) | deep graphs cost more (long stack) |

V = number of vertices, E = number of edges. Both touch every vertex and every
edge once, so both are O(V + E) in time on an adjacency list.

## When to use / when not

- **BFS for shortest path in an unweighted graph** - fewest edges from start to
  target. Also good for "all nodes within k steps" and level-by-level work.
- **DFS for path existence, cycle detection, topological sort, and
  connected-component labeling** - anything about reachability or ordering
  rather than distance.
- **Neither finds shortest path on a weighted graph** - use Dijkstra or A* there.
- **Watch memory by shape.** BFS holds an entire frontier level, so it is heavy
  on wide graphs. Recursive DFS is bounded by the deepest path, so it can
  overflow the stack on deep graphs - prefer the iterative Stack version when
  depth could be large (see [[csharp-iteration]]).

## C# 5 reference implementation

A minimal adjacency-list graph node, then BFS, iterative DFS, and recursive DFS.
All three carry a visited set so they terminate on cyclic graphs.

```csharp
using System;
using System.Collections.Generic;

class GraphNode
{
    public int Value;
    public List<GraphNode> Neighbors;

    public GraphNode(int value)
    {
        this.Value = value;
        this.Neighbors = new List<GraphNode>();
    }
}

static class Traversal
{
    public static List<int> BreadthFirst(GraphNode start)
    {
        List<int> order = new List<int>();
        if (start == null) return order;

        HashSet<GraphNode> visited = new HashSet<GraphNode>();
        Queue<GraphNode> queue = new Queue<GraphNode>();
        queue.Enqueue(start);
        visited.Add(start); // mark on enqueue so a node is queued at most once

        while (queue.Count > 0)
        {
            GraphNode current = queue.Dequeue();
            order.Add(current.Value);
            foreach (GraphNode next in current.Neighbors)
            {
                if (!visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return order;
    }

    public static List<int> DepthFirstIterative(GraphNode start)
    {
        List<int> order = new List<int>();
        if (start == null) return order;

        HashSet<GraphNode> visited = new HashSet<GraphNode>();
        Stack<GraphNode> stack = new Stack<GraphNode>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            GraphNode current = stack.Pop();
            // mark on pop: a node can be pushed more than once, so guard here
            if (visited.Contains(current)) continue;
            visited.Add(current);
            order.Add(current.Value);

            // push in reverse so neighbor[0] is visited first (matches recursion)
            for (int i = current.Neighbors.Count - 1; i >= 0; i--)
            {
                GraphNode next = current.Neighbors[i];
                if (!visited.Contains(next)) stack.Push(next);
            }
        }
        return order;
    }

    public static List<int> DepthFirstRecursive(GraphNode start)
    {
        List<int> order = new List<int>();
        DfsVisit(start, new HashSet<GraphNode>(), order);
        return order;
    }

    private static void DfsVisit(GraphNode node, HashSet<GraphNode> visited, List<int> order)
    {
        if (node == null || visited.Contains(node)) return;
        visited.Add(node);
        order.Add(node.Value);
        foreach (GraphNode next in node.Neighbors)
        {
            DfsVisit(next, visited, order);
        }
    }
}
```

## Pitfalls / notes

- **BFS uses a Queue, DFS uses a Stack.** Swapping the structure swaps the
  algorithm. The ported tree version recursed for BFS, which only worked because
  it threaded the queue through the recursion - that is BFS by data structure,
  DFS by control flow, and it is confusing. Use the iterative loop for BFS.
- **Always carry a visited set on a general graph.** The original tree code had
  none because a tree has no cycles; on any graph with a back-edge, BFS/DFS
  without a visited set never terminates.
- **Mark visited at the right moment.** BFS marks on *enqueue* so a node is never
  queued twice. The iterative DFS may push a node more than once, so it marks and
  checks on *pop*. Mixing these up causes duplicates in the output.
- **Recursive DFS and stack depth.** Recursion depth equals the longest path. On
  a deep or adversarial graph that throws an uncatchable `StackOverflowException`
  - convert to the iterative Stack version. This is exactly the
  recursion-to-explicit-stack rule in [[csharp-iteration]].
- **Iterative DFS visit order.** Pushing neighbors in reverse makes the iterative
  order match the recursive (left-to-right) order; if you do not care about order,
  push them in natural order.

## See also

- [[csharp-iteration]] - Queue/Stack idioms, loops vs recursion, recursion depth.
- [[dsa-sorting]] - the other DSA reference; merge sort also recurses.
- [[dsa-stack-queue]] - the Queue and Stack types these traversals depend on.
- [[dsa-graph]] - graph representations (adjacency list vs matrix).
