// A loaded ICM: the root directory plus its config and (optional) manifest, with sandboxed file IO.
// "Open a directory and land in the ICM" is exactly Instance.Open.
//
// The file IO is deliberately scoped to the ICM root: read/write that cannot escape the instance
// directory. The host owns this so no instance tool (or model proposal) can wander the filesystem.

using System;
using System.IO;

namespace Icm
{
    // The Result<_, String> analogue: errors are carried as a message, surfaced at the CLI edge.
    internal class IcmError : Exception
    {
        public IcmError(string message) : base(message) { }
    }

    internal class Instance
    {
        public string Root;            // absolute, normalized
        public Config Config;
        public Manifest Manifest;      // null when the instance has no manifest.json

        public static Instance Open(string dir)
        {
            string root;
            try { root = Path.GetFullPath(dir); }
            catch (Exception e) { throw new IcmError("opening ICM dir " + dir + ": " + e.Message); }
            if (!Directory.Exists(root))
                throw new IcmError("opening ICM dir " + dir + ": not a directory");

            var inst = new Instance();
            inst.Root = root;
            string cfgPath = Path.Combine(root, Conventions.ConfigFile);
            inst.Config = File.Exists(cfgPath)
                ? Config.Load(cfgPath)
                : Config.Default(Path.GetFileName(root.TrimEnd('\\', '/')));
            string manifestPath = Path.Combine(root, Conventions.ManifestFile);
            inst.Manifest = File.Exists(manifestPath) ? Manifest.Load(manifestPath) : null;
            return inst;
        }

        // Resolve a relative path inside the ICM, refusing anything that escapes the root. Rejects
        // absolute paths and `..` up front, then confirms the joined path is still under root. Works
        // for not-yet-existing files (write), so it does not depend on the target existing.
        public string Resolve(string rel)
        {
            if (string.IsNullOrEmpty(rel))
                throw new IcmError("empty path");
            if (Path.IsPathRooted(rel))
                throw new IcmError("path '" + rel + "' must be relative to the ICM dir");
            foreach (string part in rel.Split('/', '\\'))
                if (part == "..")
                    throw new IcmError("path '" + rel + "' may not contain '..'");

            string joined = Path.GetFullPath(Path.Combine(Root, rel));
            string rootWithSep = Root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string joinedWithSep = joined.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (!joinedWithSep.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                throw new IcmError("path '" + rel + "' escapes the ICM dir");
            return joined;
        }

        // Convention path helpers (see Conventions): the schema/sample/flow file for a name.
        public string SchemaPath(string table) { return Resolve(Conventions.SchemaRel(table)); }
        public string SamplePath(string table) { return Resolve(Conventions.SampleRel(table)); }
        public string FlowPath(string name) { return Resolve(Conventions.FlowRel(name)); }

        public string ReadFile(string rel)
        {
            string p = Resolve(rel);
            try { return File.ReadAllText(p); }
            catch (Exception e) { throw new IcmError("reading " + p + ": " + e.Message); }
        }

        public void WriteFile(string rel, string contents)
        {
            string p = Resolve(rel);
            try
            {
                string parent = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                File.WriteAllText(p, contents);
            }
            catch (IcmError) { throw; }
            catch (Exception e) { throw new IcmError("writing " + p + ": " + e.Message); }
        }

        // Read a KB entry by manifest id (model-facing grounding text). The routing metadata block
        // is stripped so the model sees clean content.
        public string ReadEntry(string id)
        {
            if (Manifest == null) throw new IcmError("this ICM has no manifest.json");
            Entry e = Manifest.GetEntry(id);
            if (e == null) throw new IcmError("no manifest entry '" + id + "'");
            return Indexer.StripMeta(ReadFile(e.Path));
        }
    }
}
