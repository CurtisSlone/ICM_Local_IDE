using System;
using System.Collections.Generic;
using System.IO;
using App.Core;
using App.Drivers;

namespace App
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: app add <title...> | list | done <id> | remove <id>");
                return 2;
            }

            using (var store = new FileTodoStore())
            {
                string command = args[0];
                if (command == "add")
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("usage: app add <title...>");
                        return 2;
                    }
                    string title = string.Join(" ", args, 1, args.Length - 1);
                    store.Add(title);
                }
                else if (command == "list")
                {
                    List<TodoItem> items = store.List();
                    foreach (TodoItem item in items)
                    {
                        string done = item.Done ? "x" : " ";
                        Console.WriteLine("[" + done + "] " + item.Id + " " + item.Title);
                    }
                }
                else if (command == "done")
                {
                    if (args.Length != 2)
                    {
                        Console.Error.WriteLine("usage: app done <id>");
                        return 2;
                    }
                    int id;
                    if (!int.TryParse(args[1], out id))
                    {
                        Console.Error.WriteLine("usage: app done <id>");
                        return 2;
                    }
                    if (!store.Complete(id))
                    {
                        Console.Error.WriteLine("No item with id " + id);
                        return 2;
                    }
                }
                else if (command == "remove")
                {
                    if (args.Length != 2)
                    {
                        Console.Error.WriteLine("usage: app remove <id>");
                        return 2;
                    }
                    int id;
                    if (!int.TryParse(args[1], out id))
                    {
                        Console.Error.WriteLine("usage: app remove <id>");
                        return 2;
                    }
                    if (!store.Remove(id))
                    {
                        Console.Error.WriteLine("No item with id " + id);
                        return 2;
                    }
                }
                else
                {
                    Console.Error.WriteLine("usage: app add <title...> | list | done <id> | remove <id>");
                    return 2;
                }
            }
            return 0;
        }
    }
}