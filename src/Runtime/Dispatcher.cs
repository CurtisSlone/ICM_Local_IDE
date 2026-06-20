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
        private const int RouteCandidateK = 8;       // embedder narrows a single KB route to this many
        private const int RouteManyCandidateK = 12;  // ... and a multi-route to this many

        private readonly Instance icm;
        private readonly string url;
        private readonly Action<string> status;
        private readonly List<string> history = new List<string>(); // "you: ..." / "icm: ..." lines
        private Cancel cancel;                                       // the in-flight op's cancel handle
        private string pendingFlowId;   // a router-proposed flow awaiting y/n confirmation
        private string pendingArgs;
        private string pendingRedirect; // a "> path" to save the pending flow's output to, if any
        private bool streamedThisTurn;  // set when this turn streamed its output via OnToken

        // Optional per-front-end token sink. When set (the console sets it), freeform generation
        // (ask / make / chat) streams tokens here as they arrive. Null = non-streaming (GUI, MCP).
        public Action<string> OnToken;

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
        // One turn. A '/' line is a slash command (deterministic dispatch). Plain text runs through the
        // conversational ROUTER: the model proposes a flow from the closed catalog, a deterministic gate
        // decides, and a confident match is run (after y/n confirmation by default) or falls back to
        // /ask. /chat is free conversation; /do is the classify-and-route path.
        public TurnResult Turn(string line)
        {
            var r = new TurnResult();
            line = (line ?? "").Trim();
            r.Standalone = line;
            if (line.Length == 0) { r.Text = ""; return r; }
            cancel = new Cancel();
            streamedThisTurn = false;

            // Resolve a pending router confirmation (a plain y/n answer to "Run the X flow?").
            if (pendingFlowId != null && line[0] != '/')
            {
                if (IsAffirmative(line))
                {
                    string id = pendingFlowId, args = pendingArgs, rd = pendingRedirect;
                    pendingFlowId = null; pendingArgs = null; pendingRedirect = null;
                    r.Intent = "flow:" + id;
                    Status("router: running '" + id + "'");
                    RunNamedFlow(id, args, r);
                    ApplyRedirect(r, rd, "flow:" + id, args);
                    return Done(r, line);
                }
                if (IsNegative(line))
                { pendingFlowId = null; pendingArgs = null; pendingRedirect = null; r.Intent = "chat"; r.Text = "Cancelled."; return Done(r, line); }
                pendingFlowId = null; pendingArgs = null; pendingRedirect = null; // anything else cancels, handled below
            }

            if (line[0] == '/')
            {
                RunSlash(line, r);
                if (r.Intent != "clear") Done(r, line);
                return r;
            }

            // Plain text: parse an optional "> path" redirect, then route (or /ask when routing is off).
            string redirect; string clean = ParseRedirect(line, out redirect);
            if (icm.Config.Router.Enabled()) RouteConversational(clean, redirect, r);
            else { RunSlash("/ask " + clean, r); ApplyRedirect(r, redirect, "ask", clean); }
            return Done(r, line);
        }

        private TurnResult Done(TurnResult r, string line)
        {
            r.Streamed = streamedThisTurn;
            Remember("you: " + line);
            Remember("icm: " + (r.IsError ? r.Text : Truncate(r.Text, 400)));
            return r;
        }

        // Generate freeform text, streaming to OnToken when a front end has wired it (the console);
        // otherwise a normal blocking call. Either way returns the full text.
        private string GenerateMaybeStream(string prompt, double temperature)
        {
            if (OnToken != null)
            {
                streamedThisTurn = true;
                return Ollama.GenerateStream(url, icm.Config.Models.Generate, prompt, temperature, GenTimeoutMs, OnToken, cancel);
            }
            return Ollama.Generate(url, icm.Config.Models.Generate, prompt, null, temperature, GenTimeoutMs, cancel);
        }

        private static bool IsAffirmative(string s)
        {
            string t = s.Trim().ToLowerInvariant();
            return t == "y" || t == "yes" || t == "yeah" || t == "yep" || t == "ok" || t == "okay" || t == "sure" || t == "run" || t == "do it" || t == "go";
        }

        private static bool IsNegative(string s)
        {
            string t = s.Trim().ToLowerInvariant();
            return t == "n" || t == "no" || t == "nope" || t == "cancel" || t == "stop";
        }

        // --- the conversational router: propose a flow from the closed catalog, gate it, act ---

        private class RouteResult { public string FlowId = ""; public string Args = ""; public string Confidence = "low"; }

        internal enum GateDecision { Match, Fallback }

        // Deterministic gate: a proposal only proceeds if it names an on-list flow with non-low
        // confidence. Pure + static so SelfTest can cover it without the model.
        internal static GateDecision Gate(string flowId, string confidence, List<string> validIds)
        {
            if (string.IsNullOrEmpty(flowId) || flowId == "none") return GateDecision.Fallback;
            if (!validIds.Contains(flowId)) return GateDecision.Fallback;
            if (confidence == "low") return GateDecision.Fallback;
            return GateDecision.Match;
        }

        private void RouteConversational(string line, string redirect, TurnResult r)
        {
            Status("router: matching a flow");
            RouteResult rr = null;
            try { rr = RouteFlow(line); }
            catch (IcmError) { rr = null; }     // model down/failed -> fall back to ask

            var ids = new List<string>();
            foreach (FlowInfo fi in FlowCatalog()) ids.Add(fi.Id);

            if (rr == null || Gate(rr.FlowId, rr.Confidence, ids) == GateDecision.Fallback)
            { RunSlash("/ask " + line, r); ApplyRedirect(r, redirect, "ask", line); return; }

            // Auto-run only when configured AND the model is highly confident.
            if (icm.Config.Router.AutoRunHigh() && rr.Confidence == "high")
            {
                r.Intent = "flow:" + rr.FlowId;
                Status("router: running '" + rr.FlowId + "' (high)");
                RunNamedFlow(rr.FlowId, rr.Args, r);
                if (!string.IsNullOrEmpty(redirect)) ApplyRedirect(r, redirect, "flow:" + rr.FlowId, rr.Args);
                else r.Text = "-> routed to `" + rr.FlowId + "` (high)\n\n" + r.Text;
                return;
            }

            // Otherwise propose and wait for confirmation (carrying the redirect to the y/n turn).
            pendingFlowId = rr.FlowId; pendingArgs = rr.Args; pendingRedirect = redirect;
            r.Intent = "router";
            string argNote = string.IsNullOrEmpty(rr.Args) ? "" : " with: " + rr.Args;
            string saveNote = string.IsNullOrEmpty(redirect) ? "" : " (saves to " + redirect + ")";
            r.Text = "This looks like the `" + rr.FlowId + "` flow (" + rr.Confidence + " confidence)" + argNote + saveNote +
                ".\nRun it? (y / n) - or type a slash command instead.";
        }

        private RouteResult RouteFlow(string request)
        {
            List<FlowInfo> flows = NarrowFlows(request, FlowCatalog(), RouteCandidateK);
            if (flows.Count == 0) return null;
            var ids = new List<string>();
            var lines = new List<string>();
            foreach (FlowInfo fi in flows) { ids.Add(fi.Id); lines.Add("- " + fi.Id + ": " + fi.WhenToUse); }
            ids.Add("none");

            object schema = Json.Schema(Json.Obj(
                "flow_id", Json.EnumProp(ids),
                "args", Json.StrProp(),
                "confidence", Json.EnumProp(new string[] { "high", "medium", "low" })), "flow_id", "confidence");
            string prompt =
                "Route the operator's request to ONE workflow, or 'none' if no workflow fits (a plain " +
                "question with no matching workflow is 'none'). Put the task/topic from their message in " +
                "args. Rate confidence honestly.\n\nWorkflows:\n" + string.Join("\n", lines.ToArray()) +
                "\n\nOperator: " + request + "\n\nReturn JSON {flow_id, args, confidence}.";
            Dictionary<string, object> v = Ollama.GenerateJson(url, icm.Config.DispatchModel(), prompt, schema, 0.1, DispatchTimeoutMs, cancel);
            var rr = new RouteResult();
            rr.FlowId = Json.GetStringOr(v, "flow_id", "none");
            rr.Args = Json.GetStringOr(v, "args", "");
            rr.Confidence = Json.GetStringOr(v, "confidence", "low");
            if (rr.Args.Length == 0) rr.Args = request;   // default the flow input to the raw line
            return rr;
        }

        // Embedder narrowing (the ICM "embedder" role): rank candidates by similarity to the query and
        // keep the top-k, so the constrained model pick chooses from a short, relevant list. Falls back
        // to all candidates when the embed seat is unset or Ollama is unreachable. No-op at small sizes.
        private List<Entry> NarrowEntries(string query, List<Entry> entries, int k)
        {
            if (entries.Count <= k) return entries;
            var cands = new List<Cand>();
            foreach (Entry e in entries)
                cands.Add(new Cand(e.Id, e.Title + ". " + e.Summary + " " + string.Join(" ", e.Keywords.ToArray())));
            List<string> top = Embedder.RankTopK(icm, url, icm.Config.Models.Embed, query, cands, k, status);
            if (top == null || top.Count == 0) return entries;
            var byId = new Dictionary<string, Entry>();
            foreach (Entry e in entries) byId[e.Id] = e;
            var outl = new List<Entry>();
            foreach (string id in top) { Entry e; if (byId.TryGetValue(id, out e)) outl.Add(e); }
            if (outl.Count == 0) return entries;
            Status("route: embedding-narrowed to " + outl.Count + " of " + entries.Count + " entries");
            return outl;
        }

        private List<FlowInfo> NarrowFlows(string query, List<FlowInfo> flows, int k)
        {
            if (flows.Count <= k) return flows;
            var cands = new List<Cand>();
            foreach (FlowInfo fi in flows) cands.Add(new Cand(fi.Id, fi.Name + ". " + fi.WhenToUse));
            List<string> top = Embedder.RankTopK(icm, url, icm.Config.Models.Embed, query, cands, k, status);
            if (top == null || top.Count == 0) return flows;
            var byId = new Dictionary<string, FlowInfo>();
            foreach (FlowInfo fi in flows) byId[fi.Id] = fi;
            var outl = new List<FlowInfo>();
            foreach (string id in top) { FlowInfo fi; if (byId.TryGetValue(id, out fi)) outl.Add(fi); }
            return outl.Count > 0 ? outl : flows;
        }

        private List<FlowInfo> FlowCatalog()
        {
            var outl = new List<FlowInfo>();
            string dir = System.IO.Path.Combine(icm.Root, Conventions.FlowsDir);
            if (!System.IO.Directory.Exists(dir)) return outl;
            string[] files;
            try { files = System.IO.Directory.GetFiles(dir, "*.json"); } catch { return outl; }
            System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                try { Flow fl = Flow.Load(f); outl.Add(new FlowInfo { Id = System.IO.Path.GetFileNameWithoutExtension(f), Name = fl.Name, WhenToUse = fl.WhenToUse }); }
                catch (IcmError) { }
            }
            return outl;
        }

        // Split "/cmd the rest" into ("cmd", "the rest"); cmd is lowercased. Public for SelfTest.
        public static void ParseCommand(string line, out string cmd, out string rest)
        {
            string body = (line != null && line.StartsWith("/")) ? line.Substring(1) : (line ?? "");
            SplitFirst(body, out cmd, out rest);
            cmd = cmd.ToLowerInvariant();
        }

        private static void SplitFirst(string s, out string first, out string rest)
        {
            s = (s ?? "").TrimStart();
            int i = 0; while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
            first = s.Substring(0, i);
            rest = (i < s.Length) ? s.Substring(i).Trim() : "";
        }

        private void RunSlash(string line, TurnResult r)
        {
            string cmd, rest;
            ParseCommand(line, out cmd, out rest);
            string redirect; rest = ParseRedirect(rest, out redirect);
            string task = rest;
            r.Intent = cmd;
            Status("command: /" + cmd);
            try
            {
                switch (cmd)
                {
                    case "help": case "h": case "?": r.Text = Help(); break;
                    case "ask":
                        if (rest.Length == 0) { Usage(r, "/ask <question>"); break; }
                        r.Intent = Conventions.Intent.Ask; r.Text = DoAsk(rest); break;
                    case "make":
                        if (rest.Length == 0) { Usage(r, "/make <prompt>"); break; }
                        r.Intent = Conventions.Intent.Make; r.Text = DoMake(rest); break;
                    case "chat":
                        if (rest.Length == 0) { Usage(r, "/chat <message>"); break; }
                        r.Intent = "chat"; r.Text = DoChat(rest); break;
                    case "flow":
                    {
                        // Generic: run any authored flow by name. Domain shortcuts (/write, /compile, ...)
                        // are instance-declared command aliases (icm.config.json), handled in default.
                        string name, input; SplitFirst(rest, out name, out input);
                        if (name.Length == 0) { Usage(r, "/flow <name> <input>"); break; }
                        RunNamedFlow(name, input, r); break;
                    }
                    case "tool":
                    {
                        // Generic: run any declared command/script tool by name.
                        string tname, targ; SplitFirst(rest, out tname, out targ);
                        if (tname.Length == 0) { Usage(r, "/tool <name> [arg]"); break; }
                        RunToolByName(tname, null, targ, r); break;
                    }
                    case "list": case "ls":
                        r.Text = (icm.Manifest != null) ? CatalogOr(rest) : "(no manifest.json)"; break;
                    case "flows":
                    {
                        var sb = new StringBuilder();
                        var fl = FlowCatalog();
                        sb.Append("Authored flows (the router can match these, or run with /flow <name>):\n");
                        if (fl.Count == 0) sb.Append("  (none in flows/)\n");
                        else foreach (FlowInfo fi in fl) sb.Append("  " + fi.Id + " - " + fi.WhenToUse + "\n");
                        sb.Append("\nBuilt-in capabilities:\n");
                        sb.Append("  /chat <message> - free conversation with the model (not grounded)\n");
                        sb.Append("  /ask <question> - grounded answer from the knowledge base (the plain-text default)\n");
                        sb.Append("  /make <prompt>  - freeform generation (no grounding, no oracle)");
                        r.Text = sb.ToString();
                        break;
                    }
                    case "search": case "docs":
                        r.Text = DoSearch(rest); break;
                    case "validate":
                        if (rest.Length == 0) { Usage(r, "/validate <table>"); break; }
                        r.Text = Validate(ParseTable(rest), null).ToText(MaxProblemsShown); break;
                    case "propose":
                        if (rest.Length == 0) { Usage(r, "/propose <description>"); break; }
                        DoPropose(rest, r); break;
                    case "do":
                    {
                        if (rest.Length == 0) { Usage(r, "/do <request>"); break; }
                        string standalone = rest;
                        try { if (history.Count > 0) standalone = Rewrite(rest); } catch (IcmError) { }
                        r.Standalone = standalone;
                        string intent, query;
                        Classify(standalone, out intent, out query);
                        r.Intent = intent; r.Query = query;
                        RunIntent(intent, query, r);
                        break;
                    }
                    case "note":
                        if (rest.Length == 0) { Usage(r, "/note <text>"); break; }
                        AppendNote(rest); r.Text = "noted."; break;
                    case "notes":
                    {
                        string notes = ReadNotes();
                        r.Text = notes.Length > 0 ? notes : "(no notes yet - use /note <text>, or redirect a write with '> path')";
                        break;
                    }
                    case "clear": case "reset":
                        history.Clear(); r.Intent = "clear"; r.Text = ""; break;
                    case "quit": case "exit": case "q":
                        r.Intent = Conventions.Intent.Quit; r.Text = "bye"; break;
                    default:
                    {
                        // An instance-declared command alias (icm.config.json) wins; otherwise the whole
                        // line defaults to /ask.
                        CommandAlias alias = icm.Config.FindCommand(cmd);
                        if (alias != null) { RunAlias(alias, rest, r); break; }
                        string q = rest.Length > 0 ? cmd + " " + rest : cmd;
                        if (q.Length == 0) { r.Text = Help(); break; }
                        r.Intent = Conventions.Intent.Ask; r.Text = DoAsk(q);
                        break;
                    }
                }
            }
            catch (IcmError e) { r.IsError = true; r.Text = "[error] " + e.Message; }

            ApplyRedirect(r, redirect, "/" + cmd, task);
        }

        private static void Usage(TurnResult r, string usage) { r.Text = "Usage: " + usage; r.IsError = true; }

        private Tool FindTool(string name)
        {
            foreach (Tool t in icm.Config.Tools) if (t.Name == name) return t;
            return null;
        }

        // Run an instance-declared command alias: a flow, a tool, or a detached launch. Keeps domain
        // verbs out of the host - the binary only knows how to dispatch the three kinds.
        private void RunAlias(CommandAlias a, string rest, TurnResult r)
        {
            r.Intent = a.Name;
            if (!string.IsNullOrEmpty(a.Flow))
            {
                if (rest.Length == 0) { Usage(r, "/" + a.Name + " <input>"); return; }
                if (a.Inputs != null && a.Inputs.Count > 0) RunNamedFlow(a.Flow, SplitInputs(a.Inputs, rest), r);
                else RunNamedFlow(a.Flow, rest, r);
            }
            else if (!string.IsNullOrEmpty(a.Tool))
            {
                if (rest.Length == 0) { Usage(r, "/" + a.Name + " <" + (string.IsNullOrEmpty(a.Arg) ? "arg" : a.Arg) + ">"); return; }
                RunToolByName(a.Tool, a.Arg, rest, r);
            }
            else if (!string.IsNullOrEmpty(a.Launch))
            {
                if (rest.Length == 0) { Usage(r, "/" + a.Name + " <path in the workspace>"); return; }
                string target;
                try { target = icm.Resolve(rest); } catch (IcmError e) { r.Text = "[error] " + e.Message; r.IsError = true; return; }
                if (!System.IO.File.Exists(target)) { r.Text = "not found: " + rest; r.IsError = true; return; }
                try { LaunchDetached(a.Launch, target); r.Text = "Launched " + rest + " (detached; close its window when done)."; }
                catch (Exception e) { r.Text = "[error] launching: " + e.Message; r.IsError = true; }
            }
            else { r.Text = "command '/" + a.Name + "' has no target (flow/tool/launch) in icm.config.json"; r.IsError = true; }
        }

        // Run a declared command/script tool, mapping `rest` to argName (else the tool's stdin arg, else
        // its first required input). Used by /tool and tool-kind command aliases.
        private void RunToolByName(string toolName, string argName, string rest, TurnResult r)
        {
            Tool t = FindTool(toolName);
            if (t == null) { r.Text = "no such tool: " + toolName; r.IsError = true; return; }
            var args = new Dictionary<string, object>();
            string key = argName;
            if (string.IsNullOrEmpty(key)) key = t.StdinArg();
            if (string.IsNullOrEmpty(key)) key = FirstRequiredArg(t);
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(rest)) args[key] = rest;
            ToolRunResult rr = ToolRunner.Run(icm, t, args);
            r.Text = rr.Error != null ? rr.Error : (rr.Output.Length > 0 ? rr.Output : "(no output)");
            r.IsError = rr.Error != null || !rr.Ok;
        }

        private static string FirstRequiredArg(Tool t)
        {
            var schema = t.InputSchema() as Dictionary<string, object>;
            if (schema == null) return null;
            List<object> req = Json.GetArr(schema, "required");
            return req.Count > 0 && req[0] != null ? req[0].ToString() : null;
        }

        // Launch a workspace artifact via tools/<launcher>.ps1 (the SAC-safe in-memory loader), detached
        // so a GUI app does not block the console. The launcher script takes -Exe <path>.
        private void LaunchDetached(string launcher, string targetPath)
        {
            string loader = icm.Resolve(Conventions.ToolsDir + "/" + launcher + ".ps1");
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-STA -NoProfile -ExecutionPolicy Bypass -File \"" + loader + "\" -Exe \"" + targetPath + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = icm.Root;
            System.Diagnostics.Process.Start(psi);   // detached; do not wait
        }

        // Write a command/flow's text output to a workspace file ("> path"): code fences stripped so a
        // .cs/.ps1 lands clean, recorded in NOTES.md. Clears the streamed flag so the "Wrote" line shows
        // even when the body was streamed live.
        private void ApplyRedirect(TurnResult r, string redirect, string label, string task)
        {
            if (string.IsNullOrEmpty(redirect) || r.IsError || string.IsNullOrEmpty(r.Text)
                || r.Intent == "clear" || r.Intent == Conventions.Intent.Quit) return;
            try
            {
                string content = Markdown.StripFence(r.Text);
                icm.WriteFile(redirect, content);
                r.WrittenPath = icm.Resolve(redirect);
                AppendNote("wrote `" + redirect + "` (" + label + ": " + Truncate(task, 80) + ")");
                r.Text = "Wrote " + redirect + " (" + content.Length + " chars).";
                streamedThisTurn = false;
            }
            catch (IcmError e) { r.IsError = true; r.Text = "[error] writing " + redirect + ": " + e.Message; }
        }

        // Parse a trailing " > path" redirect off a command's argument. The target must look like a path
        // (has an extension or a separator, no spaces) so prose with " > " is not misread. Returns the
        // argument without the redirect; sets path (null if none). Public for SelfTest.
        public static string ParseRedirect(string rest, out string path)
        {
            path = null;
            if (rest == null) return "";
            int idx = rest.LastIndexOf(" > ", StringComparison.Ordinal);
            if (idx >= 0)
            {
                string p = rest.Substring(idx + 3).Trim();
                bool looksLikePath = p.Length > 0 && p.IndexOf(' ') < 0
                    && (p.IndexOf('.') >= 0 || p.IndexOf('\\') >= 0 || p.IndexOf('/') >= 0);
                if (looksLikePath) { path = p; return rest.Substring(0, idx).Trim(); }
            }
            return rest;
        }

        // --- session memory (NOTES.md in the instance) ---

        private void AppendNote(string text)
        {
            string existing = ReadNotes();
            if (existing.Length == 0) existing = "# " + icm.Config.Name + " - session notes\n";
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            try { icm.WriteFile(Conventions.NotesFile, existing.TrimEnd() + "\n- [" + stamp + "] " + text + "\n"); }
            catch (IcmError) { }
        }

        private string ReadNotes()
        {
            try { return icm.ReadFile(Conventions.NotesFile); } catch (IcmError) { return ""; }
        }

        // The old classify-and-route path, now reachable via /do.
        private void RunIntent(string intent, string query, TurnResult r)
        {
            if (intent == Conventions.Intent.Quit) r.Text = "bye";
            else if (intent == Conventions.Intent.Help) r.Text = Help();
            else if (intent == Conventions.Intent.Ask) r.Text = DoAsk(query);
            else if (intent == Conventions.Intent.Make) r.Text = DoMake(query);
            else if (intent == Conventions.Intent.Validate) r.Text = Validate(ParseTable(query), null).ToText(MaxProblemsShown);
            else if (intent == Conventions.Intent.Propose) DoPropose(query, r);
            else r.Text = "unknown intent '" + intent + "'";
        }

        private string CatalogOr(string group)
        {
            string cat = icm.Manifest.Catalog(group.Length > 0 ? group : null, null);
            return cat.Length > 0 ? cat : ("(no entries" + (group.Length > 0 ? " in group '" + group + "'" : "") + ")");
        }

        private void RunNamedFlow(string name, string input, TurnResult r)
        {
            Flow flow;
            try { flow = Flow.Load(icm.FlowPath(name)); }
            catch (IcmError e) { r.IsError = true; r.Text = "[error] no flow '" + name + "': " + e.Message; return; }
            var engine = new FlowEngine(icm, this, status);
            Dictionary<string, object> state = engine.Run(flow, input);
            r.Text = FlowResult(flow, state);
        }

        // Seed a flow with several named inputs (a command alias declared `inputs`).
        private void RunNamedFlow(string name, Dictionary<string, object> seed, TurnResult r)
        {
            Flow flow;
            try { flow = Flow.Load(icm.FlowPath(name)); }
            catch (IcmError e) { r.IsError = true; r.Text = "[error] no flow '" + name + "': " + e.Message; return; }
            var engine = new FlowEngine(icm, this, status);
            Dictionary<string, object> state = engine.Run(flow, seed);
            r.Text = FlowResult(flow, state);
        }

        // Map a command line onto an alias's ordered input names: each of the first (n-1) names takes
        // one whitespace-delimited token; the LAST name captures the remainder. Generic and
        // domain-agnostic - the host never interprets what the slots mean. Public for SelfTest.
        public static Dictionary<string, object> SplitInputs(List<string> names, string rest)
        {
            var d = new Dictionary<string, object>();
            string remaining = (rest == null ? "" : rest).Trim();
            int lead = names.Count - 1;
            for (int i = 0; i < lead; i++)
            {
                string tok, more; SplitFirst(remaining, out tok, out more);
                d[names[i]] = tok;
                remaining = more;
            }
            d[names[names.Count - 1]] = remaining;
            return d;
        }

        private static string FlowResult(Flow flow, Dictionary<string, object> state)
        {
            List<string> keys = FlowEngine.ResultKeys(flow);
            if (keys.Count == 0) keys = new List<string>(new string[] { "answer", "code", "row", "verdict", "output", "text" });
            var sb = new StringBuilder();
            foreach (string k in keys)
            {
                object v;
                if (state.TryGetValue(k, out v) && v != null) { if (sb.Length > 0) sb.Append("\n"); sb.Append(v.ToString()); }
            }
            return sb.Length > 0 ? sb.ToString() : "(flow produced no output)";
        }

        private string DoSearch(string rest)
        {
            string first, more; SplitFirst(rest, out first, out more);
            string corpus = "dotnet", query = rest;
            if (first.Length > 0 && CorpusExists(first)) { corpus = first; query = more; }
            if (query.Length == 0) return "Usage: /search [corpus] <query>";
            string embedModel = string.IsNullOrEmpty(icm.Config.Models.Embed) ? "nomic-embed-text" : icm.Config.Models.Embed;
            return Search.Run(icm, url, corpus, query, 5, true, embedModel, status);
        }

        private bool CorpusExists(string corpus)
        {
            try { return System.IO.File.Exists(icm.Resolve(Conventions.RefdocRel(corpus))); }
            catch { return false; }
        }

        // Casual conversation: the model talks the operator through planning, aware of the available
        // slash commands and the KB catalog so it can point at the exact command to run.
        private string DoChat(string line)
        {
            string system;
            try { system = icm.ReadFile(Conventions.SystemFile); } catch (IcmError) { system = ""; }
            var sb = new StringBuilder();
            if (system.Length > 0) sb.Append(system + "\n\n");
            sb.Append("You are the conversational assistant of the '" + icm.Config.Name + "' tool for: " + icm.Config.Domain + ". ");
            sb.Append("The operator chats with you to plan and build software. You cannot run tools or edit files yourself; the operator acts by typing slash commands. ");
            sb.Append("When an action would help, tell them the EXACT command to run with the argument filled in:\n");
            sb.Append("  /ask <question>   grounded answer from the knowledge base\n");
            sb.Append("  /write <task>     generate C# code, compiled and repaired until it builds\n");
            sb.Append("  /ps <task>        generate PowerShell, parse-checked\n");
            sb.Append("  /list [group]     list available patterns/references/recipes/scaffolds\n");
            sb.Append("  /search <query>   search the API/doc corpora\n");
            sb.Append("  /make <prompt>    freeform generation\n");
            if (icm.Manifest != null)
            {
                string cat = icm.Manifest.Catalog(null, null);
                if (cat.Length > 0) sb.Append("\nAvailable knowledge:\n" + Truncate(cat, 2500) + "\n");
            }
            string notes = ReadNotes();
            if (notes.Length > 0) sb.Append("\nProject notes (NOTES.md, prior session context):\n" + Truncate(notes, 1500) + "\n");
            if (history.Count > 0) sb.Append("\nConversation so far:\n" + string.Join("\n", history.ToArray()) + "\n");
            sb.Append("\nOperator: " + line + "\n\nReply briefly and concretely. If a slash command would do what they want, name it exactly. Do not invent commands or APIs you were not told about.");
            return GenerateMaybeStream(sb.ToString(), 0.4);
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
            List<Entry> entries = NarrowEntries(query, icm.Manifest.Entries, RouteCandidateK);

            var ids = new List<string>();
            var lines = new List<string>();
            foreach (Entry e in entries)
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

        // Constrained MULTI-pick: the model proposes up to maxK relevant entry ids (a JSON array
        // constrained to the manifest enum). The grounding step for generation that uses several
        // patterns/references at once. Stays on-thesis: the model proposes from a fixed set, the host
        // reads what it picked.
        public List<string> RouteMany(string query, int maxK) { cancel = new Cancel(); return RouteManyImpl(query, maxK); }

        private List<string> RouteManyImpl(string query, int maxK)
        {
            var ids = new List<string>();
            if (icm.Manifest == null || icm.Manifest.Entries.Count == 0) return ids;
            if (maxK < 1) maxK = 1;

            var enumIds = new List<string>();
            var lines = new List<string>();
            foreach (Entry e in NarrowEntries(query, icm.Manifest.Entries, RouteManyCandidateK))
            {
                enumIds.Add(e.Id);
                string grp = e.Group.Length > 0 ? " (" + e.Group + ")" : "";
                string kw = e.Keywords.Count > 0 ? "  [keywords: " + string.Join(", ", e.Keywords.ToArray()) + "]" : "";
                lines.Add("- " + e.Id + grp + " : " + e.Title + " - " + e.Summary + kw);
            }

            object itemSchema = Json.EnumProp(enumIds);
            object schema = Json.Schema(Json.Obj("entry_ids", Json.Obj("type", "array", "items", itemSchema)), "entry_ids");
            string prompt =
                "Select EVERY KB entry whose content would help with the task below - the patterns, " +
                "references, or snippets you would ground on while writing the answer. Return up to " +
                maxK + " entry ids, most relevant first, as a JSON array. Return an empty array if none apply.\n\n" +
                "Index:\n" + string.Join("\n", lines.ToArray()) + "\n\nTask: " + query;
            Dictionary<string, object> v = Ollama.GenerateJson(url, icm.Config.DispatchModel(), prompt, schema, 0.1, DispatchTimeoutMs, cancel);
            foreach (object o in Json.GetArr(v, "entry_ids"))
            {
                if (o == null) continue;
                string s = o.ToString().Trim();
                if (s.Length == 0 || s == "none") continue;
                if (!ids.Contains(s)) ids.Add(s);
                if (ids.Count >= maxK) break;
            }
            return ids;
        }

        // Used by flow generate/answer nodes too. Streams to OnToken when a front end wired it, so a
        // flow's generation is visible live in the console. (All bundled flows' result key is the final
        // generated text, so the console's "already streamed, don't reprint" stays correct.)
        public string Generate(string prompt, double temperature)
        {
            cancel = new Cancel();
            return GenerateMaybeStream(prompt, temperature);
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
            return GenerateMaybeStream(prompt, 0.2);
        }

        private string DoMake(string query)
        {
            Status("make: generating a proposal");
            string prompt =
                "You are a careful assistant for the ICM domain: " + icm.Config.Domain +
                ". Produce what the operator asked for. Be concrete and minimal.\n\nRequest: " + query;
            return GenerateMaybeStream(prompt, 0.3);
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
            string groups = "(no manifest)";
            if (icm.Manifest != null)
            {
                List<string> gs = icm.Manifest.Groups();
                groups = gs.Count > 0 ? string.Join(", ", gs.ToArray()) : "(none)";
            }
            var sb = new StringBuilder();
            sb.Append("This is the " + icm.Config.Name + " operator console (" + icm.Config.Domain + ").\n");
            sb.Append("Chat normally to plan and ask questions; use slash commands to act.\n\n");
            sb.Append("Generic commands (the harness):\n");
            sb.Append("  /ask <question>          grounded answer from the knowledge base\n");
            sb.Append("  /make <prompt>           freeform generation (no grounding or verify)\n");
            sb.Append("  /chat <message>          free conversation with the model (not grounded)\n");
            sb.Append("  /flow <name> <input>     run any authored flow\n");
            sb.Append("  /tool <name> [arg]       run any declared tool\n");
            sb.Append("  /flows                   list workflows the router can match\n");
            sb.Append("  /list [group]            list KB entries (groups: " + groups + ")\n");
            sb.Append("  /search [corpus] <query> hybrid search the doc corpora\n");
            sb.Append("  /validate <table>        run the oracle on a data table\n");
            sb.Append("  /propose <description>   propose a table row, oracle-validated\n");
            sb.Append("  /note <text>  /notes     add to / show NOTES.md (session memory)\n");
            sb.Append("  /do <request>            classify-and-route\n");
            sb.Append("  /clear   /help   /quit\n");
            if (icm.Config.Commands.Count > 0)
            {
                sb.Append("\n" + icm.Config.Name + " commands (from icm.config.json):\n");
                foreach (CommandAlias a in icm.Config.Commands)
                {
                    string tgt = a.Flow != null ? "flow " + a.Flow : a.Tool != null ? "tool " + a.Tool : "launch " + a.Launch;
                    string help = a.Help.Length > 0 ? a.Help : "-> " + tgt;
                    sb.Append("  /" + a.Name.PadRight(22) + " " + help + "\n");
                }
            }
            sb.Append("\nAppend ' > path' to save a command's output to a file (e.g. /flow csharp a string reverser > out\\Rev.cs).\n");
            sb.Append("Just type what you want - it is matched to a workflow and run after you confirm (y/n);\n");
            sb.Append("if nothing fits, or you ask a question, it falls back to a grounded /ask.");
            return sb.ToString();
        }
    }
}
