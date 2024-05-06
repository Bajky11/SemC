using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemC
{
    internal class Buffer
    {
        private List<int> data = new List<int>();
        private int capacity;
        public string name;
        private bool debug = false;

        public Buffer(int capacity, string name)
        {
            this.capacity = capacity;
            this.name = name;
        }

        public void Add(int item)
        {
            data.Add(item);
            if (debug) Console.WriteLine($"{name} capacity ({data.Count})");
            if (data.Count == capacity)
            {
                if (debug) Console.WriteLine($"{name} is full");
                if (false) foreach (int one in data) Console.Write(one);
            }
        }

        public bool IsFull()
        {
            return data.Count == capacity;
        }

        public bool IsEmpty()
        {
            return data.Count == 0;
        }

        public void Clear()
        {
            data.Clear();
        }

        public int Count()
        {
            return data.Count;
        }

        public IEnumerable<int> Items => data;
    }

}
