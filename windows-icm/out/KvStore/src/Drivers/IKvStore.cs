using System;
using System.Collections.Generic;

namespace App.Drivers
{
    internal interface IKvStore
    {
        string Get(string key);
        void Set(string key, string value);
        bool Delete(string key);
        List<string> Keys();
    }
}