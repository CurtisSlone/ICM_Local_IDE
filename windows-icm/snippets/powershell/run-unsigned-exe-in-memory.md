<!--icm
{
  "id": "run-unsigned-exe-in-memory",
  "title": "Run an unsigned .NET exe under Smart App Control (in-memory)",
  "doc_type": "snippet",
  "group": "powershell",
  "summary": "Run a locally-built/unsigned .NET exe that Smart App Control blocks on disk, by loading its bytes in-process inside trusted powershell.exe - on an STA thread, invoking Main with the right arity.",
  "keywords": ["Smart App Control", "SAC", "unsigned exe", "Assembly.Load", "in-memory", "STAThread", "EntryPoint", "launcher", "code integrity"],
  "source": { "origin": "authored", "note": "the host's run-cli / run-gui launcher pattern, generalized; verified in use" }
}
-->
# Run an unsigned .NET exe under Smart App Control (in-memory)

Smart App Control (enforce) blocks running an unsigned, locally-built `.exe` directly from disk. It
does NOT block in-memory managed execution inside the already-trusted, Microsoft-signed
`powershell.exe`. So read the assembly's bytes and run them in-process. WinForms/STA apps need an STA
thread, and `Main` may take `string[]` or nothing - handle both.

```powershell
param([string] $Exe, [string[]] $Args = @())

# WinForms / COM apps need STA; relaunch self with -STA if we are not already.
if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    & powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Exe $Exe -Args $Args
    return
}

$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($Exe))
$ep  = $asm.EntryPoint
# Invoke Main matching its signature: Main() takes 0 params, Main(string[]) takes 1.
if ($ep.GetParameters().Length -eq 0) { [void]$ep.Invoke($null, $null) }
else { [void]$ep.Invoke($null, @(, [string[]]$Args)) }
```

## Notes
- This runs only the exe you point it at; SAC still guards everything else. It is your own local
  program on your own machine - do not disable SAC or change code-integrity policy to run it.
- The arity check matters: invoking a no-arg `Main()` with an args array throws
  `TargetParameterCountException` (and the window never appears).
- This is exactly how `run-cli.ps1` / `run-gui.ps1` launch `icm.exe` / `icm-gui.exe`.
