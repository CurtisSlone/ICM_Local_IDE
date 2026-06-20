// SelfTest - a lightweight, dependency-free check of the deterministic core (the trust boundary):
// the oracle, the JSON helpers, TSV handling, argv quoting, the path-escape guard, and the path
// conventions. No model, no instance dir, no test framework needed - run with `icm selftest`.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal static class SelfTest
    {
        public static int RunAll()
        {
            int fail = 0;
            fail += Check("json roundtrip + navigation", JsonRoundtrip);
            fail += Check("json schema builders", JsonSchema);
            fail += Check("tsv lines + rows", TsvHandling);
            fail += Check("oracle pass", OraclePass);
            fail += Check("oracle catches faults", OracleFaults);
            fail += Check("argv quoting", ArgvQuoting);
            fail += Check("path-escape guard", PathGuard);
            fail += Check("path conventions", PathConventions);
            fail += Check("metadata block parse + strip", MetaBlock);
            fail += Check("json pretty-print", JsonPretty);
            fail += Check("flow branch condition", BranchCondition);
            fail += Check("manifest enumeration helpers", ManifestHelpers);
            fail += Check("markdown parse", MarkdownParse);
            fail += Check("slash command parse", SlashParse);
            fail += Check("slash redirect + fence strip", SlashRedirect);
            fail += Check("router gate", RouterGate);
            fail += Check("flow lint", FlowLintCheck);
            fail += Check("command aliases", CommandAliases);
            fail += Check("split inputs (head/tail)", SplitInputsTest);
            fail += Check("embedder rank", EmbedderRank);

            Console.WriteLine(fail == 0 ? "selftest: ALL PASS" : ("selftest: " + fail + " FAILED"));
            return fail;
        }

        private static int Check(string name, Func<bool> test)
        {
            bool ok;
            try { ok = test(); }
            catch (Exception e) { Console.WriteLine("  FAIL  " + name + " (threw: " + e.Message + ")"); return 1; }
            Console.WriteLine((ok ? "  ok    " : "  FAIL  ") + name);
            return ok ? 0 : 1;
        }

        private static bool JsonRoundtrip()
        {
            var o = Json.Obj("a", "x", "n", 2);
            Dictionary<string, object> back = Json.AsObject(Json.Parse(Json.Serialize(o)));
            double? n = Json.GetNumber(back, "n");
            return Json.GetString(back, "a") == "x" && n.HasValue && n.Value == 2;
        }

        private static bool JsonSchema()
        {
            Dictionary<string, object> s = Json.Schema(Json.Obj("x", Json.StrProp(), "k", Json.EnumProp(new string[] { "a", "b" })), "x");
            if (Json.GetString(s, "type") != "object") return false;
            List<object> req = Json.GetArr(s, "required");
            Dictionary<string, object> k = Json.GetObject(Json.GetObject(s, "properties"), "k");
            return req.Count == 1 && (string)req[0] == "x" && Json.GetArr(k, "enum").Count == 2;
        }

        private static bool TsvHandling()
        {
            List<string> lines = Tsv.NonEmptyLines("a\r\n\n  \nb\n");
            List<string[]> rows = Tsv.Rows("h1\th2\nc\td");
            return lines.Count == 2 && lines[0] == "a" && lines[1] == "b"
                && rows.Count == 2 && rows[0].Length == 2 && rows[1][1] == "d";
        }

        private static TableSchema DemoSchema()
        {
            var s = new TableSchema();
            s.Columns.Add(new ColSpec { Name = "Id", CType = "int", Required = true, Min = 1, Max = 99 });
            s.Columns.Add(new ColSpec { Name = "name", CType = "string" });
            s.Columns.Add(new ColSpec { Name = "cls", CType = "enum", Values = new List<string>(new string[] { "a", "b" }) });
            return s;
        }

        private static bool OraclePass()
        {
            return Oracle.ValidateTsv(DemoSchema(), "Id\tname\tcls\n5\tx\ta", null).Count == 0;
        }

        private static bool OracleFaults()
        {
            int range = Oracle.ValidateTsv(DemoSchema(), "Id\tname\tcls\n200\tx\tz", null).Count; // 200>99 + z not enum
            int count = Oracle.ValidateTsv(DemoSchema(), "Id\tname\tcls\n5\tx", null).Count;        // wrong column count
            int notInt = Oracle.ValidateTsv(DemoSchema(), "Id\tname\tcls\nxx\tx\ta", null).Count;    // Id not int
            return range == 2 && count == 1 && notInt == 1;
        }

        private static bool ArgvQuoting()
        {
            return ToolRunner.QuoteArg("plain") == "plain"
                && ToolRunner.QuoteArg("a b") == "\"a b\""
                && ToolRunner.QuoteArg("a\"b").Contains("\\\"");
        }

        private static bool PathGuard()
        {
            var inst = new Instance();
            inst.Root = Path.GetFullPath(Path.GetTempPath());
            // a normal relative path resolves under root
            string ok = inst.Resolve("sub/file.txt");
            if (!ok.StartsWith(inst.Root, StringComparison.OrdinalIgnoreCase)) return false;
            // '..' and absolute paths are rejected
            return Throws(delegate { inst.Resolve("../escape.txt"); })
                && Throws(delegate { inst.Resolve("C:\\Windows\\System32"); });
        }

        private static bool PathConventions()
        {
            return Conventions.SchemaRel("skills") == "schemas/skills.json"
                && Conventions.SampleRel("skills") == "samples/skills.txt"
                && Conventions.FlowRel("answer") == "flows/answer.json";
        }

        private static bool MetaBlock()
        {
            string doc = "<!--icm\n{ \"id\": \"x\", \"keywords\": [\"a\", \"b\"] }\n-->\n# Title\n\nbody text";
            Dictionary<string, object> meta = Indexer.ExtractMeta(doc);
            if (meta == null || Json.GetString(meta, "id") != "x" || Json.GetArr(meta, "keywords").Count != 2) return false;
            string stripped = Indexer.StripMeta(doc);
            if (stripped.IndexOf("icm") >= 0 || !stripped.StartsWith("# Title")) return false;
            // no block: ExtractMeta is null, StripMeta is identity
            return Indexer.ExtractMeta("# plain\ntext") == null && Indexer.StripMeta("# plain") == "# plain";
        }

        private static bool JsonPretty()
        {
            // round-trips to the same object, indents, and unescapes printable \uXXXX (here: <, ')
            string pretty = Json.SerializePretty(Json.Obj("note", "a<b's", "list", new object[] { 1, 2 }, "empty", new object[0]));
            Dictionary<string, object> back = Json.AsObject(Json.Parse(pretty));
            return back != null && Json.GetString(back, "note") == "a<b's"
                && pretty.Contains("\n  ") && pretty.Contains("[]") && Json.GetArr(back, "list").Count == 2;
        }

        private static bool SlashParse()
        {
            string cmd, rest;
            Dispatcher.ParseCommand("/write a string reverser", out cmd, out rest);
            if (cmd != "write" || rest != "a string reverser") return false;
            Dispatcher.ParseCommand("/list", out cmd, out rest);
            if (cmd != "list" || rest != "") return false;
            Dispatcher.ParseCommand("/ASK   Foo bar ", out cmd, out rest);   // case-folded, trimmed
            return cmd == "ask" && rest == "Foo bar";
        }

        private static bool EmbedderRank()
        {
            // query points along x; "a" is identical, "c" is 45deg, "b" is orthogonal -> top2 = a, c
            var q = new double[] { 1.0, 0.0 };
            var cands = new List<KeyValuePair<string, double[]>>();
            cands.Add(new KeyValuePair<string, double[]>("a", new double[] { 1.0, 0.0 }));
            cands.Add(new KeyValuePair<string, double[]>("b", new double[] { 0.0, 1.0 }));
            cands.Add(new KeyValuePair<string, double[]>("c", new double[] { 0.7, 0.7 }));
            List<string> top = Embedder.RankByVectors(q, cands, 2);
            return top.Count == 2 && top[0] == "a" && top[1] == "c";
        }

        private static bool CommandAliases()
        {
            var c = new Config();
            c.Commands.Add(new CommandAlias { Name = "compile", Tool = "build_csharp", Arg = "src" });
            c.Commands.Add(new CommandAlias { Name = "write", Flow = "write_grounded" });
            CommandAlias a = c.FindCommand("COMPILE");   // case-insensitive
            if (a == null || a.Tool != "build_csharp" || a.Arg != "src") return false;
            if (c.FindCommand("write").Flow != "write_grounded") return false;
            return c.FindCommand("nope") == null;
        }

        private static bool SplitInputsTest()
        {
            // Head tokens map 1:1; the LAST input captures the remainder (so a description with spaces
            // and an embedded path both survive intact).
            var names = new System.Collections.Generic.List<string>(new string[] { "proj", "request" });
            var d = Dispatcher.SplitInputs(names, "FunnyApp add a TCP driver to src\\Drivers");
            if (d["proj"].ToString() != "FunnyApp") return false;
            if (d["request"].ToString() != "add a TCP driver to src\\Drivers") return false;
            // Single-name list: everything is the remainder.
            var one = new System.Collections.Generic.List<string>(new string[] { "request" });
            return Dispatcher.SplitInputs(one, "  hello world  ")["request"].ToString() == "hello world";
        }

        private static bool FlowLintCheck()
        {
            var tools = new List<string>(new string[] { "csc" });

            // valid: route -> tool(csc)
            var good = new Flow();
            good.Nodes.Add(new FlowNode { Id = "a", Kind = "route" });
            var t1 = new FlowNode { Id = "b", Kind = "tool" }; t1.Extra["tool"] = "csc";
            good.Nodes.Add(t1);
            if (FlowLint.Check(good, tools).Count != 0) return false;

            // invalid: unknown kind, tool naming a missing tool, empty loop with no until/max
            var bad = new Flow();
            bad.Nodes.Add(new FlowNode { Id = "x", Kind = "frobnicate" });
            var t2 = new FlowNode { Id = "y", Kind = "tool" }; t2.Extra["tool"] = "nope"; bad.Nodes.Add(t2);
            bad.Nodes.Add(new FlowNode { Id = "z", Kind = "loop" });
            // expect >= 4 problems: unknown kind, unknown tool, empty loop body, loop missing until/max
            return FlowLint.Check(bad, tools).Count >= 4;
        }

        private static bool RouterGate()
        {
            var ids = new List<string>(new string[] { "answer", "csharp", "write_grounded" });
            return Dispatcher.Gate("csharp", "high", ids) == Dispatcher.GateDecision.Match
                && Dispatcher.Gate("csharp", "medium", ids) == Dispatcher.GateDecision.Match
                && Dispatcher.Gate("csharp", "low", ids) == Dispatcher.GateDecision.Fallback
                && Dispatcher.Gate("none", "high", ids) == Dispatcher.GateDecision.Fallback
                && Dispatcher.Gate("bogus", "high", ids) == Dispatcher.GateDecision.Fallback;
        }

        private static bool SlashRedirect()
        {
            string path;
            string rest = Dispatcher.ParseRedirect("a hex viewer > out/Hex.cs", out path);
            if (rest != "a hex viewer" || path != "out/Hex.cs") return false;
            rest = Dispatcher.ParseRedirect("no redirect here", out path);
            if (path != null || rest != "no redirect here") return false;
            if (Markdown.StripFence("```csharp\nint x = 1;\n```") != "int x = 1;") return false;
            return Markdown.StripFence("plain text") == "plain text";
        }

        private static bool MarkdownParse()
        {
            // inline: plain + bold + code spans in order
            List<MdSpan> sp = Markdown.ParseInline("use `csc` and **flags**");
            if (sp.Count != 4) return false;
            if (sp[0].Style != MdSpanStyle.Plain || sp[1].Style != MdSpanStyle.Code || sp[1].Text != "csc") return false;
            if (sp[3].Style != MdSpanStyle.Bold || sp[3].Text != "flags") return false;

            // a link span carries its href
            List<MdSpan> ln = Markdown.ParseInline("see [docs](http://x)");
            if (ln[1].Style != MdSpanStyle.Link || ln[1].Text != "docs" || ln[1].Href != "http://x") return false;

            // block kinds: heading, fenced code (fence lines not emitted), bullet
            List<MdLine> doc = Markdown.Parse("# Title\n```\ncode line\n```\n- item one");
            if (doc[0].Kind != MdLineKind.Heading || doc[0].Level != 1) return false;
            bool hasCode = false, hasBullet = false, hasFenceText = false;
            foreach (MdLine l in doc)
            {
                if (l.Kind == MdLineKind.Code) { hasCode = true; if (l.Raw != "code line") return false; }
                if (l.Kind == MdLineKind.Bullet) hasBullet = true;
                if (l.Kind == MdLineKind.Paragraph && l.Spans.Count > 0 && l.Spans[0].Text.Contains("```")) hasFenceText = true;
            }
            return hasCode && hasBullet && !hasFenceText;
        }

        private static bool ManifestHelpers()
        {
            var m = new Manifest();
            m.Entries.Add(new Entry { Id = "a", Group = "creational", DocType = "pattern", Summary = "sa" });
            m.Entries.Add(new Entry { Id = "b", Group = "structural", DocType = "pattern", Summary = "sb" });
            m.Entries.Add(new Entry { Id = "c", Group = "creational", DocType = "pattern", Summary = "sc" });
            m.Entries.Add(new Entry { Id = "d", Group = "", DocType = "reference", Summary = "sd" });
            if (m.Groups().Count != 2) return false;                 // creational, structural
            if (m.ByGroup("creational").Count != 2) return false;
            if (m.ByDocType("reference").Count != 1) return false;
            string cat = m.Catalog("creational", null);
            return cat.Contains("a [creational]: sa") && cat.Contains("c [creational]: sc") && !cat.Contains("- b");
        }

        private static bool BranchCondition()
        {
            var st = new Dictionary<string, object>();
            st["empty"] = "";
            st["filled"] = "some context";
            st["flag"] = true;
            // empty/nonempty test the trimmed string; truthy/falsy read it as a bool
            return FlowEngine.BranchTaken(st, "empty", "empty")
                && !FlowEngine.BranchTaken(st, "filled", "empty")
                && FlowEngine.BranchTaken(st, "filled", "nonempty")
                && FlowEngine.BranchTaken(st, "flag", "truthy")
                && !FlowEngine.BranchTaken(st, "flag", "falsy")
                && FlowEngine.BranchTaken(st, "missing", "empty"); // absent key reads as empty
        }

        private static bool Throws(Action a)
        {
            try { a(); return false; } catch (IcmError) { return true; } catch { return false; }
        }
    }
}
