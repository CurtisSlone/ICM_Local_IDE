// Embedder - the ICM "embedder" role: narrow a candidate set to the top-k most similar to a query,
// so the constrained model pick (flow router, KB route) chooses from a short, relevant list instead
// of the whole catalog. The embedder NEVER decides or generates - it only ranks. Falls back to "use
// all candidates" whenever the embed model is absent or Ollama is unreachable (returns null).
//
// Vectors are cached (model-keyed, keyed by candidate id + a content hash so edits re-embed) in
// refdocs/.emb_cache.routing.json - the same gitignored area as the search caches.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal class Cand
    {
        public string Id;
        public string Text;
        public Cand(string id, string text) { Id = id; Text = text; }
    }

    internal static class Embedder
    {
        private const string CacheFile = "/.emb_cache.routing.json";

        // Top-k candidate ids by cosine similarity to the query, in rank order. Returns null if
        // embeddings are unavailable (the caller should then use all candidates).
        public static List<string> RankTopK(Instance icm, string url, string model, string query, List<Cand> cands, int k, Action<string> status)
        {
            if (string.IsNullOrEmpty(model) || cands == null || cands.Count == 0) return null;
            try
            {
                Dictionary<string, double[]> cache = LoadCache(icm, model);
                double[] qv = Ollama.Embed(url, model, query);
                if (qv == null || qv.Length == 0) return null;

                bool dirty = false;
                var vecs = new List<KeyValuePair<string, double[]>>();
                foreach (Cand c in cands)
                {
                    string key = c.Id + "#" + Hash(c.Text);
                    double[] cv;
                    if (!cache.TryGetValue(key, out cv) || cv == null)
                    {
                        cv = Ollama.Embed(url, model, c.Text);
                        cache[key] = cv; dirty = true;
                    }
                    vecs.Add(new KeyValuePair<string, double[]>(c.Id, cv));
                }
                if (dirty) SaveCache(icm, model, cache);
                return RankByVectors(qv, vecs, k);
            }
            catch (Exception e)
            {
                if (status != null) status("embedder unavailable (" + e.Message + "); using all candidates");
                return null;
            }
        }

        // Pure ranking over already-computed vectors (testable without Ollama).
        internal static List<string> RankByVectors(double[] q, List<KeyValuePair<string, double[]>> cands, int k)
        {
            var scored = new List<KeyValuePair<string, double>>();
            foreach (KeyValuePair<string, double[]> c in cands)
                scored.Add(new KeyValuePair<string, double>(c.Key, Cosine(q, c.Value)));
            scored.Sort(delegate(KeyValuePair<string, double> a, KeyValuePair<string, double> b) { return b.Value.CompareTo(a.Value); });
            var outl = new List<string>();
            for (int i = 0; i < scored.Count && i < k; i++) outl.Add(scored[i].Key);
            return outl;
        }

        internal static double Cosine(double[] a, double[] b)
        {
            if (a == null || b == null) return 0;
            double dot = 0, na = 0, nb = 0; int m = Math.Min(a.Length, b.Length);
            for (int i = 0; i < m; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
            if (na == 0 || nb == 0) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        // A stable content hash (FNV-1a, hex) so a cache key changes when the candidate text changes.
        private static string Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                if (s != null) for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
                return h.ToString("x8");
            }
        }

        private static string CachePath(Instance icm) { return icm.Resolve(Conventions.RefdocsDir + CacheFile); }

        private static Dictionary<string, double[]> LoadCache(Instance icm, string model)
        {
            var cache = new Dictionary<string, double[]>();
            try
            {
                string p = CachePath(icm);
                if (!File.Exists(p)) return cache;
                Dictionary<string, object> root = Json.AsObject(Json.Parse(File.ReadAllText(p)));
                if (root == null || Json.GetStringOr(root, "model", "") != model) return cache; // model-keyed
                Dictionary<string, object> vecs = Json.GetObject(root, "vecs");
                if (vecs != null) foreach (var kv in vecs) cache[kv.Key] = ToDoubleArr(Json.AsArr(kv.Value));
            }
            catch { }
            return cache;
        }

        private static void SaveCache(Instance icm, string model, Dictionary<string, double[]> cache)
        {
            try
            {
                string p = CachePath(icm);
                string dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var vecs = new Dictionary<string, object>();
                foreach (var kv in cache) vecs[kv.Key] = kv.Value;
                File.WriteAllText(p, Json.Serialize(Json.Obj("model", model, "vecs", vecs)));
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
