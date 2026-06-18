// The deterministic oracle: a schema-driven TSV validator. This is the D2 "compiler".
//
// The model proposes an edited row; this says yes or no, deterministically. The host stays
// domain-agnostic: it runs a generic validator against whatever schema the instance hands it.
//
// Checks: header has the columns the schema names; every row has the right column COUNT (the classic
// tab-corruption catch); each typed cell parses and sits in range; enum cells are in the allowed
// set; and `ref` cells resolve against another table's id set when one is provided.
//
// Data types (ColSpec/TableSchema/Problem) live in Model/TableSchema.cs; line handling in Tsv.cs.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Icm
{
    internal static class Oracle
    {
        // Build the id set for a table from its schema `key` column (for cross-table refs).
        public static HashSet<string> IdSet(TableSchema schema, string tsv)
        {
            var outSet = new HashSet<string>();
            if (string.IsNullOrEmpty(schema.Key)) return outSet;
            List<string[]> table = Tsv.Rows(tsv);
            if (table.Count == 0) return outSet;
            string[] header = table[0];
            int ki = Array.IndexOf(header, schema.Key);
            if (ki < 0) return outSet;
            for (int r = 1; r < table.Count; r++)
                if (ki < table[r].Length)
                {
                    string v = table[r][ki];
                    if (v.Trim().Length != 0) outSet.Add(v);
                }
            return outSet;
        }

        // Pass null `refs` to skip ref resolution (single-table validation); pass a built map
        // (table name -> valid ids) for cross-table integrity.
        public static List<Problem> ValidateTsv(TableSchema schema, string tsv, Dictionary<string, HashSet<string>> refs)
        {
            var problems = new List<Problem>();
            List<string[]> table = Tsv.Rows(tsv);
            if (table.Count == 0)
            {
                problems.Add(new Problem(0, "(file)", "empty file"));
                return problems;
            }
            string[] header = table[0];
            int ncols = header.Length;

            var idx = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++) idx[header[i]] = i; // last wins, mirrors the Rust insert

            var targets = new List<KeyValuePair<ColSpec, int>>();
            foreach (ColSpec c in schema.Columns)
            {
                int i;
                if (idx.TryGetValue(c.Name, out i)) targets.Add(new KeyValuePair<ColSpec, int>(c, i));
                else problems.Add(new Problem(0, c.Name, "column declared in schema is missing from the header"));
            }

            for (int ri = 1; ri < table.Count; ri++)
            {
                string[] r = table[ri];
                // THE tab-corruption catch: every row must have the header's column count.
                if (r.Length != ncols)
                {
                    problems.Add(new Problem(ri, "(row)", "has " + r.Length + " columns, expected " + ncols + " (tab added/dropped?)"));
                    continue; // index-based cell checks would be meaningless on a misaligned row
                }
                foreach (KeyValuePair<ColSpec, int> t in targets)
                {
                    ColSpec c = t.Key;
                    string cell = r[t.Value].Trim();
                    if (cell.Length == 0)
                    {
                        if (c.Required) problems.Add(new Problem(ri, c.Name, "required, but empty"));
                        continue;
                    }
                    switch (c.CType)
                    {
                        case "int":
                        {
                            long n;
                            if (long.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) CheckRange(problems, ri, c, n);
                            else problems.Add(new Problem(ri, c.Name, "'" + cell + "' is not an integer"));
                            break;
                        }
                        case "float":
                        {
                            double n;
                            if (double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) CheckRange(problems, ri, c, n);
                            else problems.Add(new Problem(ri, c.Name, "'" + cell + "' is not a number"));
                            break;
                        }
                        case "bool":
                            if (cell != "0" && cell != "1") problems.Add(new Problem(ri, c.Name, "'" + cell + "' must be 0 or 1"));
                            break;
                        case "enum":
                            if (!c.Values.Contains(cell))
                                problems.Add(new Problem(ri, c.Name, "'" + cell + "' not in [" + string.Join(", ", c.Values.ToArray()) + "]"));
                            break;
                        case "ref":
                            if (c.RefTable != null && refs != null)
                            {
                                HashSet<string> set;
                                bool okRef = refs.TryGetValue(c.RefTable, out set) && set.Contains(cell);
                                if (!okRef) problems.Add(new Problem(ri, c.Name, "'" + cell + "' not found in table '" + c.RefTable + "'"));
                            }
                            break; // refs == null: single-table mode, ref integrity skipped by design
                        default:
                            break; // "string": any non-empty value is fine
                    }
                }
            }
            return problems;
        }

        private static void CheckRange(List<Problem> problems, int ri, ColSpec c, double n)
        {
            if (c.Min.HasValue && n < c.Min.Value) problems.Add(new Problem(ri, c.Name, Num(n) + " < min " + Num(c.Min.Value)));
            if (c.Max.HasValue && n > c.Max.Value) problems.Add(new Problem(ri, c.Name, Num(n) + " > max " + Num(c.Max.Value)));
        }

        // Render a number without a trailing ".0" for whole values, matching the Rust output.
        private static string Num(double n)
        {
            if (n == Math.Floor(n) && !double.IsInfinity(n)) return ((long)n).ToString(CultureInfo.InvariantCulture);
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
