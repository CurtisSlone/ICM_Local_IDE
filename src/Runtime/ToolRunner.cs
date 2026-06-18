// ToolRunner - the instance tool/skill execution layer.
//
// An instance declares runnable tools in icm.config.json, each with a `command` (argv array) or a
// `script` (.ps1). The host runs the command with the instance root as the working directory,
// captures stdout/stderr/exit, and enforces a timeout. The GUARDRAIL: the command is authored by the
// instance, never by the model. The model (or a flow) only fills declared arguments, which are
// substituted into `{placeholder}` tokens. Because we pass an argv (not a shell string), there is no
// shell-injection surface. (Result type: ToolRunResult in Model/Results.cs.)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Icm
{
    internal static class ToolRunner
    {
        public static ToolRunResult Run(Instance icm, Tool tool, Dictionary<string, object> args)
        {
            var res = new ToolRunResult();
            List<string> cmd = tool.CommandTokens();
            if (cmd == null || cmd.Count == 0) { res.Error = "tool '" + tool.Name + "' declares no command/script"; return res; }
            if (args == null) args = new Dictionary<string, object>();

            var sub = new List<string>();
            foreach (string tok in cmd) sub.Add(Substitute(tok, args));

            var psi = new ProcessStartInfo();
            psi.FileName = sub[0];
            psi.Arguments = JoinArgs(sub, 1);
            psi.WorkingDirectory = icm.Root;     // sandbox: relative paths resolve under the instance
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;

            Dictionary<string, object> env = tool.EnvVars();
            if (env != null) foreach (var kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value == null ? "" : kv.Value.ToString();

            string stdinKey = tool.StdinArg();
            int timeoutMs = tool.TimeoutMs();

            try
            {
                using (var p = new Process())
                {
                    p.StartInfo = psi;
                    var so = new StringBuilder();
                    var se = new StringBuilder();
                    p.Start();
                    // Drain stdout/stderr on background threads to avoid pipe-buffer deadlock.
                    var ot = new Thread(delegate () { try { so.Append(p.StandardOutput.ReadToEnd()); } catch { } }); ot.IsBackground = true; ot.Start();
                    var et = new Thread(delegate () { try { se.Append(p.StandardError.ReadToEnd()); } catch { } }); et.IsBackground = true; et.Start();
                    try
                    {
                        // NOTE: Process.StandardInput's writer prepends a UTF-8 BOM preamble that the
                        // child sees as a leading character. Tools that read stdin should strip a
                        // leading U+FEFF (see windows-icm/tools/*.ps1). Kept simple here on purpose.
                        if (stdinKey != null && args.ContainsKey(stdinKey)) p.StandardInput.Write(Str(args[stdinKey]));
                        p.StandardInput.Close();
                    }
                    catch { }
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } res.TimedOut = true; }
                    ot.Join(1500); et.Join(1500);
                    res.Stdout = so.ToString();
                    res.Stderr = se.ToString();
                    res.ExitCode = res.TimedOut ? -1 : SafeExitCode(p);
                    res.Ok = !res.TimedOut && res.ExitCode == 0;
                }
            }
            catch (Exception e) { res.Error = "running '" + sub[0] + "': " + e.Message; return res; }

            var outSb = new StringBuilder();
            outSb.Append(res.Stdout.TrimEnd());
            if (res.Stderr.Trim().Length > 0) outSb.Append((outSb.Length > 0 ? "\n" : "") + "[stderr] " + res.Stderr.Trim());
            if (res.TimedOut) outSb.Append((outSb.Length > 0 ? "\n" : "") + "[timed out after " + timeoutMs + " ms]");
            if (!res.Ok && !res.TimedOut) outSb.Append((outSb.Length > 0 ? "\n" : "") + "[exit code " + res.ExitCode + "]");
            res.Output = outSb.ToString();
            return res;
        }

        private static int SafeExitCode(Process p) { try { return p.ExitCode; } catch { return -1; } }

        private static string Str(object o) { return o == null ? "" : o.ToString(); }

        // Replace {key} tokens with argument values. Unknown placeholders are left untouched.
        private static string Substitute(string token, Dictionary<string, object> args)
        {
            if (token.IndexOf('{') < 0) return token;
            string outv = token;
            foreach (var kv in args) outv = outv.Replace("{" + kv.Key + "}", Str(kv.Value));
            return outv;
        }

        // Build a Windows argument string from argv[start..], quoting per the CommandLineToArgvW
        // rules. (ProcessStartInfo.ArgumentList does not exist on .NET Framework 4.x.)
        private static string JoinArgs(List<string> argv, int start)
        {
            var sb = new StringBuilder();
            for (int i = start; i < argv.Count; i++)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(QuoteArg(argv[i]));
            }
            return sb.ToString();
        }

        public static string QuoteArg(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '"' }) < 0) return a;
            var sb = new StringBuilder();
            sb.Append('"');
            for (int i = 0; i < a.Length; i++)
            {
                int bs = 0;
                while (i < a.Length && a[i] == '\\') { bs++; i++; }
                if (i == a.Length) { sb.Append('\\', bs * 2); break; }
                else if (a[i] == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); }
                else { sb.Append('\\', bs); sb.Append(a[i]); }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
