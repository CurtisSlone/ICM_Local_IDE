using System;

namespace App.Core
{
    internal class TodoItem
    {
        public int Id;
        public string Title;
        public bool Done;

        public TodoItem(int id, string title, bool done)
        {
            Id = id;
            Title = title;
            Done = done;
        }
    }
}