// MCP server (stdio JSON-RPC) - lets a STRONG orchestrator (Claude) drive the same ICM the local
// dispatcher drives. "Same server, two callers" from the DEVLOG.
//
// tools/list advertises the instance's declared tools (using each tool's authored inputSchema when
// present). tools/call dispatches by kind: command/script tools run through ToolRunner; validate
// runs the oracle; kb_answer/propose/flow run through the shared Dispatcher / FlowEngine. The model
// never picks a tool here - Claude does, or an authored flow does.

using System;
using System.Collections.Generic;

namespace Icm
{
    internal static class Mcp
    {
        private const string ProtocolVersion = "2025-11-25";  // advertised default
        private const string HostVersion = "0.1.0";
        private const int MaxProblemsShown = 40;

        private static object InputSchema(Tool t)
        {
            object authored = t.InputSchema();
            if (authored != null) return authored;
            switch (t.Kind)
            {
                case Conventions.ToolKind.Validate:
                    return Json.Schema(Json.Obj(
                        "table", Json.Obj("type", "string", "description", "schema/table name to validate"),
                        "tsv", Json.Obj("type", "string", "description", "table text to check (optional; else the file on disk)")),
                        "table");
                case Conventions.ToolKind.KbAnswer:
                    return Json.Schema(Json.Obj("question", Json.StrProp()), "question");
                case Conventions.ToolKind.Propose:
                case Conventions.ToolKind.GenerateVerify:
                    return Json.Schema(Json.Obj(
                        "table", Json.Obj("type", "string", "description", "target table"),
                        "request", Json.Obj("type", "string", "description", "what row to add")),
                        "table", "request");
                case Conventions.ToolKind.Flow:
                    return Json.Schema(Json.Obj("request", Json.Obj("type", "string", "description", "the flow's seed input")), "request");
                default:
                    return Json.Obj("type", "object", "properties", new Dictionary<string, object>());
            }
        }

        private static Dictionary<string, object> ToolsList(Instance icm)
        {
            var tools = new List<object>();
            // Built-in enumeration tools: let the orchestrator browse the KB, then pull entries.
            if (icm.Manifest != null)
            {
                tools.Add(Json.Obj("name", "catalog",
                    "description", "List this instance's KB entries (id, group, summary). Optional filters: group, doc_type.",
                    "inputSchema", Json.Schema(Json.Obj("group", Json.StrProp(), "doc_type", Json.StrProp()))));
                tools.Add(Json.Obj("name", "read_entry",
                    "description", "Read one KB entry's full text by id (routing metadata stripped).",
                    "inputSchema", Json.Schema(Json.Obj("id", Json.StrProp()), "id")));
            }
            foreach (Tool t in icm.Config.Tools)
                tools.Add(Json.Obj("name", t.Name, "description", t.Description, "inputSchema", InputSchema(t)));
            return Json.Obj("tools", tools.ToArray());
        }

        // Built-in (non-instance) tools the host serves directly: KB enumeration.
        private static Dictionary<string, object> CallBuiltin(object id, Instance icm, string name, Dictionary<string, object> args)
        {
            try
            {
                if (name == "catalog")
                {
                    if (icm.Manifest == null) return ToolResult(id, "(no manifest.json)", false);
                    string text = icm.Manifest.Catalog(Json.GetString(args, "group"), Json.GetString(args, "doc_type"));
                    return ToolResult(id, text.Length > 0 ? text : "(no matching entries)", false);
                }
                if (name == "read_entry")
                {
                    string eid = Json.GetString(args, "id");
                    if (eid == null) return ToolResult(id, "read_entry needs an 'id' argument", true);
                    return ToolResult(id, icm.ReadEntry(eid), false);
                }
            }
            catch (IcmError e) { return ToolResult(id, e.Message, true); }
            return ToolResult(id, "unknown builtin: " + name, true);
        }

        private static Dictionary<string, object> Ok(object id, object result) { return Json.Obj("jsonrpc", "2.0", "id", id, "result", result); }
        private static Dictionary<string, object> Err(object id, long code, string message) { return Json.Obj("jsonrpc", "2.0", "id", id, "error", Json.Obj("code", code, "message", message)); }

        private static Dictionary<string, object> ToolResult(object id, string text, bool isError)
        {
            return Ok(id, Json.Obj("content", new object[] { Json.Obj("type", "text", "text", text) }, "isError", isError));
        }

        private static Dictionary<string, object> Handle(Instance icm, Dispatcher disp, Dictionary<string, object> msg)
        {
            string method = Json.GetStringOr(msg, "method", "");
            object id; bool hasId = msg.TryGetValue("id", out id);

            switch (method)
            {
                case "initialize":
                {
                    // Echo the client's requested protocol version when present (best compatibility);
                    // fall back to our advertised default.
                    string clientVer = Json.Pointer(msg, "/params/protocolVersion") as string;
                    string ver = string.IsNullOrEmpty(clientVer) ? ProtocolVersion : clientVer;
                    return Ok(id, Json.Obj(
                        "protocolVersion", ver,
                        "capabilities", Json.Obj("tools", Json.Obj("listChanged", false)),
                        "serverInfo", Json.Obj("name", icm.Config.Name, "version", HostVersion)));
                }
                case "notifications/initialized":
                    return null;
                case "ping":
                    return Ok(id, new Dictionary<string, object>());
                case "tools/list":
                    return Ok(id, ToolsList(icm));
                case "tools/call":
                {
                    string name = Json.Pointer(msg, "/params/name") as string;
                    if (name == null) name = "";
                    Dictionary<string, object> args = Json.AsObject(Json.Pointer(msg, "/params/arguments"));
                    if (args == null) args = new Dictionary<string, object>();
                    if (name == "catalog" || name == "read_entry") return CallBuiltin(id, icm, name, args);
                    Tool tool = null;
                    foreach (Tool t in icm.Config.Tools) if (t.Name == name) { tool = t; break; }
                    if (tool == null) return Err(id, -32602, "Unknown tool: " + name);
                    return CallTool(id, icm, disp, tool, args);
                }
                default:
                    if (hasId) return Err(id, -32601, "Method not found: " + method);
                    return null;
            }
        }

        private static Dictionary<string, object> CallTool(object id, Instance icm, Dispatcher disp, Tool tool, Dictionary<string, object> args)
        {
            string text; bool isError;
            try
            {
                switch (tool.Kind)
                {
                    case Conventions.ToolKind.Validate:
                    {
                        string table = Json.GetString(args, "table");
                        if (table == null) { text = "validate needs a 'table' argument"; isError = true; break; }
                        ValidateResult vr = disp.Validate(table, Json.GetString(args, "tsv"));
                        text = vr.ToText(MaxProblemsShown); isError = !vr.Ok;
                        break;
                    }
                    case Conventions.ToolKind.KbAnswer:
                    {
                        string q = Json.GetString(args, "question");
                        if (q == null) q = Json.GetString(args, "query");
                        if (q == null) { text = "kb_answer needs a 'question' argument"; isError = true; break; }
                        text = disp.Ask(q); isError = false;
                        break;
                    }
                    case Conventions.ToolKind.Propose:
                    case Conventions.ToolKind.GenerateVerify:
                    {
                        string table = Json.GetString(args, "table");
                        string request = Json.GetString(args, "request");
                        if (request == null) request = Json.GetString(args, "task");
                        if (table == null || request == null) { text = "propose needs 'table' and 'request' arguments"; isError = true; break; }
                        ProposeResult pr = disp.ProposeRow(table, request);
                        text = Dispatcher.FormatPropose(pr); isError = !pr.Ok;
                        break;
                    }
                    case Conventions.ToolKind.Flow:
                    {
                        string fname = Json.GetString(tool.Extra, "flow");
                        if (fname == null) fname = tool.Name;
                        string req = Json.GetString(args, "request");
                        if (req == null) req = "";
                        Flow flow = Flow.Load(icm.FlowPath(fname));
                        var engine = new FlowEngine(icm, disp, delegate(string s) { Console.Error.WriteLine("  - " + s); });
                        Dictionary<string, object> state = engine.Run(flow, req);
                        text = FlowResultText(flow, state);
                        object okv;
                        isError = state.TryGetValue("ok", out okv) && (okv is bool) && !(bool)okv;
                        break;
                    }
                    default:
                    {
                        if (tool.CommandTokens() != null)
                        {
                            ToolRunResult rr = ToolRunner.Run(icm, tool, args);
                            if (rr.Error != null) { text = rr.Error; isError = true; }
                            else { text = rr.Output.Length > 0 ? rr.Output : "(no output)"; isError = !rr.Ok; }
                        }
                        else { text = "tool kind '" + tool.Kind + "' is not implemented in the host"; isError = true; }
                        break;
                    }
                }
            }
            catch (IcmError e) { text = e.Message; isError = true; }
            return ToolResult(id, text, isError);
        }

        // Concatenate a flow's result keys (last node's outputs), falling back to common keys.
        private static string FlowResultText(Flow flow, Dictionary<string, object> state)
        {
            List<string> keys = FlowEngine.ResultKeys(flow);
            if (keys.Count == 0) keys = new List<string>(new string[] { "answer", "row", "verdict", "output", "text" });
            var sb = new System.Text.StringBuilder();
            foreach (string k in keys)
            {
                object v;
                if (state.TryGetValue(k, out v) && v != null) { if (sb.Length > 0) sb.Append("\n"); sb.Append(v.ToString()); }
            }
            return sb.Length > 0 ? sb.ToString() : "(flow produced no output)";
        }

        // Serve MCP over stdio until stdin closes. stdout carries protocol only; logs go to stderr.
        public static void Serve(Instance icm, string url)
        {
            var toolNames = new List<string>();
            foreach (Tool t in icm.Config.Tools) toolNames.Add(t.Name);
            Console.Error.WriteLine("[icm mcp] serving '" + icm.Config.Name + "' tools=[" + string.Join(", ", toolNames.ToArray()) + "] @ " + url);

            var disp = new Dispatcher(icm, url, delegate(string s) { Console.Error.WriteLine("  - " + s); });

            string line;
            while ((line = Console.In.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0) continue;
                Dictionary<string, object> msg;
                try { msg = Json.AsObject(Json.Parse(line)); }
                catch { continue; }
                if (msg == null) continue;
                Dictionary<string, object> resp = Handle(icm, disp, msg);
                if (resp != null)
                {
                    Console.Out.WriteLine(Json.Serialize(resp));
                    Console.Out.Flush();
                }
            }
        }
    }
}
