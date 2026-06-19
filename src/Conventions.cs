// Conventions.cs - the instance contract in one place: the file/dir layout an ICM uses and the
// string identifiers (intents, tool kinds, flow node kinds) the engine dispatches on. Centralizing
// these kills typo drift and documents, in a single file, exactly what an instance directory may
// contain and what the runtime understands.

namespace Icm
{
    internal static class Conventions
    {
        // Top-level files an instance may provide.
        public const string ConfigFile = "icm.config.json";
        public const string ManifestFile = "manifest.json";
        public const string SystemFile = "SYSTEM.md";
        public const string NotesFile = "NOTES.md";   // persistent session memory the chat reads/appends

        // Sub-directories (relative to the instance root).
        public const string SchemasDir = "schemas";
        public const string SamplesDir = "samples";
        public const string FlowsDir = "flows";
        public const string KbDir = "kb";
        public const string ToolsDir = "tools";
        public const string RefdocsDir = "refdocs";

        // Relative-path builders for the table/flow/refdocs conventions.
        public static string SchemaRel(string table) { return SchemasDir + "/" + table + ".json"; }
        public static string SampleRel(string table) { return SamplesDir + "/" + table + ".txt"; }
        public static string FlowRel(string name) { return FlowsDir + "/" + name + ".json"; }
        public static string RefdocRel(string corpus) { return RefdocsDir + "/" + corpus + ".json"; }

        // A routable reference file leads with a metadata block in an HTML comment (invisible in
        // rendered markdown, parseable): <!--icm { "id","title","doc_type","summary","keywords",
        // "source" } -->. `icm reindex` reads these to (re)generate manifest.json mechanically.
        public const string MetaOpen = "<!--icm";
        public const string MetaClose = "-->";

        // Folders scanned for routable reference files (markdown with an icm metadata block).
        public static readonly string[] RoutableDirs = { "reference", "patterns", "recipes", "scaffold", "snippets", "kb" };

        // Dispatcher intents (the constrained classify enum).
        internal static class Intent
        {
            public const string Ask = "ask";
            public const string Make = "make";
            public const string Validate = "validate";
            public const string Propose = "propose";
            public const string Help = "help";
            public const string Quit = "quit";
            public static readonly string[] All = { Ask, Make, Validate, Propose, Help, Quit };
        }

        // Tool kinds the host knows how to dispatch (a command/script tool uses any other kind that
        // declares a `command`/`script`).
        internal static class ToolKind
        {
            public const string Validate = "validate";
            public const string KbAnswer = "kb_answer";
            public const string Propose = "propose";
            public const string GenerateVerify = "generate_verify";
            public const string Flow = "flow";
            public const string Command = "command";
            public const string Script = "script";
        }

        // Flow node kinds.
        internal static class Node
        {
            public const string Route = "route";
            public const string Read = "read";
            public const string Generate = "generate";
            public const string Answer = "answer";
            public const string Propose = "propose";
            public const string Validate = "validate";
            public const string Tool = "tool";
            public const string Loop = "loop";   // repeat a body of nodes until a state key is truthy, or N times
            public const string Branch = "branch"; // run `then` or `else` body based on a state-key test
            public const string Search = "search"; // hybrid docs search over a refdocs corpus -> context
            public const string RouteMany = "route_many"; // constrained multi-pick of relevant manifest entry ids
            public const string Catalog = "catalog";      // write the (optionally filtered) manifest index to a state key
        }
    }
}
