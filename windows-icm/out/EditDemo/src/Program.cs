using System;

namespace App
{
    internal static class Program
    {
        // The entry point. Add pure logic under src\Core and I/O adapters under src\Drivers, then
        // wire them in here. Returning int lets the process report an exit code (0 = success).
        private static int Main(string[] args)
        {
            Console.WriteLine("App ready. Add features under src\\Core and src\\Drivers.");
            return 0;
        }
    }
}