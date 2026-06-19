// Flow / FlowNode - the data model for an authored workflow (flows/<name>.json). The execution
// engine lives in Runtime/FlowEngine.cs.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal class FlowNode
    {
        public string Id = "";
        public string Kind = "";
        public List<string> Inputs = new List<string>();
        public List<string> Outputs = new List<string>();
        public List<FlowNode> Body = new List<FlowNode>(); // child nodes, for `loop`
        public List<FlowNode> Then = new List<FlowNode>(); // child nodes taken when a `branch` condition holds
        public List<FlowNode> Else = new List<FlowNode>(); // child nodes taken when it does not
        public Dictionary<string, object> Extra = new Dictionary<string, object>();
    }

    // Lightweight flow metadata for the conversational router's catalog (no nodes loaded).
    internal class FlowInfo
    {
        public string Id = "";          // the file name without extension - how `icm flow <id>` refers to it
        public string Name = "";
        public string WhenToUse = "";   // the router's match surface ("use this when...")
    }

    internal class Flow
    {
        public string Name = "";
        public string Description = "";
        public string WhenToUse = "";   // router match text; falls back to Description
        public List<FlowNode> Nodes = new List<FlowNode>();

        public static Flow Load(string path)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) { throw new IcmError("reading flow " + path + ": " + e.Message); }
            Dictionary<string, object> root;
            try { root = Json.AsObject(Json.Parse(text)); }
            catch (Exception e) { throw new IcmError("parsing flow " + path + ": " + e.Message); }
            if (root == null) throw new IcmError("parsing flow " + path + ": not a JSON object");

            var f = new Flow();
            f.Name = Json.GetStringOr(root, "name", Path.GetFileNameWithoutExtension(path));
            f.Description = Json.GetStringOr(root, "description", "");
            f.WhenToUse = Json.GetStringOr(root, "whenToUse", f.Description);
            foreach (object o in Json.GetArr(root, "nodes"))
            {
                var no = o as Dictionary<string, object>;
                if (no != null) f.Nodes.Add(ParseNode(no));
            }
            return f;
        }

        // Parse one node, recursing into a `body` array (for `loop` nodes).
        private static FlowNode ParseNode(Dictionary<string, object> no)
        {
            var n = new FlowNode();
            n.Id = Json.GetStringOr(no, "id", "");
            n.Kind = Json.GetStringOr(no, "kind", "");
            foreach (object i in Json.GetArr(no, "inputs")) if (i != null) n.Inputs.Add(i.ToString());
            foreach (object ot in Json.GetArr(no, "outputs")) if (ot != null) n.Outputs.Add(ot.ToString());
            ParseChildren(no, "body", n.Body);
            ParseChildren(no, "then", n.Then);
            ParseChildren(no, "else", n.Else);
            foreach (var kv in no)
                if (kv.Key != "id" && kv.Key != "kind" && kv.Key != "inputs" && kv.Key != "outputs"
                    && kv.Key != "body" && kv.Key != "then" && kv.Key != "else")
                    n.Extra[kv.Key] = kv.Value;
            return n;
        }

        private static void ParseChildren(Dictionary<string, object> no, string key, List<FlowNode> into)
        {
            foreach (object b in Json.GetArr(no, key))
            {
                var bo = b as Dictionary<string, object>;
                if (bo != null) into.Add(ParseNode(bo));
            }
        }
    }
}
