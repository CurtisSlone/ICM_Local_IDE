// icm - the ICM host console CLI.
//
//   icm open  <dir>                 load + summarize an ICM instance
//   icm chat  <dir>                 operator console (dispatcher; needs Ollama)
//   icm mcp   <dir>                 serve this ICM over MCP (stdio)
//   icm flow  <dir> <name> [in...]  run an authored workflow (flows/<name>.json)
//   icm validate <dir> <table>      run the oracle on schemas/<table>.json + samples/<table>.txt
//   icm gen   <dir> <prompt...>     one raw generate call (smoke-test the model seat)
//   icm selftest                    check the deterministic core (no model needed)
//
// OLLAMA_URL overrides the config's ollama_url.

using System;
using System.Collections.Generic;

namespace Icm
{
    internal static class Program
    {
        private const int GenTimeoutMs = 300000;
        private const int MaxProblemsShown = 40;

        private static string EffectiveUrl(Instance icm)
        {
            string env = Environment.GetEnvironmentVariable("OLLAMA_URL");
            return string.IsNullOrEmpty(env) ? icm.Config.OllamaUrl : env;
        }

        private static void CmdOpen(string dir)
        {
            Instance icm = Instance.Open(dir);
            Config c = icm.Config;
            Console.WriteLine("ICM '" + c.Name + "'  (" + c.Domain + ")");
            Console.WriteLine("  root      : " + icm.Root);
            string embed = string.IsNullOrEmpty(c.Models.Embed) ? "(none)" : c.Models.Embed;
            Console.WriteLine("  models    : generate=" + c.Models.Generate + " dispatch=" + c.DispatchModel() + " embed=" + embed);
            Console.WriteLine("  ollama    : " + EffectiveUrl(icm));
            if (icm.Manifest != null)
            {
                Console.WriteLine("  kb entries: " + icm.Manifest.Entries.Count);
                foreach (Entry e in icm.Manifest.Entries)
                    Console.WriteLine("    - " + e.Id.PadRight(16) + " " + e.Title);
            }
            else Console.WriteLine("  kb entries: (no manifest.json)");
            var names = new List<string>();
            foreach (Tool t in c.Tools) names.Add(t.Name);
            Console.WriteLine("  tools     : " + string.Join(", ", names.ToArray()));
        }

        private static void CmdValidate(string dir, string table)
        {
            Instance icm = Instance.Open(dir);
            TableSchema schema = TableSchema.Load(icm.SchemaPath(table));
            string data = icm.ReadFile(Conventions.SampleRel(table));
            var vr = new ValidateResult();
            vr.Table = table;
            vr.Problems = Oracle.ValidateTsv(schema, data, null);
            vr.Ok = vr.Problems.Count == 0;
            if (vr.Ok) Console.WriteLine(vr.ToText(MaxProblemsShown));
            else { Console.Error.WriteLine(vr.ToText(MaxProblemsShown)); Environment.Exit(2); }
        }

        private static void CmdGen(string dir, string prompt)
        {
            Instance icm = Instance.Open(dir);
            string outText = Ollama.Generate(EffectiveUrl(icm), icm.Config.Models.Generate, prompt, null, 0.3, GenTimeoutMs);
            Console.WriteLine(outText);
        }

        private static void CmdFlow(string dir, string name, string input)
        {
            Instance icm = Instance.Open(dir);
            var status = (Action<string>)delegate(string s) { Console.Error.WriteLine("  - " + s); };
            var disp = new Dispatcher(icm, EffectiveUrl(icm), status);
            Flow flow = Flow.Load(icm.FlowPath(name));
            var engine = new FlowEngine(icm, disp, status);
            Dictionary<string, object> state = engine.Run(flow, input);

            List<string> keys = FlowEngine.ResultKeys(flow);
            if (keys.Count == 0) keys = new List<string>(new string[] { "answer", "row", "verdict", "output", "text" });
            foreach (string k in keys)
            {
                object v;
                if (state.TryGetValue(k, out v) && v != null) Console.WriteLine(v.ToString());
            }
        }

        private static string Arg(string[] args, int i) { return (i < args.Length) ? args[i] : null; }

        private static void Run(string[] args)
        {
            string cmd = args.Length > 0 ? args[0] : "";
            switch (cmd)
            {
                case "open":
                {
                    string dir = Arg(args, 1);
                    if (dir == null) throw new IcmError("usage: icm open <dir>");
                    CmdOpen(dir);
                    break;
                }
                case "validate":
                {
                    string dir = Arg(args, 1), table = Arg(args, 2);
                    if (dir == null || table == null) throw new IcmError("usage: icm validate <dir> <table>");
                    CmdValidate(dir, table);
                    break;
                }
                case "gen":
                {
                    string dir = Arg(args, 1);
                    if (dir == null) throw new IcmError("usage: icm gen <dir> <prompt...>");
                    string prompt = (args.Length > 2) ? string.Join(" ", args, 2, args.Length - 2) : "";
                    if (prompt.Length == 0) throw new IcmError("usage: icm gen <dir> <prompt...>");
                    CmdGen(dir, prompt);
                    break;
                }
                case "chat":
                {
                    string dir = Arg(args, 1);
                    if (dir == null) throw new IcmError("usage: icm chat <dir>");
                    Instance icm = Instance.Open(dir);
                    ConsoleChat.Run(icm, EffectiveUrl(icm));
                    break;
                }
                case "mcp":
                {
                    string dir = Arg(args, 1);
                    if (dir == null) throw new IcmError("usage: icm mcp <dir>");
                    Instance icm = Instance.Open(dir);
                    Mcp.Serve(icm, EffectiveUrl(icm));
                    break;
                }
                case "flow":
                {
                    string dir = Arg(args, 1), name = Arg(args, 2);
                    if (dir == null || name == null) throw new IcmError("usage: icm flow <dir> <name> [input...]");
                    string input = (args.Length > 3) ? string.Join(" ", args, 3, args.Length - 3) : "";
                    CmdFlow(dir, name, input);
                    break;
                }
                case "selftest":
                    if (SelfTest.RunAll() != 0) Environment.Exit(2);
                    break;
                case "":
                case "-h":
                case "--help":
                case "help":
                    Console.WriteLine(Usage);
                    break;
                default:
                    throw new IcmError("unknown command '" + cmd + "'\n\n" + Usage);
            }
        }

        private const string Usage =
            "icm - ICM host\n" +
            "  icm open  <dir>                 load + summarize an ICM instance\n" +
            "  icm chat  <dir>                 operator console (dispatcher; needs Ollama)\n" +
            "  icm mcp   <dir>                 serve this ICM over MCP (stdio)\n" +
            "  icm flow  <dir> <name> [in...]  run an authored workflow (flows/<name>.json)\n" +
            "  icm validate <dir> <table>      run the oracle on a table\n" +
            "  icm gen   <dir> <prompt...>     one raw generate call\n" +
            "  icm selftest                    check the deterministic core (no model)\n" +
            "\n" +
            "  env OLLAMA_URL overrides the config ollama_url";

        private static int Main(string[] args)
        {
            try { Run(args); return 0; }
            catch (IcmError e) { Console.Error.WriteLine("error: " + e.Message); return 1; }
        }
    }
}
