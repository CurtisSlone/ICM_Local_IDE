namespace App.Core
{
    // Pure domain logic: no console, no files, no UI. Trivially testable.
    public sealed class Greeter
    {
        public string Greet(string name)
        {
            if (string.IsNullOrEmpty(name)) { name = "world"; }
            return "hello, " + name;
        }
    }
}