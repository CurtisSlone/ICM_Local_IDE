using System;
using System.Collections.Generic;
using App.Drivers;

namespace App
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: app <backend> <operation> [args...]");
                Console.Error.WriteLine("  backend: 'mem' or 'file'");
                Console.Error.WriteLine("  operation: 'set <key> <value>', 'get <key>', 'rm <key>', 'keys'");
                return 2;
            }

            string backend = args[0];
            string operation = args[1];

            IKvStore store;
            if (backend == "mem")
            {
                store = new InMemoryKvStore();
            }
            else if (backend == "file")
            {
                store = new FileKvStore();
            }
            else
            {
                Console.Error.WriteLine("usage: app <backend> <operation> [args...]");
                Console.Error.WriteLine("  backend: 'mem' or 'file'");
                Console.Error.WriteLine("  operation: 'set <key> <value>', 'get <key>', 'rm <key>', 'keys'");
                return 2;
            }

            if (operation == "set" && args.Length >= 4)
            {
                string key = args[2];
                string value = args[3];
                store.Set(key, value);
            }
            else if (operation == "get" && args.Length >= 3)
            {
                string key = args[2];
                string result = store.Get(key);
                if (result == null)
                {
                    Console.WriteLine("(nil)");
                }
                else
                {
                    Console.WriteLine(result);
                }
            }
            else if (operation == "rm" && args.Length >= 3)
            {
                string key = args[2];
                bool removed = store.Delete(key);
                if (removed)
                {
                    Console.WriteLine("removed");
                }
                else
                {
                    Console.WriteLine("not found");
                }
            }
            else if (operation == "keys" && args.Length == 2)
            {
                List<string> keys = store.Keys();
                foreach (string key in keys)
                {
                    Console.WriteLine(key);
                }
            }
            else
            {
                Console.Error.WriteLine("usage: app <backend> <operation> [args...]");
                Console.Error.WriteLine("  backend: 'mem' or 'file'");
                Console.Error.WriteLine("  operation: 'set <key> <value>', 'get <key>', 'rm <key>', 'keys'");
                return 2;
            }

            return 0;
        }
    }
}