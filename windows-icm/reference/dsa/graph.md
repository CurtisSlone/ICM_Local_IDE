<!--icm
{
  "id": "dsa-graph",
  "title": "Graph: adjacency-list representation and complexity",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "A graph is a set of vertices joined by edges; this reference uses an adjacency list (Dictionary<int, List<int>>) and covers directed vs undirected. Add-vertex and add-edge are O(1); listing one vertex's neighbours is O(degree); space is O(V + E). Iterative C# 5 reference implementation of an undirected graph.",
  "keywords": ["graph", "adjacency list", "adjacency matrix", "vertex", "edge", "directed", "undirected", "neighbours", "degree", "addVertex", "addEdge", "V + E", "Dictionary", "List"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Graph: adjacency-list representation and complexity

A graph is a set of **vertices** (nodes) joined by **edges** (connections). It models anything
relational: networks, maps, dependencies, social links. The two questions that shape every graph are
how edges are represented and whether they have direction.

## What it is

**Adjacency list.** Store, for each vertex, the list of vertices it connects to. Here that is a
`Dictionary<int, List<int>>`: the key is a vertex id, the value is its neighbour list. This is the
common, memory-lean representation for sparse graphs (few edges relative to vertices). The
alternative, an **adjacency matrix** (a `V x V` grid of booleans), gives O(1) edge existence checks
but costs O(V^2) space and is only worth it for dense graphs.

**Directed vs undirected.**

- **Undirected:** an edge between A and B goes both ways. Adding an edge appends B to A's list *and*
  A to B's list. (Friendship, a two-way road.) The implementation below is undirected.
- **Directed:** an edge from A to B is one-way. Adding it appends only B to A's list. (A follows B,
  a one-way street, a build dependency.) To make the code directed, drop the second `Add` in
  `AddEdge`.

Core operations: add a vertex (create an empty neighbour list), add an edge (link two vertices),
and read a vertex's neighbours (used by traversals).

## Big-O complexity

`V` is the number of vertices, `E` the number of edges. For adjacency list:

| Operation                  | Time      | Space (whole structure) |
|----------------------------|-----------|--------------------------|
| Add vertex                 | O(1)      | O(V + E)                 |
| Add edge                   | O(1)      | O(V + E)                 |
| List one vertex's neighbours | O(degree) | -                      |
| Check if edge A-B exists   | O(degree of A) | -                  |
| Whole-graph storage        | -         | O(V + E)                 |

Adjacency matrix differs: edge existence and edge add/remove are O(1), but storage is O(V^2) and
listing one vertex's neighbours is O(V) regardless of degree. Pick the list for sparse graphs, the
matrix for dense ones or when you check edge existence constantly.

## When to use / when not

Use an adjacency-list graph when:

- Relationships matter as much as the items: routing, dependencies, social/connection data, state
  machines.
- The graph is sparse (typical real-world graphs are), so per-vertex neighbour lists stay short.
- You traverse far more than you test single-edge existence (BFS/DFS, shortest path, reachability).

Prefer something else when:

- The relations form a strict tree/hierarchy with one parent each. A tree (see
  [[dsa-binary-search-tree]]) is simpler.
- The graph is small and dense and you constantly ask "is there an edge A-B?". An adjacency matrix
  answers in O(1).
- You only need key/value lookup with no relationships. A `Dictionary<K,V>` is enough.

## Reference implementation (C# 5)

Undirected graph over integer vertex ids, backed by an adjacency list. Add-vertex is idempotent and
add-edge guards against unknown vertices. Self-contained.

```csharp
using System;
using System.Collections.Generic;
using System.Text;

// Undirected graph backed by an adjacency list keyed by integer vertex id.
public class Graph
{
    private Dictionary<int, List<int>> adjacencyList;
    private int numberOfNodes;

    public Graph()
    {
        this.adjacencyList = new Dictionary<int, List<int>>();
        this.numberOfNodes = 0;
    }

    public int NumberOfNodes
    {
        get { return this.numberOfNodes; }
    }

    // Add a vertex. No-op if it already exists.
    public void AddVertex(int vertex)
    {
        if (!this.adjacencyList.ContainsKey(vertex))
        {
            this.adjacencyList.Add(vertex, new List<int>());
            this.numberOfNodes++;
        }
    }

    // Add an undirected edge between two existing vertices.
    // For a DIRECTED graph, drop the second Add.
    public void AddEdge(int vertex1, int vertex2)
    {
        if (!this.adjacencyList.ContainsKey(vertex1) || !this.adjacencyList.ContainsKey(vertex2))
        {
            return;
        }
        this.adjacencyList[vertex1].Add(vertex2);
        this.adjacencyList[vertex2].Add(vertex1);
    }

    // Return the neighbours of a vertex, or an empty list if it is unknown.
    public List<int> Neighbours(int vertex)
    {
        List<int> result;
        if (this.adjacencyList.TryGetValue(vertex, out result))
        {
            return result;
        }
        return new List<int>();
    }

    // Render the adjacency list as lines like "3-->1 4".
    public string Describe()
    {
        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<int, List<int>> pair in this.adjacencyList)
        {
            StringBuilder connections = new StringBuilder();
            foreach (int neighbour in pair.Value)
            {
                connections.Append(neighbour).Append(" ");
            }
            sb.Append(pair.Key).Append("-->").Append(connections.ToString().TrimEnd());
            sb.Append(Environment.NewLine);
        }
        return sb.ToString();
    }
}
```

## Pitfalls / notes

- **Add the vertex before the edge.** The original port indexed `adjacentList[value1]` directly,
  which throws `KeyNotFoundException` if either endpoint was never added. This version guards
  `AddEdge` and makes `AddVertex` idempotent so re-adding a vertex does not wipe its edges or
  inflate the count.
- **Undirected means two appends.** Forgetting the second `Add` silently makes the graph directed.
  Conversely, if you want directed, remove it on purpose, not by accident.
- **Parallel edges.** A plain `List<int>` lets you add the same edge twice. If you need at most one
  edge between a pair, check `Contains` first or use a `HashSet<int>` per vertex.
- **Traversal, not iteration, is where graphs get costly.** A BFS/DFS visits every vertex and edge,
  so it is O(V + E); do it with an explicit `Queue<int>` (BFS) or `Stack<int>` (DFS) plus a visited
  set rather than recursion, because a large or cyclic graph would recurse too deep and a missing
  visited check loops forever. See [[csharp-iteration]] on iteration vs recursion and bounding every
  loop, and [[dsa-stack-queue]] for the queue/stack the traversal uses.

## See also

- [[dsa-traversal]] - BFS and DFS over a graph.
- [[dsa-stack-queue]] - the queue (BFS) and stack (DFS) traversals rely on.
- [[csharp-iteration]] - iteration vs recursion, safe and bounded loops.
- [[dsa-binary-search-tree]] - a tree is a special, acyclic case of a graph.
