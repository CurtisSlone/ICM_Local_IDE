<!--icm
{
  "id": "dsa-hash-table",
  "title": "Hash tables: operations, complexity, common problems",
  "doc_type": "reference",
  "group": "dsa",
  "summary": "Separate-chaining hash table with Big-O, plus the first-recurring-value problem, as compile-verified C# 5.",
  "keywords": ["hash table", "hashtable", "dictionary", "hash map", "separate chaining", "collision", "hash function", "set", "get", "keys", "first recurring character", "first duplicate", "big-o", "amortized"],
  "source": { "origin": "ported", "url": "local repo: data-structures-and-algorithms", "note": "corrected + adapted to C# 5; compile-verified with csc_check" }
}
-->
# Hash tables: operations, complexity, common problems

A hash table maps keys to values with average O(1) insert, lookup, and delete. It hashes the key to a
bucket index; collisions (different keys landing in the same bucket) are resolved here by separate
chaining - each bucket holds a small list of entries. In production use `Dictionary<K,V>`; this entry
shows the mechanics and the classic first-recurring-value problem.

## Core operations

- Set (insert or update): hash the key, find the bucket, update if the key is present else append.
- Get: hash the key, scan that bucket's chain for the key.
- Keys: walk every bucket and collect each entry's key.
- Collisions are expected; a good hash function spreads keys so chains stay short.

## Big-O complexity

| Operation | Average | Worst case | Space |
|---|---|---|---|
| Set (insert/update) | O(1) | O(n) | O(1) |
| Get (lookup) | O(1) | O(n) | O(1) |
| Delete | O(1) | O(n) | O(1) |
| Keys (enumerate all) | O(n) | O(n) | O(n) |
| First recurring value (len n) | O(n) | O(n) | O(n) |
| Whole table | - | - | O(n) |

Worst case O(n) happens when many keys collide into one bucket (a bad hash function or adversarial
keys), degrading a bucket to a linear scan.

## When to use / when not

- Use a hash table when you look up, insert, or check membership by key and order does not matter.
  This turns repeated O(n) searches into O(1) average.
- Use it to dedupe or detect repeats in one pass: track what you have seen (the first-recurring
  problem below).
- Avoid it when you need sorted order or range queries: a hash table has no ordering. Use a sorted
  structure or sort the keys.
- Avoid it for tiny fixed collections where a plain array scan is simpler and cache-friendlier.
- When you index by position rather than key, use an array: see [[dsa-array]].

## Reference implementation (C# 5)

```csharp
using System;
using System.Collections.Generic;

// A separate-chaining hash table mapping string keys to int values.
public class HashTable
{
    private class Entry
    {
        public string Key;
        public int Value;

        public Entry(string key, int value)
        {
            this.Key = key;
            this.Value = value;
        }
    }

    private List<Entry>[] buckets;

    public HashTable(int size)
    {
        if (size < 1) { size = 1; }
        this.buckets = new List<Entry>[size];
    }

    private int Hash(string key)
    {
        int hash = 0;
        for (int i = 0; i < key.Length; i++)
        {
            // Keep the running hash non-negative and inside the bucket range.
            hash = (hash * 31 + key[i]) % this.buckets.Length;
            if (hash < 0) { hash += this.buckets.Length; }
        }
        return hash;
    }

    // Insert or update the value for a key.
    public void Set(string key, int value)
    {
        int index = Hash(key);
        if (this.buckets[index] == null)
        {
            this.buckets[index] = new List<Entry>();
        }
        List<Entry> chain = this.buckets[index];
        for (int i = 0; i < chain.Count; i++)
        {
            if (chain[i].Key == key)
            {
                chain[i].Value = value; // update existing
                return;
            }
        }
        chain.Add(new Entry(key, value));
    }

    // Returns true and sets value via out param if the key exists.
    public bool TryGet(string key, out int value)
    {
        int index = Hash(key);
        List<Entry> chain = this.buckets[index];
        if (chain != null)
        {
            foreach (Entry e in chain)
            {
                if (e.Key == key)
                {
                    value = e.Value;
                    return true;
                }
            }
        }
        value = 0;
        return false;
    }

    public List<string> Keys()
    {
        List<string> result = new List<string>();
        for (int i = 0; i < this.buckets.Length; i++)
        {
            List<Entry> chain = this.buckets[i];
            if (chain != null)
            {
                foreach (Entry e in chain)
                {
                    result.Add(e.Key);
                }
            }
        }
        return result;
    }
}

public static class HashProblems
{
    // First value that appears twice, scanning left to right. Returns true if found.
    public static bool FirstRecurring(int[] arr, out int found)
    {
        Dictionary<int, bool> seen = new Dictionary<int, bool>();
        if (arr != null)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (seen.ContainsKey(arr[i]))
                {
                    found = arr[i];
                    return true;
                }
                seen[arr[i]] = true;
            }
        }
        found = 0;
        return false;
    }

    public static bool FirstRecurringChar(string s, out char found)
    {
        Dictionary<char, bool> seen = new Dictionary<char, bool>();
        if (s != null)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (seen.ContainsKey(s[i]))
                {
                    found = s[i];
                    return true;
                }
                seen[s[i]] = true;
            }
        }
        found = '\0';
        return false;
    }
}
```

## Pitfalls / notes

- The original `hash` multiplied the char code by its index `i`, so the first character (index 0)
  contributed nothing and the spread was poor; a `hash * 31 + char` rolling hash is the standard
  choice. Guard against a negative result before indexing.
- The original `set` always appended, so setting the same key twice stored two entries and `get`
  returned only the first - updates were broken. Scan the chain and update in place.
- The original `keys` looped the inner index up to `length` (the table size) instead of the chain's
  own count, throwing or reading wrong slots. Iterate each chain by its actual `Count`, and skip
  null (empty) buckets.
- Returning a sentinel like `0` to mean "absent" is ambiguous when `0` is a real value. Prefer the
  `TryGet`/`out` pattern so absence is unambiguous.
- For first-recurring, a hash set gives one O(n) pass and O(n) space; the brute-force nested-loop
  alternative is O(n^2) time and O(1) space - trade memory for speed. The original used the legacy
  non-generic `System.Collections.Hashtable`; prefer the generic `Dictionary<K,V>` (or `HashSet<T>`)
  for type safety and no boxing. See [[csharp-iteration]] for the loop and safe-iteration rules.

## See also

- [[dsa-array]] - index-by-position container and the merge / reverse problems.
- [[csharp-iteration]] - loop choice, bounds, and not mutating a collection during `foreach`.
- [[csharp-control-flow]] - guard clauses and the `out`-param pattern.
