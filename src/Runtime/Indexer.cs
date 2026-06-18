// Indexer - the routable foundation (the knowledge-oracle pattern from CMMC-Claude-Companion's
// _index_generator.py, adapted to this host). Each routable reference file leads with a metadata
// block in an HTML comment:
//
//   <!--icm
//   { "id": "...", "title": "...", "doc_type": "reference",
//     "summary": "one sharp line - the only thing routing sees",
//     "keywords": ["..."], "source": { "origin": "...", "url": "...", "retrieved": "..." } }
//   -->
//
// `icm reindex` scans the routable folders, reads those blocks, and regenerates manifest.json
// MECHANICALLY (no LLM summarization) so the routing metadata lives with each file. Grounding reads
// strip the block (StripMeta) so the model sees clean content; provenance stays available for
// citations.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal static class Indexer
    {
        // The metadata object inside a file's <!--icm ... --> block, or null if absent/invalid.
        public static Dictionary<string, object> ExtractMeta(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int a = text.IndexOf(Conventions.MetaOpen, StringComparison.Ordinal);
            if (a < 0) return null;
            int start = a + Conventions.MetaOpen.Length;
            int b = text.IndexOf(Conventions.MetaClose, start, StringComparison.Ordinal);
            if (b < 0) return null;
            string json = text.Substring(start, b - start).Trim();
            try { return Json.AsObject(Json.Parse(json)); }
            catch { return null; }
        }

        // The file content with a leading metadata block removed (for grounding).
        public static string StripMeta(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int a = text.IndexOf(Conventions.MetaOpen, StringComparison.Ordinal);
            if (a < 0) return text;
            int b = text.IndexOf(Conventions.MetaClose, a, StringComparison.Ordinal);
            if (b < 0) return text;
            return text.Remove(a, b + Conventions.MetaClose.Length - a).TrimStart('\r', '\n', ' ', '\t');
        }

        private static string FirstHeading(string text)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("# ")) return line.Substring(2).Trim();
            }
            return null;
        }

        // Scan the routable folders, read each file's metadata block, and regenerate manifest.json.
        public static int Reindex(Instance icm, Action<string> status)
        {
            if (status == null) status = delegate(string s) { };

            // Preserve the manifest header (name/description/domain) if present, else from config.
            string name = icm.Config.Name, description = "", domain = icm.Config.Domain;
            string manifestPath = Path.Combine(icm.Root, Conventions.ManifestFile);
            if (File.Exists(manifestPath))
            {
                try
                {
                    Dictionary<string, object> old = Json.AsObject(Json.Parse(File.ReadAllText(manifestPath)));
                    if (old != null) { name = Json.GetStringOr(old, "name", name); description = Json.GetStringOr(old, "description", description); domain = Json.GetStringOr(old, "domain", domain); }
                }
                catch { }
            }

            var entries = new List<object>();
            int count = 0;
            foreach (string d in Conventions.RoutableDirs)
            {
                string dir = Path.Combine(icm.Root, d);
                if (!Directory.Exists(dir)) continue;
                // Recursive: a layer may be organized into sub-folders (e.g. patterns/creational,
                // reference/dotnet). The sub-folder becomes the entry's `group`.
                string[] files = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    string fn = Path.GetFileName(f);
                    if (fn.Equals("README.md", StringComparison.OrdinalIgnoreCase)) continue; // folder guides

                    // rel: path under the instance root, forward-slashed (e.g. "patterns/creational/builder.md").
                    string rel = f.Substring(icm.Root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    // group: the folders between the layer and the file (e.g. "creational"); "" if directly in the layer.
                    string under = rel.Length > d.Length ? rel.Substring(d.Length + 1) : fn; // path below the layer dir
                    int lastSlash = under.LastIndexOf('/');
                    string group = lastSlash >= 0 ? under.Substring(0, lastSlash) : "";

                    string text;
                    try { text = File.ReadAllText(f); } catch { continue; }
                    Dictionary<string, object> meta = ExtractMeta(text);
                    if (meta == null) { status("skip (no <!--icm--> block): " + rel); continue; }

                    // Default id from the path (unique across sub-folders) when the block omits one.
                    string defaultId = (d + "/" + under).Replace(".md", "").Replace('/', '-');
                    string id = Json.GetStringOr(meta, "id", defaultId);
                    string title = Json.GetStringOr(meta, "title", FirstHeading(text) ?? id);
                    string summary = Json.GetStringOr(meta, "summary", "");
                    string docType = Json.GetStringOr(meta, "doc_type", d);
                    string grp = Json.GetStringOr(meta, "group", group);
                    var kws = new List<object>();
                    foreach (object kw in Json.GetArr(meta, "keywords")) if (kw != null) kws.Add(kw.ToString());

                    entries.Add(Json.Obj("id", id, "title", title, "path", rel,
                        "summary", summary, "doc_type", docType, "group", grp, "keywords", kws.ToArray()));
                    count++;
                }
            }

            var root = Json.Obj(
                "$comment", "Generated by `icm reindex` from each file's <!--icm--> metadata block. Edit the block in the source file, not this file.",
                "name", name, "description", description, "domain", domain, "entries", entries.ToArray());
            File.WriteAllText(manifestPath, Json.SerializePretty(root));
            status("reindex: wrote " + count + " entries -> " + Conventions.ManifestFile);
            return count;
        }
    }
}
