// icm - the ICM host console CLI.
//
//   icm open  <dir>                 load + summarize an ICM instance
//   icm chat  <dir>                 operator console (dispatcher; needs Ollama)
//   icm mcp   <dir>                 serve this ICM over MCP (stdio)
//   icm flow  <dir> <name> [in...]  run an authored workflow (flows/<name>.json)
//   icm validate <dir> <table>      run the oracle on schemas/<table>.json + samples/<table>.txt
//   icm docsearch <dir> <corpus> <query...>   hybrid search a built refdocs corpus
//   icm reindex <dir>               regenerate manifest.json from files' <!--icm--> metadata blocks
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
                {
                    string g = e.Group.Length > 0 ? "[" + e.Group + "] " : "";
                    Console.WriteLine("    - " + e.Id.PadRight(22) + " " + g + e.Title);
                }
            }
            else Console.WriteLine("  kb entries: (no manifest.json)");
            var names = new List<string>();
            foreach (Tool t in c.Tools) names.Add(t.Name);
            Console.WriteLine("  tools     : " + string.Join(", ", names.ToArray()));
        }

        private static void CmdList(string dir, string group, string type, bool asJson)
        {
            Instance icm = Instance.Open(dir);
            if (icm.Manifest == null) { Console.Error.WriteLine("no manifest.json in " + icm.Root); return; }
            var entries = new List<Entry>();
            foreach (Entry e in icm.Manifest.Entries)
            {
                if (group != null && !string.Equals(e.Group, group, StringComparison.OrdinalIgnoreCase)) continue;
                if (type != null && !string.Equals(e.DocType, type, StringComparison.OrdinalIgnoreCase)) continue;
                entries.Add(e);
            }
            if (asJson)
            {
                var arr = new List<object>();
                foreach (Entry e in entries)
                    arr.Add(Json.Obj("id", e.Id, "title", e.Title, "group", e.Group, "doc_type", e.DocType, "path", e.Path, "summary", e.Summary, "keywords", e.Keywords.ToArray()));
                Console.WriteLine(Json.SerializePretty(arr.ToArray()));
                return;
            }
            entries.Sort(delegate(Entry a, Entry b)
            {
                int g = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
                return g != 0 ? g : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });
            string cur = " ";
            foreach (Entry e in entries)
            {
                string g = e.Group.Length > 0 ? e.Group : "(top level)";
                if (g != cur) { Console.WriteLine((cur == " " ? "" : "\n") + "[" + g + "]"); cur = g; }
                Console.WriteLine("  " + e.Id.PadRight(24) + " " + e.Summary);
            }
            Console.WriteLine("\n" + entries.Count + " entr" + (entries.Count == 1 ? "y" : "ies"));
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

        // The set of recognized verbs; anything else that names a directory is the `icm <dir>` shorthand.
        private static bool IsCommand(string s)
        {
            switch (s)
            {
                case "open": case "chat": case "mcp": case "flow": case "validate":
                case "docsearch": case "reindex": case "list": case "flows": case "gen": case "selftest":
                case "help": case "-h": case "--help": return true;
                default: return false;
            }
        }

        private static void Run(string[] args)
        {
            string cmd = args.Length > 0 ? args[0] : "";

            // VSCode-style shorthand: `icm <dir>` opens the operator console on that directory. The path
            // is relative to the terminal's working directory, or absolute. Recognized commands win.
            if (cmd.Length > 0 && !IsCommand(cmd) && System.IO.Directory.Exists(cmd))
            {
                Instance icm = Instance.Open(cmd);
                ConsoleChat.Run(icm, EffectiveUrl(icm));
                return;
            }

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
                case "docsearch":
                {
                    string dir = Arg(args, 1), corpus = Arg(args, 2);
                    if (dir == null || corpus == null) throw new IcmError("usage: icm docsearch <dir> <corpus> [-k N] [--no-embed] <query...>");
                    int kk = 5; bool emb = true; var qparts = new List<string>();
                    for (int i = 3; i < args.Length; i++)
                    {
                        if (args[i] == "-k" && i + 1 < args.Length) { int.TryParse(args[++i], out kk); }
                        else if (args[i] == "--no-embed") { emb = false; }
                        else qparts.Add(args[i]);
                    }
                    if (qparts.Count == 0) throw new IcmError("usage: icm docsearch <dir> <corpus> [-k N] [--no-embed] <query...>");
                    Instance icm = Instance.Open(dir);
                    string embedModel = string.IsNullOrEmpty(icm.Config.Models.Embed) ? "nomic-embed-text" : icm.Config.Models.Embed;
                    var status = (Action<string>)delegate(string s) { Console.Error.WriteLine("  - " + s); };
                    Console.WriteLine(Search.Run(icm, EffectiveUrl(icm), corpus, string.Join(" ", qparts.ToArray()), kk, emb, embedModel, status));
                    break;
                }
                case "reindex":
                {
                    string dir = Arg(args, 1);
                    if (dir == null) throw new IcmError("usage: icm reindex <dir>");
                    Instance icm = Instance.Open(dir);
                    Indexer.Reindex(icm, delegate(string s) { Console.Error.WriteLine("  - " + s); });
                    break;
                }
                case "flows":
                {
                    string dir = Arg(args, 1);
                    if (dir == null) throw new IcmError("usage: icm flows <dir>");
                    Instance icmF = Instance.Open(dir);
                    string fdir = System.IO.Path.Combine(icmF.Root, Conventions.FlowsDir);
                    if (!System.IO.Directory.Exists(fdir)) { Console.WriteLine("(no flows/ dir)"); break; }
                    string[] ff = System.IO.Directory.GetFiles(fdir, "*.json"); System.Array.Sort(ff, StringComparer.OrdinalIgnoreCase);
                    foreach (string f in ff)
                    {
                        try { Flow fl = Flow.Load(f); Console.WriteLine("  " + System.IO.Path.GetFileNameWithoutExtension(f).PadRight(18) + " " + fl.WhenToUse); }
                        catch (IcmError) { }
                    }
                    break;
                }
                case "list":
                {
                    string dir = Arg(args, 1);
                    if (dir == null) throw new IcmError("usage: icm list <dir> [--group G] [--type T] [--json]");
                    string group = null, type = null; bool asJson = false;
                    for (int i = 2; i < args.Length; i++)
                    {
                        if (args[i] == "--group" && i + 1 < args.Length) group = args[++i];
                        else if (args[i] == "--type" && i + 1 < args.Length) type = args[++i];
                        else if (args[i] == "--json") asJson = true;
                    }
                    CmdList(dir, group, type, asJson);
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
            "  icm <dir>                       open the operator console on an ICM dir (VSCode-style; rel or abs)\n" +
            "  icm open  <dir>                 load + summarize an ICM instance\n" +
            "  icm chat  <dir>                 operator console (dispatcher; needs Ollama)\n" +
            "  icm mcp   <dir>                 serve this ICM over MCP (stdio)\n" +
            "  icm flow  <dir> <name> [in...]  run an authored workflow (flows/<name>.json)\n" +
            "  icm validate <dir> <table>      run the oracle on a table\n" +
            "  icm docsearch <dir> <corpus> <query...>   hybrid search a built refdocs corpus\n" +
            "  icm reindex <dir>               regenerate manifest.json from files' <!--icm--> blocks\n" +
            "  icm list  <dir> [--group G] [--type T] [--json]   enumerate the KB catalog\n" +
            "  icm flows <dir>                 list the instance's workflows (the router's menu)\n" +
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
