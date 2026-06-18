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
            var state = new Dictionary<string, object>();
            state["request"] = request;
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
                string id = StateStr(state, InputKey(n, 0, "entry_id"));
                string ctx = "";
                if (id.Length > 0) { try { ctx = icm.ReadEntry(id); } catch (IcmError) { ctx = ""; } }
                Set(state, OutputKey(n, 0, "context"), ctx);
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
            else
            {
                status("flow: unknown node kind '" + n.Kind + "' (skipped)");
            }
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
