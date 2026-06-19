// Results - the small structured outcome types passed between the runtime and the front ends.
// Keeping these as data (no logic) lets callers branch on `Ok` instead of sniffing PASS/FAIL text.

using System.Collections.Generic;
using System.Text;

namespace Icm
{
    // The outcome of one dispatcher turn. Front ends render this however they like.
    internal class TurnResult
    {
        public string Intent = "";      // ask | make | validate | propose | help | quit | (unknown)
        public string Query = "";       // the core query the dispatcher extracted
        public string Standalone = "";  // the utterance after coreference rewrite (== input if none)
        public bool Rewritten = false;  // true when Standalone differs from the raw input
        public string Text = "";        // the capability's output to show the user
        public bool IsError = false;    // true when Text is an error message
        public string ProposedTable;    // set on a successful `propose`: the target table
        public string ProposedRow;      // set on a successful `propose`: the validated tab-joined row
        public string WrittenPath;      // set when output was redirected to a file ("> path"); absolute path
        public bool Streamed = false;   // true when Text was already streamed to the front end via a token sink
    }

    // The outcome of an oracle run on a table.
    internal class ValidateResult
    {
        public bool Ok;
        public string Table = "";
        public List<Problem> Problems = new List<Problem>();

        // Human/agent-facing rendering (capped). Callers branch on Ok, not on this text.
        public string ToText(int maxShown)
        {
            if (Ok) return "PASS - '" + Table + "' is valid under its schema.";
            var sb = new StringBuilder();
            sb.Append("FAIL - " + Problems.Count + " problem(s) in '" + Table + "':\n");
            int shown = 0;
            foreach (Problem p in Problems)
            {
                if (shown++ >= maxShown) break;
                sb.Append("  " + p.ToString() + "\n");
            }
            return sb.ToString();
        }
    }

    // The outcome of a propose-row run: a model proposal gated by the oracle, with bounded repair.
    internal class ProposeResult
    {
        public bool Ok;
        public string Table = "";
        public string Header = "";              // tab-joined header used for validation
        public string Row = "";                 // the best/last tab-joined row produced
        public int Attempts;
        public string Error;                    // non-null on a hard failure (load/model error)
        public List<Problem> Problems = new List<Problem>(); // last verdict's problems when !Ok
    }

    // The outcome of running an instance command/script tool.
    internal class ToolRunResult
    {
        public bool Ok;
        public int ExitCode;
        public bool TimedOut;
        public string Stdout = "";
        public string Stderr = "";
        public string Output = "";   // combined, human/agent-facing
        public string Error;         // non-null on a host-side failure (couldn't launch, etc.)
    }
}
