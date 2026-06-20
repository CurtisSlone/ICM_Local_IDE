using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace App.Drivers
{
    internal class FileTodoStore : ITodoStore, IDisposable
    {
        private readonly string _filename = "todos.txt";
        private List<App.Core.TodoItem> _items;
        private bool _disposed;

        public FileTodoStore()
        {
            _items = new List<App.Core.TodoItem>();
            Load();
        }

        public void Add(string title)
        {
            int id = 1;
            if (_items.Count > 0)
            {
                id = _items.Max(i => i.Id) + 1;
            }
            _items.Add(new App.Core.TodoItem(id, title, false));
            Save();
        }

        public List<App.Core.TodoItem> List()
        {
            return _items;
        }

        public bool Complete(int id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                item.Done = true;
                Save();
                return true;
            }
            return false;
        }

        public bool Remove(int id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _items.Remove(item);
                Save();
                return true;
            }
            return false;
        }

        private void Load()
        {
            _items.Clear();
            if (!File.Exists(_filename))
                return;

            try
            {
                string[] lines = File.ReadAllLines(_filename);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] parts = line.Split('|');
                    if (parts.Length != 3)
                        continue;

                    int id;
                    bool done;
                    if (!int.TryParse(parts[0], out id))
                        continue;

                    if (!bool.TryParse(parts[2], out done))
                        continue;

                    string title = parts[1];
                    _items.Add(new App.Core.TodoItem(id, title, done));
                }
            }
            catch (Exception)
            {
                // Ignore load errors, start fresh
            }
        }

        private void Save()
        {
            try
            {
                using (var writer = new StreamWriter(_filename))
                {
                    foreach (var item in _items)
                    {
                        writer.WriteLine(item.Id + "|" + item.Title + "|" + item.Done);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore save errors
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}