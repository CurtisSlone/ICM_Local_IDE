<!--icm
{
  "id": "drive-external-tool",
  "title": "Drive an external CLI tool and capture its output",
  "doc_type": "recipe",
  "group": "tooling",
  "summary": "Run an external executable (dumpbin, sigcheck, a Sysinternals tool) with arguments, capture stdout/stderr without deadlocking, and enforce a timeout - the pattern the host's own ToolRunner uses.",
  "keywords": ["Process", "ProcessStartInfo", "RedirectStandardOutput", "capture output", "stdout", "stderr", "timeout", "external tool", "deadlock", "drive cli"],
  "source": { "origin": "authored", "repo_path": "win_icm_code/src/Runtime/ToolRunner.cs", "note": "C# 5 / in-box csc (System.dll); mirrors the host's ToolRunner; compiled to verify" }
}
-->
# Drive an external CLI tool and capture its output

Run a command-line tool from C# and read what it prints. The one thing people get wrong: if you read
stdout to the end before the process exits, and the process fills the stderr pipe buffer (or vice
versa), both sides block forever. Drain BOTH pipes on background threads. This is exactly what the
host's `ToolRunner` does.

## Code

```csharp
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace RunTool
{
    public static class ToolDriver
    {
        // Run exe with args; capture stdout/stderr; enforce a timeout (ms). Returns the exit code
        // (-1 if it timed out or the code could not be read).
        public static int Run(string exe, string args, int timeoutMs, out string stdout, out string stderr)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            var so = new StringBuilder();
            var se = new StringBuilder();
            using (var p = new Process())
            {
                p.StartInfo = psi;
                p.Start();
                // Drain both pipes concurrently to avoid a buffer-full deadlock.
                Thread ot = new Thread(delegate() { try { so.Append(p.StandardOutput.ReadToEnd()); } catch { } });
                Thread et = new Thread(delegate() { try { se.Append(p.StandardError.ReadToEnd()); } catch { } });
                ot.IsBackground = true; et.IsBackground = true;
                ot.Start(); et.Start();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } }
                ot.Join(1000); et.Join(1000);
                stdout = so.ToString();
                stderr = se.ToString();
                try { return p.ExitCode; } catch { return -1; }
            }
        }
    }
}
```

## Notes
- Requires only `System.dll`.
- Pass arguments as a single `Arguments` string; quote any path with spaces. (The host's `ToolRunner`
  builds this with proper CommandLineToArgvW quoting - reuse that logic for untrusted args.)
- To run MANY tools at once without overwhelming the machine, gate them with a `SemaphoreSlim` - see the
  `throttle-with-semaphore` concurrency pattern.
- This is the same mechanism the ICM uses to run its own `command` tools; a tool that returns exit 0 is
  treated as success.
