// Manifest loader (manifest.json): the routing index the dispatcher picks on. Port of
// manifest.rs. The summaries are the only thing routing sees; the host reads this to know what
// KB entries exist and where they live.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal class Entry
    {
        public string Id = "";
        public string Title = "";
        public string Path = "";
        public string Summary = "";
        public string DocType = "";
        public string Group = "";   // sub-folder under the routable layer, e.g. "dotnet" or "creational"
        public List<string> Keywords = new List<string>();
    }

    internal class Manifest
    {
        public string Name = "";
        public string Description = "";
        public List<Entry> Entries = new List<Entry>();

        public static Manifest Load(string path)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) { throw new IcmError("reading " + path + ": " + e.Message); }

            Dictionary<string, object> root;
            try { root = Json.AsObject(Json.Parse(text)); }
            catch (Exception e) { throw new IcmError("parsing " + path + ": " + e.Message); }
            if (root == null) throw new IcmError("parsing " + path + ": not a JSON object");

            var m = new Manifest();
            m.Name = Json.GetStringOr(root, "name", "");
            m.Description = Json.GetStringOr(root, "description", "");
            foreach (object e in Json.GetArr(root, "entries"))
            {
                var eo = e as Dictionary<string, object>;
                if (eo == null) continue;
                var entry = new Entry();
                entry.Id = Json.GetStringOr(eo, "id", "");
                entry.Title = Json.GetStringOr(eo, "title", "");
                entry.Path = Json.GetStringOr(eo, "path", "");
                entry.Summary = Json.GetStringOr(eo, "summary", "");
                entry.DocType = Json.GetStringOr(eo, "doc_type", "");
                entry.Group = Json.GetStringOr(eo, "group", "");
                foreach (object kw in Json.GetArr(eo, "keywords")) if (kw != null) entry.Keywords.Add(kw.ToString());
                m.Entries.Add(entry);
            }
            return m;
        }

        public Entry GetEntry(string id)
        {
            foreach (Entry e in Entries) if (e.Id == id) return e;
            return null;
        }
    }
}
