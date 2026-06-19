<!--icm
{
  "id": "scan-bytes-in-file",
  "title": "Scan a file for a byte pattern (all offsets)",
  "doc_type": "recipe",
  "group": "file-io",
  "summary": "Read a file as bytes and find every offset where a byte pattern occurs, plus a hex-string parser - the basis for locating data in a binary asset or save file.",
  "keywords": ["byte scan", "find bytes", "pattern search", "offset", "binary file", "ReadAllBytes", "hex string", "signature scan"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc; compiled to verify" }
}
-->
# Scan a file for a byte pattern

Find every offset of a byte sequence in a binary file. The naive scan below is fine for files that fit
in memory (most game assets and save files). For very large files, stream in chunks and carry the last
`needle.Length - 1` bytes across chunk boundaries.

## Code

```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace ByteScan
{
    public static class ByteScanner
    {
        // Every offset where needle occurs within the file at path.
        public static List<long> FindAll(string path, byte[] needle)
        {
            var hits = new List<long>();
            if (needle == null || needle.Length == 0) return hits;
            byte[] data = File.ReadAllBytes(path);
            int last = data.Length - needle.Length;
            for (int i = 0; i <= last; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (data[i + j] != needle[j]) { match = false; break; }
                }
                if (match) hits.Add(i);
            }
            return hits;
        }

        // Parse "DE AD BE EF" or "DEADBEEF" into bytes.
        public static byte[] ParseHex(string hex)
        {
            var bytes = new List<byte>();
            string clean = hex.Replace(" ", "").Replace("-", "");
            for (int i = 0; i + 1 < clean.Length; i += 2)
                bytes.Add(Convert.ToByte(clean.Substring(i, 2), 16));
            return bytes.ToArray();
        }
    }
}
```

## Notes
- `File.ReadAllBytes` loads the whole file; check the size first for multi-GB inputs.
- To search for ASCII text, get the needle with `System.Text.Encoding.ASCII.GetBytes("...")`.
- For huge files prefer `System.IO.MemoryMappedFiles.MemoryMappedFile` and scan a view.
- To scan in parallel over chunks, see the `parallel-aggregate` concurrency pattern.
