using System;

namespace App
{
    internal static class Program
    {
        // The entry point. Add pure logic under src\Core and I/O adapters under src\Drivers, then
        // wire them in here. Returning int lets the process report an exit code (0 = success).
        private static int Main(string[] args)
        {
            Tuple<string, int> parsed = ParseArgs(args);
            string name = parsed.Item1;
            int count = parsed.Item2;
            
            for (int i = 1; i <= count; i++)
            {
                Console.WriteLine("Hello, " + name + "! (line " + i + ")");
            }
            
            return 0;
        }
        
        private static Tuple<string, int> ParseArgs(string[] args)
        {
            string name = "world";
            int count = 3;
            
            if (args.Length >= 1)
            {
                name = args[0];
            }
            
            if (args.Length >= 2)
            {
                int parsedCount;
                if (int.TryParse(args[1], out parsedCount))
                {
                    count = parsedCount;
                }
            }
            
            return new Tuple<string, int>(name, count);
        }
    }
}