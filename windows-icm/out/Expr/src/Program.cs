using System;
using System.Globalization;
using App.Core;

namespace App
{
    internal static class Program
    {
        // The entry point. Add pure logic under src\Core and I/O adapters under src\Drivers, then
        // wire them in here. Returning int lets the process report an exit code (0 = success).
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: Expr <expression>");
                return 2;
            }

            string expression = string.Join(" ", args);
            
            try
            {
                double result = Evaluator.Evaluate(expression);
                Console.WriteLine(result.ToString(CultureInfo.InvariantCulture));
                return 0;
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return 1;
            }
        }
    }
}