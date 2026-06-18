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
