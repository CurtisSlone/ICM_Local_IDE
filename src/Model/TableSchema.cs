// TableSchema - the data model for the oracle: a table's columns and their constraints, plus the
// Problem type the oracle reports. The validation logic lives in Runtime/Oracle.cs.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal class ColSpec
    {
        public string Name = "";
        public string CType = "string"; // int | float | bool | string | enum | ref
        public bool Required = false;
        public double? Min = null;
        public double? Max = null;
        public List<string> Values = new List<string>(); // for enum
        public string RefTable = null;                    // for ref
    }

    internal class TableSchema
    {
        public string Name = "";
        // The id column other tables reference (used to build the ref set for this table).
        public string Key = null;
        public List<ColSpec> Columns = new List<ColSpec>();

        public static TableSchema Load(string path)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) { throw new IcmError("reading schema " + path + ": " + e.Message); }

            Dictionary<string, object> root;
            try { root = Json.AsObject(Json.Parse(text)); }
            catch (Exception e) { throw new IcmError("parsing schema " + path + ": " + e.Message); }
            if (root == null) throw new IcmError("parsing schema " + path + ": not a JSON object");

            var s = new TableSchema();
            s.Name = Json.GetStringOr(root, "name", "");
            s.Key = Json.GetString(root, "key");
            foreach (object c in Json.GetArr(root, "columns"))
            {
                var co = c as Dictionary<string, object>;
                if (co == null) continue;
                var col = new ColSpec();
                col.Name = Json.GetStringOr(co, "name", "");
                col.CType = Json.GetStringOr(co, "type", "string");
                col.Required = Json.GetBool(co, "required", false);
                col.Min = Json.GetNumber(co, "min");
                col.Max = Json.GetNumber(co, "max");
                foreach (object v in Json.GetArr(co, "values"))
                    if (v != null) col.Values.Add(v.ToString());
                col.RefTable = Json.GetString(co, "ref_table");
                s.Columns.Add(col);
            }
            return s;
        }
    }

    internal class Problem
    {
        public int Row;       // 1-based data row (0 = header)
        public string Col;
        public string Msg;

        public Problem(int row, string col, string msg) { Row = row; Col = col; Msg = msg; }

        public override string ToString()
        {
            if (Row == 0) return "[header] " + Col + ": " + Msg;
            return "[row " + Row + "] " + Col + ": " + Msg;
        }
    }
}
