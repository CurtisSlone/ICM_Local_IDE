<!--icm
{
  "id": "patch-bytes-at-offset",
  "title": "Patch bytes at an offset, with a backup",
  "doc_type": "recipe",
  "group": "file-io",
  "summary": "Overwrite bytes at a known offset in a binary file after copying the original to a .bak - the safe write half of binary editing.",
  "keywords": ["patch bytes", "write at offset", "binary edit", "backup", "WriteAllBytes", "File.Copy", "hex patch"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc; compiled to verify" }
}
-->
# Patch bytes at an offset, with a backup

Apply a byte patch at a known offset. Always back up first - a wrong offset corrupts the file. Pair
this with `scan-bytes-in-file` to locate the offset.

## Code

```csharp
using System;
using System.IO;

namespace BytePatch
{
    public static class BytePatcher
    {
        // Overwrite bytes at offset after copying the original to path + ".bak".
        // Returns the backup path. Refuses a patch that would run past the end of the file.
        public static string PatchAt(string path, long offset, byte[] replacement)
        {
            byte[] data = File.ReadAllBytes(path);
            if (offset < 0 || offset + replacement.Length > data.Length)
                throw new ArgumentOutOfRangeException("offset", "patch would run past end of file");

            string backup = path + ".bak";
            File.Copy(path, backup, true);

            for (int i = 0; i < replacement.Length; i++)
                data[offset + i] = replacement[i];
            File.WriteAllBytes(path, data);
            return backup;
        }
    }
}
```

## Notes
- This rewrites the whole file. For surgical writes to a large file, open a `FileStream`, `Seek` to the
  offset, and write just the patch bytes.
- A same-length patch keeps every other offset stable; inserting or deleting bytes shifts everything
  after and usually breaks length/offset fields in the format.
- Keep the `.bak` until you have confirmed the patched file loads correctly.
