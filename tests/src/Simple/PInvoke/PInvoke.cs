// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

// Name of namespace matches the name of the assembly on purpose to
// ensure that we can handle this (mostly an issue for C++ code generation).
namespace PInvoke
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
        public static extern bool SafeHandleTest(SafeFileHandle sh1, Int32 sh1Value);

        public static int Main(string[] args)
        {
            TestBlittableType();
            TestBoolean();
            TestArrays();
            TestByRef();
            TestString();
            TestSafeHandle();
            
            return 100;
        }

        public static void ThrowIfNotEquals<T>(T expected, T actual, string message)
        {
            if (!Object.Equals(expected, actual))
            {
                message += "\nExpected: " + expected + "\n";
                message += "Actual: " + actual + "\n";
                throw new Exception(message);
            }
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

        private static void TestSafeHandle()
        {
            Console.WriteLine("Testing marshalling SafeHandle");
            String lpFileName = "A.txt";
            uint dwDesiredAccess = 0x40000000; // GENERIC_WRITE
            uint dwShareMode = 0x00000002; // FILE_SHARE_WRITE
            IntPtr lpSecurityAttributes = IntPtr.Zero;
            uint dwCreationDisposition = 2;// CREATE_ALWAYS;
            uint dwFlagsAndAttributes = 0x04000000; //FILE_FLAG_DELETE_ON_CLOSE
            IntPtr hTemplateFile = IntPtr.Zero;

            //create the handle
            SafeFileHandle hnd = SafeFileHandle.CreateFile(lpFileName, dwDesiredAccess, dwShareMode,
                lpSecurityAttributes, dwCreationDisposition,
                dwFlagsAndAttributes, hTemplateFile);

            IntPtr hndIntPtr = hnd.DangerousGetHandle(); //get the IntPtr associated with hnd
            int intVal =  hndIntPtr.ToInt32(); //return the 32-bit value associated with hnd

            ThrowIfNotEquals(true, SafeHandleTest(hnd, intVal), "Ansi String marshalling failed.");
        }
    }

    public class SafeFileHandle : SafeHandle //SafeHandle subclass
    {

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);


        //each SafeHandle subclass will expose a static method for instance creation
        [DllImport("kernel32", EntryPoint = "CreateFileW", SetLastError = true, CharSet =CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(String lpFileName,
                                                uint dwDesiredAccess, uint dwShareMode,
                                                IntPtr lpSecurityAttributes, uint dwCreationDisposition,
                                                uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        //default constructor which just calls the base class constructor
        public SafeFileHandle()
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
            return CloseHandle(handle);
        }
    } //end of SafeFileHandle class

}
