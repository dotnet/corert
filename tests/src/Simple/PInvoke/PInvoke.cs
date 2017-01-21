// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

// Name of namespace matches the name of the assembly on purpose to
// ensure that we can handle this (mostly an issue for C++ code generation).
namespace PInvokeTests
{
    internal class Program
    {
        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        private static extern int Square(int intValue);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        private static extern int IsTrue(bool boolValue);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        private static extern int CheckIncremental(int[] array, int sz);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        private static extern int Inc(ref int value);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int VerifyAnsiString(string str);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyUnicodeString(string str);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyStringBuilder(StringBuilder sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SafeHandleTest(SafeMemoryHandle sh1, Int64 sh1Value);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        public static extern int SafeHandleOutTest(out SafeMemoryHandle sh1);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool LastErrorTest();

        public static int Main(string[] args)
        {
            TestBlittableType();
            TestBoolean();
            TestArrays();
            TestByRef();
            TestString();
            TestLastError();
            TestSafeHandle();
            return 100;
        }

        public static void ThrowIfNotEquals(int expected, int actual, string message)
        {
            if (expected != actual)
            {
                message += "\nExpected: " + expected + "\n";
                message += "Actual: " + actual + "\n";
                throw new Exception(message);
            }
        }

        public static void ThrowIfNotEquals(bool expected, bool actual, string message)
        {
           ThrowIfNotEquals(expected ? 1 : 0, actual ? 1 : 0, message);
        }

        private static void TestBlittableType()
        {
           Console.WriteLine("Testing marshalling blittable types");
           ThrowIfNotEquals(100, Square(10),  "Int marshalling failed");
        }

        private static void TestBoolean()
        {
           Console.WriteLine("Testing marshalling boolean");
           ThrowIfNotEquals(1, IsTrue(true), "Bool marshalling failed");
           ThrowIfNotEquals(0, IsTrue(false), "Bool marshalling failed");
        }

        private static void TestArrays()
        {
            Console.WriteLine("Testing marshalling arrays");
            const int ArraySize = 100;
            int[] arr = new int[ArraySize];
            for (int i = 0; i < ArraySize; i++)
                arr[i] = i;

           ThrowIfNotEquals(0, CheckIncremental(arr, ArraySize), "Array marshalling failed");
        }

        private static void TestByRef()
        {
            Console.WriteLine("Testing marshalling by ref");
            int value = 100;
            ThrowIfNotEquals(0, Inc(ref value), "By ref marshalling failed");
            ThrowIfNotEquals(101, value, "By ref marshalling failed");
        }

        private static void TestString()
        {
            Console.WriteLine("Testing marshalling string");
            ThrowIfNotEquals(1, VerifyAnsiString("Hello World"), "Ansi String marshalling failed.");
            ThrowIfNotEquals(1, VerifyUnicodeString("Hello World"), "Unicode String marshalling failed.");
        }

        private static void TestLastError()
        {
            Console.WriteLine("Testing last error");
            ThrowIfNotEquals(true, LastErrorTest(), "GetLastWin32Error is not zero");
            ThrowIfNotEquals(12345, Marshal.GetLastWin32Error(), "Last Error test failed");
        }

        private static void TestSafeHandle()
        {
            Console.WriteLine("Testing marshalling SafeHandle");

            SafeMemoryHandle hnd = SafeMemoryHandle.AllocateMemory(1000);

            IntPtr hndIntPtr = hnd.DangerousGetHandle(); //get the IntPtr associated with hnd
            long val =  hndIntPtr.ToInt64(); //return the 64-bit value associated with hnd

            ThrowIfNotEquals(true, SafeHandleTest(hnd, val), "SafeHandle marshalling failed.");

            Console.WriteLine("Testing marshalling out SafeHandle");
            SafeMemoryHandle hnd2;
            int actual = SafeHandleOutTest(out hnd2);
            int expected = unchecked((int)hnd2.DangerousGetHandle().ToInt64());
            ThrowIfNotEquals(actual, expected, "SafeHandle out marshalling failed");
        }
    }

    public class SafeMemoryHandle : SafeHandle //SafeHandle subclass
    {
        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        public static extern SafeMemoryHandle AllocateMemory(int size);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        public static extern bool ReleaseMemory(IntPtr handle);

        public SafeMemoryHandle()
            : base(IntPtr.Zero, true)
        {
        }

        private static readonly IntPtr _invalidHandleValue = new IntPtr(-1);

        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero || handle == _invalidHandleValue; }
        }

        override protected bool ReleaseHandle()
        {
            return ReleaseMemory(handle);
        }
    } //end of SafeMemoryHandle class

}
