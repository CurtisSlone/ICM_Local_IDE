using System;

namespace App.Drivers
{
    // One IOutput implementation. To add another (file, network), copy this file as a template.
    public sealed class ConsoleOutput : IOutput
    {
        public void Write(string line) { Console.WriteLine(line); }
    }
}