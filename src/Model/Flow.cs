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
        public Dictionary<string, object> Extra = new Dictionary<string, object>();
    }

    internal class Flow
    {
        public string Name = "";
        public string Description = "";
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
            foreach (object o in Json.GetArr(root, "nodes"))
            {
                var no = o as Dictionary<string, object>;
                if (no == null) continue;
                var n = new FlowNode();
                n.Id = Json.GetStringOr(no, "id", "");
                n.Kind = Json.GetStringOr(no, "kind", "");
                foreach (object i in Json.GetArr(no, "inputs")) if (i != null) n.Inputs.Add(i.ToString());
                foreach (object ot in Json.GetArr(no, "outputs")) if (ot != null) n.Outputs.Add(ot.ToString());
                foreach (var kv in no)
                    if (kv.Key != "id" && kv.Key != "kind" && kv.Key != "inputs" && kv.Key != "outputs")
                        n.Extra[kv.Key] = kv.Value;
                f.Nodes.Add(n);
            }
            return f;
        }
    }
}
