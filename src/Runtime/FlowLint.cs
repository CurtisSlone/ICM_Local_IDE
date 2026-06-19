// FlowLint - a deterministic structural check for an authored flow, so a hand- or agent-written
// flows/<name>.json can be validated before it runs. Catches the mistakes the engine would otherwise
// hit at run time (or silently skip): unknown node kinds, missing ids/kinds, duplicate ids, a tool
// node naming a tool the instance does not declare, an empty loop body, a branch with no when, etc.
// Pure + static (takes the declared tool names, not the Instance) so SelfTest can cover it.

using System;
using System.Collections.Generic;

namespace Icm
{
    internal static class FlowLint
    {
        private static readonly string[] Kinds = {
            Conventions.Node.Route, Conventions.Node.Read, Conventions.Node.Generate,
            Conventions.Node.Answer, Conventions.Node.Propose, Conventions.Node.Validate,
            Conventions.Node.Tool, Conventions.Node.Loop, Conventions.Node.Branch,
            Conventions.Node.Search, Conventions.Node.RouteMany, Conventions.Node.Catalog
        };

        // Returns a list of human-readable problems; empty means the flow is structurally valid.
        public static List<string> Check(Flow flow, List<string> toolNames)
        {
            var problems = new List<string>();
            if (flow == null) { problems.Add("flow is null"); return problems; }
            if (flow.Nodes.Count == 0) problems.Add("flow has no nodes");
            CheckNodes(flow.Nodes, toolNames, problems, new HashSet<string>(), "");
            return problems;
        }

        private static void CheckNodes(List<FlowNode> nodes, List<string> tools, List<string> problems, HashSet<string> seen, string ctx)
        {
            foreach (FlowNode n in nodes)
            {
                string where = ctx + (n.Id.Length > 0 ? "node '" + n.Id + "'" : "node (no id)");
                if (n.Id.Length == 0) problems.Add(where + ": missing 'id'");
                else if (!seen.Add(n.Id)) problems.Add(where + ": duplicate id '" + n.Id + "'");

                if (n.Kind.Length == 0) { problems.Add(where + ": missing 'kind'"); continue; }
                if (Array.IndexOf(Kinds, n.Kind) < 0) { problems.Add(where + ": unknown kind '" + n.Kind + "'"); continue; }

                if (n.Kind == Conventions.Node.Tool)
                {
                    string tool = Json.GetString(n.Extra, "tool");
                    if (string.IsNullOrEmpty(tool)) problems.Add(where + ": tool node needs a 'tool' field");
                    else if (tools != null && !tools.Contains(tool)) problems.Add(where + ": references unknown tool '" + tool + "'");
                }
                else if (n.Kind == Conventions.Node.Search)
                {
                    if (string.IsNullOrEmpty(Json.GetString(n.Extra, "corpus")))
                        problems.Add(where + ": search node needs a 'corpus' field");
                }
                else if (n.Kind == Conventions.Node.Loop)
                {
                    if (n.Body.Count == 0) problems.Add(where + ": loop has an empty 'body'");
                    if (string.IsNullOrEmpty(Json.GetString(n.Extra, "until")) && Json.GetNumber(n.Extra, "maxIterations") == null)
                        problems.Add(where + ": loop has neither 'until' nor 'maxIterations'");
                    CheckNodes(n.Body, tools, problems, seen, ctx + "loop '" + n.Id + "' > ");
                }
                else if (n.Kind == Conventions.Node.Branch)
                {
                    if (string.IsNullOrEmpty(Json.GetString(n.Extra, "when")))
                        problems.Add(where + ": branch node needs a 'when' field");
                    if (n.Then.Count == 0 && n.Else.Count == 0)
                        problems.Add(where + ": branch has empty 'then' and 'else'");
                    CheckNodes(n.Then, tools, problems, seen, ctx + "branch '" + n.Id + "' then > ");
                    CheckNodes(n.Else, tools, problems, seen, ctx + "branch '" + n.Id + "' else > ");
                }
            }
        }
    }
}
