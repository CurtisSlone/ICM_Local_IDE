using System;
using System.Collections.Generic;
using System.IO;

namespace App.Drivers
{
    internal class FileKvStore : IKvStore
    {
        private Dictionary<string, string> _store;
        private readonly string _filePath;

        public FileKvStore()
        {
            _store = new Dictionary<string, string>();
            _filePath = "kv.txt";
            LoadFromFile();
        }

        public string Get(string key)
        {
            if (_store.ContainsKey(key))
            {
                return _store[key];
            }
            return null;
        }

        public void Set(string key, string value)
        {
            _store[key] = value;
            SaveToFile();
        }

        public bool Delete(string key)
        {
            bool result = _store.Remove(key);
            if (result)
            {
                SaveToFile();
            }
            return result;
        }

        public List<string> Keys()
        {
            return new List<string>(_store.Keys);
        }

        private void LoadFromFile()
        {
            _store.Clear();
            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(_filePath);
                foreach (string line in lines)
                {
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0 && equalsIndex < line.Length - 1)
                    {
                        string key = line.Substring(0, equalsIndex);
                        string value = line.Substring(equalsIndex + 1);
                        _store[key] = value;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors during load, keep empty store
            }
        }

        private void SaveToFile()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (var kvp in _store)
                {
                    lines.Add(kvp.Key + "=" + kvp.Value);
                }
                File.WriteAllLines(_filePath, lines);
            }
            catch (Exception)
            {
                // Ignore errors during save
            }
        }
    }
}