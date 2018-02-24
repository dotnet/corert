using System;
using System.Runtime.InteropServices;

namespace StaticLibrary
{
    public class ClassLibrary
    {
        [NativeCallable(EntryPoint = "Add", CallingConvention = CallingConvention.StdCall)]
        public static int Add(int a, int b)
        {
            return a + b;
        }

        [NativeCallable(EntryPoint = "Subtract", CallingConvention = CallingConvention.StdCall)]
        public static int Subtract(int a, int b)
        {
            return a - b;
        }

        [NativeCallable(EntryPoint = "Not", CallingConvention = CallingConvention.StdCall)]
        public static bool Not(bool b)
        {
            return !b;
        }
    }
}
