// FlowEngine - runs an authored workflow (the Python icm_flow analog). Each node declares the
// inputs it reads from a shared state blackboard and the outputs it writes back. The FLOW is the
// orchestrator; model nodes (route/generate/answer/propose) PROPOSE, deterministic nodes
// (read/validate/tool) EXECUTE. The local model never decides what runs next. (Flow data model:
// Model/Flow.cs.)

using System;
using System.Collections.Generic;
using System.Text;

namespace Icm
{
    internal class FlowEngine
    {
        private const int MaxProblemsShown = 40;

        private readonly Instance icm;
        private readonly Dispatcher disp;
        private readonly Action<string> status;

        public FlowEngine(Instance icm, Dispatcher disp, Action<string> status)
        {
            this.icm = icm;
            this.disp = disp;
            this.status = status != null ? status : delegate(string s) { };
        }

        // Run the flow over a seed request. Returns the final state blackboard.
        public Dictionary<string, object> Run(Flow flow, string request)
        {
            var seed = new Dictionary<string, object>();
            seed["request"] = request;
            return Run(flow, seed);
        }

        // Seed several named inputs (from a command alias's declared `inputs`). "request" is always
        // present so flows and prompt placeholders can rely on it.
        public Dictionary<string, object> Run(Flow flow, Dictionary<string, object> seed)
        {
            var state = new Dictionary<string, object>();
            if (seed != null) foreach (KeyValuePair<string, object> kv in seed) state[kv.Key] = kv.Value;
            if (!state.ContainsKey("request")) state["request"] = "";
            foreach (FlowNode n in flow.Nodes)
            {
                status("flow: " + n.Id + " (" + n.Kind + ")");
                RunNode(n, state);
            }
            return state;
        }

        // The output keys written by the last node - what a caller usually wants to show.
        public static List<string> ResultKeys(Flow flow)
        {
            if (flow.Nodes.Count == 0) return new List<string>();
            return flow.Nodes[flow.Nodes.Count - 1].Outputs;
        }

        private void RunNode(FlowNode n, Dictionary<string, object> state)
        {
            if (n.Kind == Conventions.Node.Route)
            {
                string id = disp.RouteEntryId(StateStr(state, InputKey(n, 0, "request")));
                Set(state, OutputKey(n, 0, "entry_id"), id == null ? "" : id);
            }
            else if (n.Kind == Conventions.Node.Read)
            {
                // Accepts one id or a comma/newline-delimited list (from route_many). Each entry is
                // read with its routing metadata stripped and prefixed with a "## title (group)" header.
                string raw = StateStr(state, InputKey(n, 0, "entry_id"));
                var sb = new StringBuilder();
                foreach (string part in raw.Split(',', '\n'))
                {
                    string id = part.Trim();
                    if (id.Length == 0 || id == "none") continue;
                    try
                    {
                        string body = icm.ReadEntry(id);
                        Entry e = icm.Manifest != null ? icm.Manifest.GetEntry(id) : null;
                        string title = e != null ? e.Title : id;
                        string grp = (e != null && e.Group.Length > 0) ? " (" + e.Group + ")" : "";
                        if (sb.Length > 0) sb.Append("\n\n");
                        sb.Append("## " + title + grp + "\n" + body);
                    }
                    catch (IcmError) { }
                }
                Set(state, OutputKey(n, 0, "context"), sb.ToString());
            }
            else if (n.Kind == Conventions.Node.Generate)
            {
                string tmpl = Json.GetStringOr(n.Extra, "prompt", "Request: {request}\n\nContext:\n{context}");
                Set(state, OutputKey(n, 0, "text"), disp.Generate(Subst(tmpl, state), NumExtra(n, "temperature", 0.3)));
            }
            else if (n.Kind == Conventions.Node.Answer)
            {
                string sys = SafeReadFile(Conventions.SystemFile);
                string prompt = sys + "\n\nAnswer the question using ONLY the context below. If it is not " +
                    "covered, say so.\n\n--- CONTEXT ---\n{context}\n--- END ---\n\nQuestion: {request}";
                Set(state, OutputKey(n, 0, "answer"), disp.Generate(Subst(prompt, state), 0.2));
            }
            else if (n.Kind == Conventions.Node.Propose)
            {
                ProposeResult pr = disp.ProposeRow(TableArg(n, state), StateStr(state, "request"));
                Set(state, OutputKey(n, 0, "row"), pr.Row);
                Set(state, "ok", pr.Ok);
                if (!pr.Ok) Set(state, "problems", ProblemText(pr));
            }
            else if (n.Kind == Conventions.Node.Validate)
            {
                string tsv = n.Inputs.Contains("tsv") ? StateStr(state, "tsv") : null;
                ValidateResult vr = disp.Validate(TableArg(n, state), tsv);
                Set(state, OutputKey(n, 0, "verdict"), vr.ToText(MaxProblemsShown));
                Set(state, "ok", vr.Ok);
            }
            else if (n.Kind == Conventions.Node.Tool)
            {
                string toolName = Json.GetStringOr(n.Extra, "tool", "");
                Tool t = FindTool(toolName);
                if (t == null) { Set(state, OutputKey(n, 0, "output"), "no such tool: " + toolName); Set(state, "ok", false); return; }
                ToolRunResult rr = ToolRunner.Run(icm, t, ToolArgs(n, state));
                Set(state, OutputKey(n, 0, "output"), rr.Error != null ? rr.Error : rr.Output);
                Set(state, "ok", rr.Error == null && rr.Ok);
            }
            else if (n.Kind == Conventions.Node.Loop)
            {
                RunLoop(n, state);
            }
            else if (n.Kind == Conventions.Node.RouteMany)
            {
                int k = (int)NumExtra(n, "maxK", 3);
                List<string> ids = disp.RouteMany(StateStr(state, InputKey(n, 0, "request")), k);
                Set(state, OutputKey(n, 0, "entry_ids"), string.Join(",", ids.ToArray()));
            }
            else if (n.Kind == Conventions.Node.Catalog)
            {
                string grp = Json.GetString(n.Extra, "group");
                string dt = Json.GetString(n.Extra, "doc_type");
                string cat = icm.Manifest != null ? icm.Manifest.Catalog(grp, dt) : "";
                Set(state, OutputKey(n, 0, "catalog"), cat);
            }
            else if (n.Kind == Conventions.Node.Branch)
            {
                RunBranch(n, state);
            }
            else if (n.Kind == Conventions.Node.Search)
            {
                string corpus = Json.GetStringOr(n.Extra, "corpus", "");
                string query = StateStr(state, InputKey(n, 0, "request"));
                int k = (int)NumExtra(n, "k", 5);
                bool embed = Json.GetBool(n.Extra, "embed", true);
                string embedModel = string.IsNullOrEmpty(icm.Config.Models.Embed) ? "nomic-embed-text" : icm.Config.Models.Embed;
                string res = "";
                try { res = Search.Run(icm, disp.Url, corpus, query, k, embed, embedModel, status); }
                catch (Exception e) { status("flow: search '" + corpus + "' failed: " + e.Message); }
                Set(state, OutputKey(n, 0, "context"), res);
            }
            else
            {
                status("flow: unknown node kind '" + n.Kind + "' (skipped)");
            }
        }

        // Repeat the node's body until `until` (a state key) is truthy, or up to `maxIterations`.
        // With no `until`, runs exactly maxIterations times. This is the bounded repair/retry
        // primitive that turns a flow into an assembly line (e.g. generate -> verify -> repair).
        private void RunLoop(FlowNode n, Dictionary<string, object> state)
        {
            int max = (int)NumExtra(n, "maxIterations", 4);
            if (max < 1) max = 1;
            string until = Json.GetString(n.Extra, "until");

            // Seed the body's output keys (and the until key) so {placeholders} in the first pass
            // resolve to empty rather than literal braces.
            foreach (FlowNode child in n.Body)
                foreach (string outKey in child.Outputs)
                    if (!state.ContainsKey(outKey)) state[outKey] = "";
            if (until != null && !state.ContainsKey(until)) state[until] = false;

            for (int i = 0; i < max; i++)
            {
                status("flow: loop " + n.Id + " (iteration " + (i + 1) + "/" + max + ")");
                foreach (FlowNode child in n.Body)
                {
                    status("flow:   " + child.Id + " (" + child.Kind + ")");
                    RunNode(child, state);
                }
                if (until != null && IsTruthy(state, until))
                {
                    status("flow: loop " + n.Id + " satisfied '" + until + "' after " + (i + 1) + " iteration(s)");
                    return;
                }
            }
            if (until != null) status("flow: loop " + n.Id + " hit max " + max + " without '" + until + "'");
        }

        // Run one of two child bodies based on a state-key test. The condition is deterministic and
        // host-owned (the model never decides which branch runs). Tests: "truthy" (default)/"falsy"
        // read the value as a boolean; "empty"/"nonempty" test the trimmed string. This is the
        // "answer, else fall back" primitive (e.g. branch when `context` is empty -> search).
        private void RunBranch(FlowNode n, Dictionary<string, object> state)
        {
            string when = Json.GetStringOr(n.Extra, "when", "");
            string test = Json.GetStringOr(n.Extra, "test", "truthy");
            bool cond = BranchTaken(state, when, test);
            List<FlowNode> chosen = cond ? n.Then : n.Else;
            status("flow: branch " + n.Id + " [" + when + " " + test + "] = " + cond +
                " -> " + (cond ? "then" : "else") + " (" + chosen.Count + " node(s))");
            foreach (FlowNode child in chosen)
            {
                status("flow:   " + child.Id + " (" + child.Kind + ")");
                RunNode(child, state);
            }
        }

        // Whether a branch's `then` body is taken. Static + deterministic so it is unit-testable.
        public static bool BranchTaken(Dictionary<string, object> state, string when, string test)
        {
            string val = StateStr(state, when);
            if (test == "empty") return val.Trim().Length == 0;
            if (test == "nonempty") return val.Trim().Length > 0;
            if (test == "falsy") return !IsTruthy(state, when);
            return IsTruthy(state, when); // "truthy" (default)
        }

        private static bool IsTruthy(Dictionary<string, object> state, string key)
        {
            object v;
            if (!state.TryGetValue(key, out v) || v == null) return false;
            if (v is bool) return (bool)v;
            return string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        }

        // ----- helpers -----

        private string TableArg(FlowNode n, Dictionary<string, object> state)
        {
            string t = Json.GetString(n.Extra, "table");
            if (!string.IsNullOrEmpty(t)) return Subst(t, state);
            if (state.ContainsKey("table")) return StateStr(state, "table");
            return StateStr(state, "request");
        }

        // Tool arguments: explicit Extra["args"] (templated) wins; else the declared inputs become args.
        private Dictionary<string, object> ToolArgs(FlowNode n, Dictionary<string, object> state)
        {
            var args = new Dictionary<string, object>();
            Dictionary<string, object> declared = Json.GetObject(n.Extra, "args");
            if (declared != null)
                foreach (var kv in declared) args[kv.Key] = Subst(kv.Value == null ? "" : kv.Value.ToString(), state);
            else
                foreach (string inp in n.Inputs) args[inp] = StateStr(state, inp);
            return args;
        }

        private Tool FindTool(string name)
        {
            foreach (Tool t in icm.Config.Tools) if (t.Name == name) return t;
            return null;
        }

        private string SafeReadFile(string rel) { try { return icm.ReadFile(rel); } catch (IcmError) { return ""; } }

        private static string InputKey(FlowNode n, int i, string fallback) { return n.Inputs.Count > i ? n.Inputs[i] : fallback; }
        private static string OutputKey(FlowNode n, int i, string fallback) { return n.Outputs.Count > i ? n.Outputs[i] : fallback; }

        private static string StateStr(Dictionary<string, object> state, string key)
        {
            object v;
            if (state.TryGetValue(key, out v) && v != null) return v.ToString();
            return "";
        }

        private static void Set(Dictionary<string, object> state, string key, object val) { state[key] = val; }

        private static double NumExtra(FlowNode n, string key, double fallback)
        {
            double? d = Json.GetNumber(n.Extra, key);
            return d.HasValue ? d.Value : fallback;
        }

        // Replace {key} tokens with state values.
        private static string Subst(string s, Dictionary<string, object> state)
        {
            if (s.IndexOf('{') < 0) return s;
            string outv = s;
            foreach (var kv in state) outv = outv.Replace("{" + kv.Key + "}", kv.Value == null ? "" : kv.Value.ToString());
            return outv;
        }

        private static string ProblemText(ProposeResult pr)
        {
            var sb = new StringBuilder();
            foreach (Problem p in pr.Problems) sb.Append(p.ToString() + "\n");
            return sb.ToString();
        }
    }
}
