<!--icm
{
  "id": "pinvoke-wrapper",
  "title": "P/Invoke wrapper class (DllImport + managed wrapper)",
  "doc_type": "scaffold",
  "group": "csharp",
  "summary": "A P/Invoke wrapper: a private [DllImport] native declaration plus a clean public managed method, shown with a harmless kernel32 Beep and a SafeHandle-based file-handle example with proper release.",
  "keywords": ["P/Invoke", "DllImport", "kernel32", "Beep", "SafeHandle", "SafeFileHandle", "CreateFile", "Marshal", "GetLastWin32Error", "wrapper", "C# 5", "interop"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc; verified to compile/parse" }
}
-->
# P/Invoke wrapper class (DllImport + managed wrapper)

The shape to copy when calling a native Win32 function: keep the raw `[DllImport]` declaration
`private` and expose a clean managed method that validates input and translates failures into
exceptions. P/Invoke lives in `System.Runtime.InteropServices` (in `mscorlib`, no extra reference).

## Files

`Native.cs` - two examples. First, the minimal case: a harmless `kernel32 Beep` with no handle to
manage. Second, the handle-owning case done right with a `SafeHandle` (preferred over a raw `IntPtr`
plus finalizer - see `disposable-pattern`).

```csharp
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Interop
{
    public static class NativeAudio
    {
        // Raw signature stays private. EntryPoint binds the native name while the managed name
        // differs; SetLastError so the wrapper can read the error code.
        [DllImport("kernel32.dll", EntryPoint = "Beep", SetLastError = true)]
        private static extern bool NativeBeep(uint dwFreq, uint dwDuration);

        // Clean managed wrapper: validate, call, translate failure into an exception.
        public static void Beep(uint frequency, uint durationMs)
        {
            if (frequency < 37 || frequency > 32767)
            {
                throw new ArgumentOutOfRangeException("frequency", "must be 37..32767 Hz");
            }
            if (!NativeBeep(frequency, durationMs))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    public static class NativeFile
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint OPEN_EXISTING = 3;

        // Returning a SafeFileHandle (not IntPtr) means the runtime releases the handle for us:
        // CloseHandle runs in the SafeHandle's critical finalizer even if the caller forgets.
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        // Open a file for read; caller owns the returned handle and disposes it (using).
        public static SafeFileHandle OpenForRead(string path)
        {
            if (string.IsNullOrEmpty(path)) { throw new ArgumentException("path required", "path"); }

            SafeFileHandle h = CreateFile(path, GENERIC_READ, 1 /* FILE_SHARE_READ */,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return h;
        }
    }
}
```

Caller usage (the `SafeHandle` makes cleanup deterministic with `using`):

```csharp
NativeAudio.Beep(800, 200);

using (var h = NativeFile.OpenForRead(@"C:\Windows\win.ini"))
{
    // h.DangerousGetHandle() / pass to a FileStream(SafeFileHandle, ...) etc.
}   // CloseHandle runs here, even on exception
```

## Build

It is just a class - compile as a library (or `-t:exe` if you add a `Main`). `System.ComponentModel`
(for `Win32Exception`) and `Microsoft.Win32.SafeHandles` are both in `mscorlib`/`System.dll`, so no
extra reference beyond the usual two:

```
csc.exe -nologo -noconfig -optimize+ -langversion:5 -target:library ^
    -reference:System.dll -reference:System.Core.dll ^
    -out:Interop.dll Native.cs
```

## Notes

- **Keep the `[DllImport]` declaration `private`** and expose only the validated managed method - the
  raw signature (unsigned types, `IntPtr`, no validation) is not a good public API.
- **`SetLastError = true`** is required before `Marshal.GetLastWin32Error()` means anything; wrap the
  code in a `Win32Exception` for a readable message.
- **Prefer `SafeHandle`** over `IntPtr` for any handle you must close: the runtime guarantees release
  even if the caller forgets, and it is robust against async exceptions. Only fall back to `IntPtr` +
  the full Dispose pattern when no `SafeHandle` subclass fits (see `disposable-pattern`).
- **`CharSet = CharSet.Unicode`** on string-taking APIs binds the `...W` entry point directly. Match
  the native unsigned types (`uint`, not `int`) so marshaling is correct.
- C# 5: no `?.`, no interpolation; throw with the parameter name as the second `ArgumentException`
  argument as shown.
