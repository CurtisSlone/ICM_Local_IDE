<!--icm
{
  "id": "in-box-assemblies",
  "title": "Importable in-box assemblies (no SDK / NuGet)",
  "doc_type": "reference",
  "summary": "Which .NET Framework assemblies the in-box csc can reference here, what each unlocks, and the exact -reference: line - plus the high-value ones (WMI, zip, GDI+, WMI) and what is NOT present (WPF, ValueTask, Channels, Dataflow).",
  "keywords": ["reference assembly", "DllImport", "csc reference", "System.Management", "WMI", "System.IO.Compression", "JavaScriptSerializer", "System.Drawing", "WPF", "ValueTask", "no NuGet", "GAC"],
  "group": "dotnet",
  "source": { "origin": "authored", "verified": "enumerated against C:/Windows/Microsoft.NET/Framework64/v4.0.30319 on this machine", "note": "the dir the in-box csc resolves simple reference names from" }
}
-->
# Importable in-box assemblies

No SDK, no NuGet, no MSBuild here - you compile with the in-box .NET Framework `csc` and reference
assemblies that already ship in Windows. `csc` resolves a simple name (e.g. `-reference:System.Drawing.dll`)
from the framework directory; `mscorlib.dll` is referenced automatically. See `csc-build` for the
invocation.

## High-value assemblies (verified present on this machine)

| `-reference:` | Namespace / key types | Use |
| --- | --- | --- |
| (automatic) `mscorlib.dll` | `System`, `System.IO`, `System.Collections.Generic`, `System.Collections.Concurrent`, `System.Threading(.Tasks)`, `System.Runtime.InteropServices` (`Marshal`, `DllImport`), `Microsoft.Win32` (`Registry`), `System.Security.Cryptography` | core runtime, P/Invoke, registry, hashing |
| `System.dll` | `System.Diagnostics.Process`, `System.Text.RegularExpressions`, `System.Net`, `System.ComponentModel` | process control, regex, sockets |
| `System.Core.dll` | LINQ (`System.Linq`), PLINQ (`AsParallel`) | queries, parallel queries |
| `System.Windows.Forms.dll` + `System.Drawing.dll` | WinForms; GDI+ (`Graphics`, `Bitmap`, `Pen`) | tool GUIs; read/convert textures, sprites, draw a hex view |
| `System.Windows.Forms.DataVisualization.dll` | `System.Windows.Forms.DataVisualization.Charting.Chart` | charts: byte histograms, offset maps |
| `System.Web.Extensions.dll` | `System.Web.Script.Serialization.JavaScriptSerializer` | JSON (the host uses this) |
| `System.Xml.dll`, `System.Xml.Linq.dll` | `XmlDocument`, `XmlSerializer`; `XDocument` (LINQ-to-XML) | XML config/manifests |
| `System.Runtime.Serialization.dll` | `DataContractJsonSerializer`, `DataContractSerializer` | alt JSON/XML serializers |
| `System.Management.dll` | `System.Management` - WMI (`ManagementObjectSearcher`, `Win32_Process`) | enumerate processes/modules/hardware without raw Win32 |
| `System.IO.Compression.dll` + `System.IO.Compression.FileSystem.dll` | `ZipArchive`, `ZipFile`, `DeflateStream` | game asset archives, packaging |
| `Microsoft.VisualBasic.dll` | `Microsoft.VisualBasic.Interaction.InputBox`, `FileSystem` | quick prompts from C# |
| `System.Net.Http.dll` | `HttpClient` | fetch tools/updates |
| `System.Numerics.dll` | `BigInteger`, `Complex`, `Vector` | checksums, math-heavy parsing |
| `System.ServiceProcess.dll` | `ServiceController`, `ServiceBase` | query/control Windows services |
| `System.Data.dll` | ADO.NET (`SqlConnection`, `DataTable`) | databases (SqlClient is in-box; SQLite is not) |
| `System.Configuration.dll`, `System.Transactions.dll`, `System.Security.dll` | config, transactions, extra crypto | as needed |

## NOT present here (do not reference - they need NuGet)

- **WPF**: `PresentationFramework.dll`, `PresentationCore.dll`, `WindowsBase.dll` are not in this
  reference dir. Use **WinForms** for GUI (see `host-conventions` and the WinForms scaffold).
- `System.Threading.Tasks.Extensions.dll` (`ValueTask`), `System.Threading.Channels.dll`
  (`Channel<T>`), `System.Threading.Tasks.Dataflow.dll` (TPL Dataflow) - see `concurrency-inbox` for
  the in-box substitutes.
- Anything from NuGet (Newtonsoft.Json, System.Text.Json, Dapper, SQLite, etc.).

## Rule

If a type lives only in a NuGet package or in .NET Core / .NET 5+, it will not resolve here. When
unsure whether an assembly is importable, check the framework dir
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319`) before relying on it.
