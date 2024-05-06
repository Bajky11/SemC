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

        public Buffer(int capacity, string name)
        {
            this.capacity = capacity;
            this.name = name;
        }

        public void Add(int item)
        {
            data.Add(item);
            Console.WriteLine($"{name} capacity ({data.Count})");
            if (data.Count == capacity)
            {
                Console.WriteLine($"{name} is full");
            }
        }

        public bool IsFull()
        {
            return data.Count == capacity;
        }

        public void Clear()
        {
            data.Clear();
        }

        public IEnumerable<int> Items => data;
    }

}
