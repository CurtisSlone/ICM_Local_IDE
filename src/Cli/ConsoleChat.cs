// ConsoleChat - the console operator console: a thin REPL over a Dispatcher. The conversation,
// routing, and oracle logic all live in the Dispatcher; this is just stdin/stdout plumbing.

using System;

namespace Icm
{
    internal static class ConsoleChat
    {
        public static void Run(Instance icm, string url)
        {
            Console.WriteLine("ICM operator console - '" + icm.Config.Name + "'");
            Console.WriteLine("  dispatch seat: " + icm.Config.DispatchModel() +
                              "   generate seat: " + icm.Config.Models.Generate + "   ollama: " + url);
            Console.WriteLine("  just type what you want - it's matched to a workflow (you confirm before it runs),");
            Console.WriteLine("  or use slash commands directly. '/help' lists them, '/flows' shows workflows, 'quit' exits.\n");

            // Status trace goes to stderr so stdout carries only the conversation.
            var d = new Dispatcher(icm, url, delegate(string s) { Console.Error.WriteLine("  - " + s); });

            // Stream freeform generation token-by-token. A blank line is printed before the first token
            // so the answer is not jammed against the prompt; the turn's r.Streamed tells us not to
            // re-print the text afterward.
            bool firstToken = false;
            d.OnToken = delegate(string t)
            {
                if (firstToken) { Console.WriteLine(); firstToken = false; }
                Console.Write(t);
            };

            while (true)
            {
                Console.Write("icm > ");
                string line = Console.In.ReadLine();
                if (line == null) break; // EOF / Ctrl-Z
                line = line.Trim();
                if (line.Length == 0) continue;
                // fast-path the obvious exits so a down model can't trap the operator
                if (line == "quit" || line == "exit" || line == ":q") break;

                firstToken = true;
                long pTok = TokenMeter.Prompt, eTok = TokenMeter.Eval; int pCalls = TokenMeter.Calls;
                TurnResult r = d.Turn(line);
                if (r.Intent == Conventions.Intent.Quit) break;
                if (r.Intent == "clear") { Console.Clear(); continue; }
                if (r.Streamed) { Console.WriteLine(); Console.WriteLine(); }   // already on screen; end the line + space
                else if (r.IsError) Console.Error.WriteLine("\n" + r.Text + "\n");
                else Console.WriteLine("\n" + r.Text + "\n");

                // Show how much the LOCAL model did this turn (work kept off the frontier bill).
                int dCalls = TokenMeter.Calls - pCalls;
                if (dCalls > 0)
                    Console.WriteLine("  [local model: " + (TokenMeter.Eval - eTok) + " generated + " +
                        (TokenMeter.Prompt - pTok) + " prompt tok, " + dCalls + " call(s)]\n");
            }
            if (TokenMeter.Calls > 0)
                Console.WriteLine("session local tokens: " + TokenMeter.Eval + " generated + " + TokenMeter.Prompt +
                    " prompt = " + TokenMeter.Total + " across " + TokenMeter.Calls + " call(s)");
            Console.WriteLine("bye");
        }
    }
}
