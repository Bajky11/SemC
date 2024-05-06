using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemC
{
    internal class Generator
    {
        private int Counter = 0;
        public void Reset()
        {
            Counter = 0;
        }

        public int Next()
        {
            return Counter++;
        }

        public int Count()
        {
            return Counter;
        }
    }
}
