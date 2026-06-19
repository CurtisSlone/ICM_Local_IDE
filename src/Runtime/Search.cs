// Search - hybrid reference search over a built corpus (refdocs/<corpus>.json), the host-side port of
// the Rust assistant's icm_docs: BM25-lite ranks the whole corpus into a shortlist, then (if an
// embedder is reachable) embeddings re-rank the shortlist and Reciprocal Rank Fusion fuses the two
// orders. Falls back to BM25-only when embeddings are unavailable. The corpus is built by an
// instance's tools (e.g. build_dotnet_docs.ps1); this is the generic search engine over it.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Icm
{
    internal static class Search
    {
        private static readonly Regex TokRe = new Regex("[a-z0-9_]+", RegexOptions.Compiled);
        private const int Shortlist = 20;
        private const int RrfK = 60;
        private const double K1 = 1.5;
        private const double B = 0.75;

        private class Doc { public string Id = ""; public string Title = ""; public string Kind = ""; public string Text = ""; }
        private class Row { public int Idx; public int BmRank; public int EmbRank; public double Sim; public double Rrf; }

        // Returns the top-k chunks as readable grounding text. `useEmbed` adds the embedding rerank.
        public static string Run(Instance icm, string url, string corpus, string query, int k, bool useEmbed, string embedModel, Action<string> status)
        {
            if (status == null) status = delegate(string s) { };
            // Prefer a locally (re)built corpus in refdocs/; fall back to the shipped, tracked
            // corpus in refdocs-seed/. (Vector caches always live in refdocs/, keyed by corpus + model.)
            string path = icm.Resolve(Conventions.RefdocRel(corpus));
            if (!File.Exists(path))
            {
                string seed = icm.Resolve(Conventions.RefdocSeedRel(corpus));
                if (File.Exists(seed)) path = seed;
                else throw new IcmError("corpus not built: " + Conventions.RefdocRel(corpus) +
                    " (ship it in " + Conventions.RefdocsSeedDir + "/ or run the build_*_docs tool)");
            }

            List<object> arr = Json.AsArr(Json.Parse(File.ReadAllText(path)));
            var docs = new List<Doc>();
            foreach (object o in arr)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                docs.Add(new Doc { Id = Json.GetStringOr(d, "id", ""), Title = Json.GetStringOr(d, "title", ""), Kind = Json.GetStringOr(d, "kind", ""), Text = Json.GetStringOr(d, "text", "") });
            }
            int n = docs.Count;
            if (n == 0) return "(empty corpus)";

            // --- BM25 index ---
            var tf = new List<Dictionary<string, int>>(n);
            var len = new int[n];
            var df = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                List<string> toks = Tokens(docs[i].Title + " " + docs[i].Text);
                var h = new Dictionary<string, int>();
                foreach (string w in toks) { int c; h[w] = h.TryGetValue(w, out c) ? c + 1 : 1; }
                tf.Add(h); len[i] = toks.Count;
                foreach (string w in h.Keys) { int c; df[w] = df.TryGetValue(w, out c) ? c + 1 : 1; }
            }
            double avgdl = 0; for (int i = 0; i < n; i++) avgdl += len[i]; if (n > 0) avgdl /= n;

            var qset = new HashSet<string>(Tokens(query));
            var scores = new List<KeyValuePair<int, double>>();
            for (int i = 0; i < n; i++)
            {
                double s = 0; int dl = len[i];
                foreach (string w in qset)
                {
                    int f; if (!tf[i].TryGetValue(w, out f)) continue;
                    int dfw = df[w];
                    double idf = Math.Log(1 + (n - dfw + 0.5) / (dfw + 0.5));
                    s += idf * (f * (K1 + 1)) / (f + K1 * (1 - B + B * dl / avgdl));
                }
                if (s > 0) scores.Add(new KeyValuePair<int, double>(i, s));
            }
            if (scores.Count == 0) return "(no matches for '" + query + "')";
            scores.Sort(delegate(KeyValuePair<int, double> a, KeyValuePair<int, double> c) { return c.Value.CompareTo(a.Value); });

            int sl = Math.Min(Shortlist, scores.Count);
            var rows = new List<Row>();
            for (int r = 0; r < sl; r++) rows.Add(new Row { Idx = scores[r].Key, BmRank = r, EmbRank = r, Sim = 0 });

            // --- optional embedding rerank ---
            if (useEmbed)
            {
                try
                {
                    status("docsearch: embedding rerank (" + embedModel + ")");
                    Dictionary<string, double[]> cache = LoadCache(icm, corpus, embedModel);
                    double[] qv = Ollama.Embed(url, embedModel, query);
                    bool dirty = false;
                    foreach (Row row in rows)
                    {
                        Doc d = docs[row.Idx];
                        double[] vec;
                        if (!cache.TryGetValue(d.Id, out vec) || vec == null)
                        {
                            string snip = d.Title + ". " + d.Text;
                            if (snip.Length > 512) snip = snip.Substring(0, 512);
                            vec = Ollama.Embed(url, embedModel, snip); cache[d.Id] = vec; dirty = true;
                        }
                        row.Sim = Cosine(qv, vec);
                    }
                    if (dirty) SaveCache(icm, corpus, embedModel, cache);
                    var bySim = new List<Row>(rows);
                    bySim.Sort(delegate(Row a, Row c) { return c.Sim.CompareTo(a.Sim); });
                    for (int e = 0; e < bySim.Count; e++) bySim[e].EmbRank = e;
                }
                catch (Exception e) { status("docsearch: embeddings unavailable (" + e.Message + "); BM25 only"); }
            }

            foreach (Row row in rows) row.Rrf = 1.0 / (RrfK + row.BmRank) + 1.0 / (RrfK + row.EmbRank);
            rows.Sort(delegate(Row a, Row c) { return c.Rrf.CompareTo(a.Rrf); });

            var sb = new StringBuilder();
            int shown = 0;
            foreach (Row row in rows)
            {
                if (shown++ >= k) break;
                Doc d = docs[row.Idx];
                sb.Append("## " + d.Title + "  (" + d.Kind + ")\n" + d.Text + "\n\n");
            }
            return sb.ToString().TrimEnd();
        }

        private static List<string> Tokens(string s)
        {
            var outl = new List<string>();
            if (string.IsNullOrEmpty(s)) return outl;
            foreach (Match m in TokRe.Matches(s.ToLowerInvariant())) outl.Add(m.Value);
            return outl;
        }

        private static double Cosine(double[] a, double[] b)
        {
            double dot = 0, na = 0, nb = 0; int m = Math.Min(a.Length, b.Length);
            for (int i = 0; i < m; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
            if (na == 0 || nb == 0) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        private static string CachePath(Instance icm, string corpus) { return icm.Resolve(Conventions.RefdocsDir + "/.emb_cache." + corpus + ".json"); }

        private static Dictionary<string, double[]> LoadCache(Instance icm, string corpus, string model)
        {
            var cache = new Dictionary<string, double[]>();
            try
            {
                string p = CachePath(icm, corpus);
                if (!File.Exists(p)) return cache;
                Dictionary<string, object> root = Json.AsObject(Json.Parse(File.ReadAllText(p)));
                if (root == null || Json.GetStringOr(root, "model", "") != model) return cache; // model-keyed
                Dictionary<string, object> vecs = Json.GetObject(root, "vecs");
                if (vecs != null) foreach (var kv in vecs) cache[kv.Key] = ToDoubleArr(Json.AsArr(kv.Value));
            }
            catch { }
            return cache;
        }

        private static void SaveCache(Instance icm, string corpus, string model, Dictionary<string, double[]> cache)
        {
            try
            {
                var vecs = new Dictionary<string, object>();
                foreach (var kv in cache) vecs[kv.Key] = kv.Value;
                var root = new Dictionary<string, object>();
                root["model"] = model; root["vecs"] = vecs;
                File.WriteAllText(CachePath(icm, corpus), Json.Serialize(root));
            }
            catch { }
        }

        private static double[] ToDoubleArr(List<object> nums)
        {
            var a = new double[nums.Count];
            for (int i = 0; i < nums.Count; i++) { double? d = Json.ToDouble(nums[i]); a[i] = d.HasValue ? d.Value : 0; }
            return a;
        }
    }
}
