// Tsv - one place for the tab-separated line handling the oracle, the dispatcher, and the propose
// flow all share: CRLF-tolerant splitting into non-empty lines (and into tab-split rows).

using System.Collections.Generic;

namespace Icm
{
    internal static class Tsv
    {
        // Non-empty lines, with trailing '\r' stripped (tolerate Windows CRLF).
        public static List<string> NonEmptyLines(string text)
        {
            var outl = new List<string>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Trim().Length != 0) outl.Add(line);
            }
            return outl;
        }

        // Non-empty lines, each split into cells on tabs.
        public static List<string[]> Rows(string text)
        {
            var outl = new List<string[]>();
            foreach (string line in NonEmptyLines(text)) outl.Add(line.Split('\t'));
            return outl;
        }
    }
}
