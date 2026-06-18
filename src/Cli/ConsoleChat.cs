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
            Console.WriteLine("  type a request, 'help', or 'quit'. (Ctrl-Z then Enter to exit)\n");

            // Status trace goes to stderr so stdout carries only the conversation.
            var d = new Dispatcher(icm, url, delegate(string s) { Console.Error.WriteLine("  - " + s); });

            while (true)
            {
                Console.Write("icm > ");
                string line = Console.In.ReadLine();
                if (line == null) break; // EOF / Ctrl-Z
                line = line.Trim();
                if (line.Length == 0) continue;
                // fast-path the obvious exits so a down model can't trap the operator
                if (line == "quit" || line == "exit" || line == ":q") break;

                TurnResult r = d.Turn(line);
                if (r.Intent == Conventions.Intent.Quit) break;
                if (r.IsError) Console.Error.WriteLine("\n" + r.Text + "\n");
                else Console.WriteLine("\n" + r.Text + "\n");
            }
            Console.WriteLine("bye");
        }
    }
}
