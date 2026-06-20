using System;
using System.Collections.Generic;

namespace App.Drivers
{
    internal class InMemoryKvStore : IKvStore
    {
        private Dictionary<string, string> _store;

        public InMemoryKvStore()
        {
            _store = new Dictionary<string, string>();
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
        }

        public bool Delete(string key)
        {
            return _store.Remove(key);
        }

        public List<string> Keys()
        {
            return new List<string>(_store.Keys);
        }
    }
}