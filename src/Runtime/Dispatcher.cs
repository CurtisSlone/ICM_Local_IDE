// The dispatcher: a chat-style front end that is a constrained ROUTER underneath. Reusable across
// the console REPL (Cli/ConsoleChat.cs), the WinForms GUI, the MCP server, and the flow engine.
//
// The DEVLOG three-layer design ("Wrapping the dispatcher in a real chat"):
//   CONVERSATION (Rewrite)  small model, NON-load-bearing: rewrites a follow-up into a standalone request.
//   DISPATCHER   (Classify) ONE constrained classify call per turn -> {intent, query}.
//   ICM AGENT    (Do*)      the heavy generate seat + the oracle; results are grounded.
// Nothing the conversation layer says is load-bearing: the routing decision is the dispatcher's
// constrained pick and correctness is the oracle's verdict.
//
// Threading: a Dispatcher is single-threaded per use - one in-flight operation at a time (the
// `cancel` handle is reassigned per call). Front ends serialize their calls (e.g. the GUI disables
// send during a turn).

using System;
using System.Collections.Generic;
using System.Text;

namespace Icm
{
    internal class Dispatcher
    {
        private const int DispatchTimeoutMs = 60000;
        private const int GenTimeoutMs = 300000;
        private const int RewriteTimeoutMs = 30000;
        private const int MaxHistory = 6;        // turns kept for coreference rewrite
        private const int MaxProposeRepairs = 4; // bounded repair on a failing proposed row
        private const int MaxProblemsShown = 40;

        private readonly Instance icm;
        private readonly string url;
        private readonly Action<string> status;
        private readonly List<string> history = new List<string>(); // "you: ..." / "icm: ..." lines
        private Cancel cancel;                                       // the in-flight op's cancel handle

        public Dispatcher(Instance icm, string url, Action<string> status)
        {
            this.icm = icm;
            this.url = url;
            this.status = status != null ? status : delegate(string s) { };
        }

        public Instance Icm { get { return icm; } }
        public string Url { get { return url; } }

        private void Status(string msg) { status(msg); }

        // Abort the in-flight operation's model call (best effort). Safe to call from another thread.
        public void CancelCurrent() { Cancel c = cancel; if (c != null) c.Abort(); }

        // One turn: conversation rewrite (if there is history) -> classify -> run capability.
        // Never throws for model/oracle failures; it returns a TurnResult with IsError set.
        public TurnResult Turn(string line)
        {
            var r = new TurnResult();
            line = (line ?? "").Trim();
            r.Standalone = line;
            if (line.Length == 0) { r.Text = ""; return r; }
            cancel = new Cancel();

            if (history.Count > 0)
            {
                try
                {
                    string standalone = Rewrite(line);
                    if (!string.IsNullOrEmpty(standalone) && standalone.Trim() != line)
                    {
                        r.Standalone = standalone.Trim();
                        r.Rewritten = true;
                        Status("rewrite: read as \"" + r.Standalone + "\"");
                    }
                }
                catch (IcmError) { /* non-load-bearing: fall back to the raw line */ }
            }

            Status("dispatch: classifying");
            string intent, query;
            try { Classify(r.Standalone, out intent, out query); }
            catch (IcmError e) { r.Intent = "(error)"; r.IsError = true; r.Text = "dispatch failed: " + e.Message; return r; }
            r.Intent = intent; r.Query = query;
            Status("intent=" + intent + "  query=\"" + query + "\"");

            try
            {
                if (intent == Conventions.Intent.Quit) r.Text = "bye";
                else if (intent == Conventions.Intent.Help) r.Text = Help();
                else if (intent == Conventions.Intent.Ask) r.Text = DoAsk(query);
                else if (intent == Conventions.Intent.Make) r.Text = DoMake(query);
                else if (intent == Conventions.Intent.Validate) r.Text = Validate(ParseTable(query), null).ToText(MaxProblemsShown);
                else if (intent == Conventions.Intent.Propose) DoPropose(query, r);
                else r.Text = "unknown intent '" + intent + "' (try help)";
            }
            catch (IcmError e) { r.IsError = true; r.Text = "[error] " + e.Message; }

            Remember("you: " + line);
            Remember("icm: " + (r.IsError ? r.Text : Truncate(r.Text, 400)));
            return r;
        }

        private void Remember(string entry)
        {
            history.Add(entry);
            while (history.Count > MaxHistory) history.RemoveAt(0);
        }

        private static string Truncate(string s, int max)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max) + " ...";
        }

        // --- Layer 1: coreference rewrite (non-load-bearing) ---

        private string Rewrite(string line)
        {
            object schema = Json.Schema(Json.Obj("standalone", Json.StrProp()), "standalone");
            string prompt =
                "Rewrite the user's latest line into a single self-contained request, resolving any " +
                "pronouns or references (it, that, the previous one) using the conversation so far. " +
                "If the line is already self-contained, return it unchanged. Do not answer it.\n\n" +
                "Conversation so far:\n" + string.Join("\n", history.ToArray()) +
                "\n\nLatest line: " + line + "\n\nReturn JSON {standalone}.";
            Dictionary<string, object> v = Ollama.GenerateJson(url, icm.Config.DispatchModel(), prompt, schema, 0.1, RewriteTimeoutMs, cancel);
            return Json.GetStringOr(v, "standalone", line);
        }

        // --- Layer 2: the one constrained classify call ---

        private void Classify(string line, out string intent, out string query)
        {
            object schema = Json.Schema(Json.Obj("intent", Json.EnumProp(Conventions.Intent.All), "query", Json.StrProp()), "intent", "query");
            string prompt =
                "You are the command router for a local ICM operator console. Classify the operator's " +
                "line into exactly one intent and extract the core query.\n" +
                "Intents:\n" +
                "- ask: a question to answer from the knowledge base.\n" +
                "- propose: add or propose a new ROW for a data table (e.g. 'add a level 30 sorceress skill'); query = the description of the row.\n" +
                "- make: generate freeform text/code that is NOT a table row.\n" +
                "- validate: check a data table against its schema; query = the table name.\n" +
                "- help: they want to know what they can do here.\n" +
                "- quit: they want to exit.\n" +
                "Return JSON {intent, query}.\n\nOperator line: " + line;
            Dictionary<string, object> v = Ollama.GenerateJson(url, icm.Config.DispatchModel(), prompt, schema, 0.1, DispatchTimeoutMs, cancel);
            intent = Json.GetStringOr(v, "intent", Conventions.Intent.Help);
            query = Json.GetStringOr(v, "query", line);
        }

        // Constrained route to a KB entry id (or null). The grounding step for `ask`.
        private string Route(string query)
        {
            if (icm.Manifest == null || icm.Manifest.Entries.Count == 0) return null;

            var ids = new List<string>();
            var lines = new List<string>();
            foreach (Entry e in icm.Manifest.Entries)
            {
                ids.Add(e.Id);
                string grp = e.Group.Length > 0 ? " (" + e.Group + ")" : "";
                string kw = e.Keywords.Count > 0 ? "  [keywords: " + string.Join(", ", e.Keywords.ToArray()) + "]" : "";
                lines.Add("- " + e.Id + grp + " : " + e.Title + " - " + e.Summary + kw);
            }
            ids.Add("none");

            object schema = Json.Schema(Json.Obj("entry_id", Json.EnumProp(ids)), "entry_id");
            string prompt =
                "Pick the single KB entry whose content can answer the question, or 'none' if nothing " +
                "fits.\n\nIndex:\n" + string.Join("\n", lines.ToArray()) + "\n\nQuestion: " + query;
            Dictionary<string, object> v = Ollama.GenerateJson(url, icm.Config.DispatchModel(), prompt, schema, 0.1, DispatchTimeoutMs, cancel);
            string id = Json.GetStringOr(v, "entry_id", "none");
            return id == "none" ? null : id;
        }

        // --- Layer 3: capabilities (public wrappers so MCP and the flow engine reuse them) ---

        public string Ask(string query) { cancel = new Cancel(); return DoAsk(query); }
        public string RouteEntryId(string query) { cancel = new Cancel(); return Route(query); }

        public string Generate(string prompt, double temperature)
        {
            cancel = new Cancel();
            return Ollama.Generate(url, icm.Config.Models.Generate, prompt, null, temperature, GenTimeoutMs, cancel);
        }

        private string DoAsk(string query)
        {
            Status("route: picking a KB entry");
            string id = Route(query);
            if (id == null) return "That isn't covered in this ICM's knowledge base.";
            Status("read: kb entry '" + id + "'");
            string entry = icm.ReadEntry(id);
            string system;
            try { system = icm.ReadFile(Conventions.SystemFile); } catch (IcmError) { system = ""; }
            string prompt =
                system + "\n\nAnswer the question using ONLY the entry text below. If it does not contain " +
                "the answer, say so.\n\n--- ENTRY TEXT ---\n" + entry + "\n--- END ---\n\nQuestion: " + query;
            Status("answer: generating (grounded)");
            return Ollama.Generate(url, icm.Config.Models.Generate, prompt, null, 0.2, GenTimeoutMs, cancel);
        }

        private string DoMake(string query)
        {
            Status("make: generating a proposal");
            string prompt =
                "You are a careful assistant for the ICM domain: " + icm.Config.Domain +
                ". Produce what the operator asked for. Be concrete and minimal.\n\nRequest: " + query;
            return Ollama.Generate(url, icm.Config.Models.Generate, prompt, null, 0.3, GenTimeoutMs, cancel);
        }

        private static string ParseTable(string query)
        {
            string[] parts = query.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : query.Trim();
        }

        // Run the oracle on a table: against `tsv` if given, else samples/<table>.txt on disk.
        public ValidateResult Validate(string table, string tsv)
        {
            var res = new ValidateResult();
            res.Table = table;
            if (table.Length == 0) { res.Ok = false; res.Problems.Add(new Problem(0, "(table)", "no table name given")); return res; }
            TableSchema schema = TableSchema.Load(icm.SchemaPath(table));
            string data = tsv != null ? tsv : icm.ReadFile(Conventions.SampleRel(table));
            Status("oracle: validating '" + table + "' against its schema");
            res.Problems = Oracle.ValidateTsv(schema, data, null);
            res.Ok = res.Problems.Count == 0;
            return res;
        }

        // --- propose: model proposes a table row, the oracle gates it, bounded repair fixes it ---

        private void DoPropose(string query, TurnResult r)
        {
            string table = PickTable(query);
            if (table == null) { r.Text = "No table schemas to propose into (need schemas/<table>.json)."; r.IsError = true; return; }
            ProposeResult pr = ProposeRow(table, query);
            r.Text = FormatPropose(pr);
            r.IsError = !pr.Ok;
            if (pr.Ok) { r.ProposedTable = pr.Table; r.ProposedRow = pr.Row; }
        }

        public ProposeResult ProposeRow(string table, string request)
        {
            cancel = new Cancel();
            var res = new ProposeResult();
            res.Table = table;

            TableSchema schema;
            try { schema = TableSchema.Load(icm.SchemaPath(table)); }
            catch (IcmError e) { res.Error = e.Message; return res; }

            string header = TableHeader(table);
            if (header == null)
            {
                var names = new List<string>();
                foreach (ColSpec c in schema.Columns) names.Add(c.Name);
                header = string.Join("\t", names.ToArray());
            }
            res.Header = header;
            string[] cols = header.Split('\t');

            var props = new Dictionary<string, object>();
            foreach (string c in cols) props[c] = Json.StrProp();
            object genSchema = Json.Schema(props, cols);

            string basePrompt =
                "You are proposing exactly ONE new row for the tab-separated table '" + table +
                "' in the ICM domain: " + icm.Config.Domain + ".\n" +
                "Columns in order and their constraints:\n" + DescribeColumns(cols, schema) +
                ExampleBlock(table) +
                "Rules: give a value for EVERY column; numbers must be PLAIN digits only (no commas, " +
                "units, quotes, or thousands separators) and within any stated range; booleans as 0 or 1; " +
                "enum columns must be EXACTLY one of the listed values.\n" +
                "Request: " + request + "\nReturn JSON with one field per column name.";

            string prompt = basePrompt;
            for (int attempt = 0; attempt <= MaxProposeRepairs; attempt++)
            {
                Status("propose: generating row (attempt " + (attempt + 1) + ")");
                Dictionary<string, object> v;
                try { v = Ollama.GenerateJson(url, icm.Config.Models.Generate, prompt, genSchema, attempt == 0 ? 0.2 : 0.3, GenTimeoutMs, cancel); }
                catch (IcmError e) { res.Error = e.Message; return res; }

                var cells = new List<string>();
                foreach (string c in cols)
                {
                    string val = Json.GetStringOr(v, c, "");
                    val = val.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
                    cells.Add(val);
                }
                string row = string.Join("\t", cells.ToArray());
                res.Row = row; res.Attempts = attempt + 1;

                List<Problem> problems = Oracle.ValidateTsv(schema, header + "\n" + row, null);
                bool headerBad = false;
                foreach (Problem p in problems) if (p.Row == 0) headerBad = true;
                if (headerBad)
                {
                    res.Problems = problems;
                    res.Error = "schema/header mismatch for '" + table + "' (a declared column is missing from the table header)";
                    return res;
                }
                if (problems.Count == 0) { res.Ok = true; Status("propose: PASS"); return res; }

                res.Problems = problems;
                Status("propose: FAIL (" + problems.Count + " problem(s)), repairing");
                if (attempt == MaxProposeRepairs) break;

                var sb = new StringBuilder();
                sb.Append(basePrompt);
                sb.Append("\n\nYour previous row FAILED validation:\n");
                foreach (Problem p in problems) sb.Append("  " + p.ToString() + "\n");
                sb.Append("Previous row (tab-separated): " + row + "\nReturn a corrected JSON row.");
                prompt = sb.ToString();
            }
            return res;
        }

        // Pick which table the request targets: the only schema, or a constrained model pick.
        private string PickTable(string query)
        {
            List<string> tables = SchemaTables();
            if (tables.Count == 0) return null;
            if (tables.Count == 1) return tables[0];
            object schema = Json.Schema(Json.Obj("table", Json.EnumProp(tables)), "table");
            string prompt = "Pick which table this request adds a row to.\nTables: " +
                string.Join(", ", tables.ToArray()) + "\nRequest: " + query;
            Dictionary<string, object> v = Ollama.GenerateJson(url, icm.Config.DispatchModel(), prompt, schema, 0.1, DispatchTimeoutMs, cancel);
            return Json.GetStringOr(v, "table", tables[0]);
        }

        public List<string> SchemaTables()
        {
            var outl = new List<string>();
            string dir = System.IO.Path.Combine(icm.Root, Conventions.SchemasDir);
            try
            {
                if (System.IO.Directory.Exists(dir))
                    foreach (string f in System.IO.Directory.GetFiles(dir, "*.json"))
                        outl.Add(System.IO.Path.GetFileNameWithoutExtension(f));
            }
            catch (System.IO.IOException e) { Status("could not list schemas: " + e.Message); }
            return outl;
        }

        // The authoritative header is the first line of samples/<table>.txt; null if unavailable.
        private string TableHeader(string table)
        {
            try
            {
                List<string> lines = Tsv.NonEmptyLines(icm.ReadFile(Conventions.SampleRel(table)));
                return lines.Count > 0 ? lines[0] : null;
            }
            catch (IcmError) { return null; }
        }

        private string ExampleBlock(string table)
        {
            try
            {
                List<string> lines = Tsv.NonEmptyLines(icm.ReadFile(Conventions.SampleRel(table)));
                if (lines.Count <= 1) return "";
                var sb = new StringBuilder("Existing example rows (tab-separated):\n");
                for (int i = 1; i < lines.Count && i <= 2; i++) sb.Append(lines[i] + "\n");
                return sb.ToString();
            }
            catch (IcmError) { return ""; }
        }

        private static string DescribeColumns(string[] cols, TableSchema schema)
        {
            var byName = new Dictionary<string, ColSpec>();
            foreach (ColSpec c in schema.Columns) byName[c.Name] = c;
            var sb = new StringBuilder();
            foreach (string c in cols)
            {
                ColSpec cs;
                if (byName.TryGetValue(c, out cs))
                {
                    sb.Append("- " + c + ": " + cs.CType);
                    if (cs.Required) sb.Append(", required");
                    if (cs.Min.HasValue || cs.Max.HasValue)
                        sb.Append(", range " + (cs.Min.HasValue ? cs.Min.Value.ToString() : "*") + ".." + (cs.Max.HasValue ? cs.Max.Value.ToString() : "*"));
                    if (cs.Values.Count > 0) sb.Append(", one of: " + string.Join("|", cs.Values.ToArray()));
                    sb.Append("\n");
                }
                else sb.Append("- " + c + ": string (free text)\n");
            }
            return sb.ToString();
        }

        public static string FormatPropose(ProposeResult pr)
        {
            if (pr.Ok)
                return "PASS - proposed row for '" + pr.Table + "' (validated in " + pr.Attempts + " attempt(s)):\n" + pr.Row;
            if (pr.Error != null && pr.Problems.Count == 0)
                return "[error] " + pr.Error;
            var sb = new StringBuilder();
            sb.Append("FAIL - no valid row for '" + pr.Table + "' after " + pr.Attempts + " attempt(s).\n");
            if (pr.Error != null) sb.Append(pr.Error + "\n");
            sb.Append("Last row: " + pr.Row + "\n");
            foreach (Problem p in pr.Problems) sb.Append("  " + p.ToString() + "\n");
            return sb.ToString();
        }

        public string Help()
        {
            string entries = "(no manifest)";
            if (icm.Manifest != null)
            {
                var ids = new List<string>();
                foreach (Entry e in icm.Manifest.Entries) ids.Add(e.Id);
                entries = string.Join(", ", ids.ToArray());
            }
            return
                "This is the " + icm.Config.Name + " operator console. Type natural language; I route it to one capability:\n" +
                "- ask X      answer X from the knowledge base (" + entries + ")\n" +
                "- propose X  propose a new table row for X; the oracle validates it, then you can insert it\n" +
                "- make X     generate freeform text/code with the model\n" +
                "- validate T  check table T against its schema (the oracle)\n" +
                "- help / quit\n" +
                "The model classifies your line into one of these; it never free-roams tools.";
        }
    }
}
