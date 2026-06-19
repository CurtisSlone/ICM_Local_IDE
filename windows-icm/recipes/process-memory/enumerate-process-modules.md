<!--icm
{
  "id": "enumerate-process-modules",
  "title": "Enumerate a running process's loaded modules",
  "doc_type": "recipe",
  "group": "process-memory",
  "summary": "List the modules (DLLs/exe) a running process has loaded and their base addresses using System.Diagnostics.Process - the managed starting point for inspecting a target process.",
  "keywords": ["process", "modules", "base address", "ProcessModule", "GetProcessesByName", "MainModule", "WMI", "Win32_Process", "inspect process"],
  "source": { "origin": "authored", "note": "C# 5 / in-box csc (System.dll); compiled to verify" }
}
-->
# Enumerate a running process's loaded modules

The managed way to see what a target process has loaded and where. `ProcessModule.BaseAddress` is the
starting point for any further memory work.

## Code

```csharp
using System;
using System.Diagnostics;

namespace ProcInspect
{
    public static class ModuleLister
    {
        // Print each loaded module of the first process with this name, with base address and size.
        public static void List(string processName)
        {
            Process[] procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0) { Console.WriteLine("no process named " + processName); return; }
            Process p = procs[0];
            try
            {
                foreach (ProcessModule m in p.Modules)
                    Console.WriteLine(m.ModuleName + "  base=0x" + m.BaseAddress.ToString("X") +
                        "  size=" + m.ModuleMemorySize);
            }
            catch (Exception e)
            {
                // A 32-bit host cannot read a 64-bit target's modules (and vice versa); access can also
                // be denied. Match the target's bitness and run elevated if needed.
                Console.WriteLine("could not read modules: " + e.Message);
            }
        }
    }
}
```

## Notes
- Requires only `System.dll` (`System.Diagnostics`).
- Build your tool for the SAME bitness as the target (x64 here) or `p.Modules` throws.
- `p.MainModule.FileName` gives the executable path - useful with `Microsoft.Win32.Registry` to confirm
  an install location.
- For a system-wide view without opening each process, query WMI:
  `new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Process")` (reference
  `System.Management.dll`; see `in-box-assemblies`).
- Reading process MEMORY (not just module metadata) needs Win32 `OpenProcess` + `ReadProcessMemory` via
  P/Invoke - a separate recipe.
