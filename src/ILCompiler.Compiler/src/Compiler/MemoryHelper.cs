using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler
{
    static unsafe class MemoryHelper
    {
        public static void FillMemory(byte* dest, byte fill, int count)
        {
            for (; count > 0; count--)
            {
                *dest = fill;
                dest++;
            }
        }
    }
}
