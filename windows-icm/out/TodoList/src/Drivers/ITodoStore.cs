using System;
using System.Collections.Generic;

namespace App.Drivers
{
    internal interface ITodoStore
    {
        void Add(string title);
        List<App.Core.TodoItem> List();
        bool Complete(int id);
        bool Remove(int id);
    }
}