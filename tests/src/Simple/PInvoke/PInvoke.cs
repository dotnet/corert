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
        private static extern int CheckIncremental_Foo(Foo[] array, int sz);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        private static extern int Inc(ref int value);
 
        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        private static extern int VerifyByRefFoo(ref Foo value);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern bool GetNextChar(ref char c);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int VerifyAnsiString(string str);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyUnicodeString(string str);

        [DllImport("*", CharSet=CharSet.Ansi)]
        private static extern int VerifyAnsiStringArray([In, MarshalAs(UnmanagedType.LPArray)]string []str);

        [DllImport("*", CharSet=CharSet.Ansi)]
        private static extern void ToUpper([In, Out, MarshalAs(UnmanagedType.LPArray)]string[] str);

        [DllImport("*", CharSet = CharSet.Ansi)]
        private static extern bool VerifySizeParamIndex(
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] out byte[] arrByte, out byte arrSize);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyStringBuilder(StringBuilder sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SafeHandleTest(SafeMemoryHandle sh1, Int64 sh1Value);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        public static extern int SafeHandleOutTest(out SafeMemoryHandle sh1);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool LastErrorTest();

        delegate int Delegate_Int(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j);
        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool ReversePInvoke_Int(Delegate_Int del);
        
        delegate void Delegate_Unused();
        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern unsafe int* ReversePInvoke_Unused(Delegate_Unused del);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, EntryPoint = "StructTest")]
        static extern bool StructTest_Auto(AutoStruct ss);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool StructTest(SequentialStruct ss);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern void StructTest_ByRef(ref SequentialStruct ss);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern void StructTest_ByOut(out SequentialStruct ss);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool StructTest_Explicit(ExplicitStruct es);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool StructTest_Nested(NestedStruct ns);

        public static int Main(string[] args)
        {
            TestBlittableType();
            TestBoolean();
            TestUnichar();
            TestArrays();
            TestByRef();
            TestString();
            TestStringBuilder();
            TestLastError();
            TestSafeHandle();
            TestStringArray();
            TestSizeParamIndex();
#if !CODEGEN_CPP
            TestDelegate();
#endif            
            TestStruct();
            return 100;
        }

        public static void ThrowIfNotEquals<T>(T expected, T actual, string message)
        {
            if (!expected.Equals(actual))
            {
                message += "\nExpected: " + expected.ToString() + "\n";
                message += "Actual: " + actual.ToString() + "\n";
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

        private static void TestUnichar()
        {
            Console.WriteLine("Testing Unichar");
            char c = 'a';
            ThrowIfNotEquals(true, GetNextChar(ref c), "Unichar marshalling failed.");
            ThrowIfNotEquals('b', c, "Unichar marshalling failed.");
        }

        struct Foo
        {
            public int a;
            public float b;
        }

        private static void TestArrays()
        {
            Console.WriteLine("Testing marshalling int arrays");

            const int ArraySize = 100;
            int[] arr = new int[ArraySize];
            for (int i = 0; i < ArraySize; i++)
                arr[i] = i;

            ThrowIfNotEquals(0, CheckIncremental(arr, ArraySize), "Array marshalling failed");

            Console.WriteLine("Testing marshalling blittable struct arrays");

            Foo[] arr_foo = new Foo[ArraySize];
            for (int i = 0; i < ArraySize; i++)
            {
                arr_foo[i].a = i;
                arr_foo[i].b = i;
            }

            ThrowIfNotEquals(0, CheckIncremental_Foo(arr_foo, ArraySize), "Array marshalling failed");
        }

        private static void TestByRef()
        {
            Console.WriteLine("Testing marshalling by ref");

            int value = 100;
            ThrowIfNotEquals(0, Inc(ref value), "By ref marshalling failed");
            ThrowIfNotEquals(101, value, "By ref marshalling failed");

            Foo foo = new Foo();
            foo.a = 10;
            foo.b = 20;
            int ret = VerifyByRefFoo(ref foo);            
            ThrowIfNotEquals(0, ret, "By ref struct marshalling failed");

            ThrowIfNotEquals(foo.a, 11, "By ref struct unmarshalling failed");
            ThrowIfNotEquals(foo.b, 21.0f, "By ref struct unmarshalling failed");
        }

        private static void TestString()
        {
            Console.WriteLine("Testing marshalling string");
            ThrowIfNotEquals(1, VerifyAnsiString("Hello World"), "Ansi String marshalling failed.");
            ThrowIfNotEquals(1, VerifyUnicodeString("Hello World"), "Unicode String marshalling failed.");
        }

        private static void TestStringBuilder()
        {
            Console.WriteLine("Testing marshalling string builder");
            StringBuilder sb = new StringBuilder(16);
            VerifyStringBuilder(sb);
            bool result = (sb.ToString() == "Hello World");
            ThrowIfNotEquals(true, result, "Unicode String builder marshalling failed.");
        }


        private static void TestStringArray()
        {
            Console.WriteLine("Testing marshalling string array");
            string[] strArray = new string[] { "Hello", "World" };
            ThrowIfNotEquals(1, VerifyAnsiStringArray(strArray), "Ansi string array in marshalling failed.");
            ToUpper(strArray);

            ThrowIfNotEquals(true, "HELLO" ==  strArray[0] && "WORLD" == strArray[1], "Ansi string array  out marshalling failed.");
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

        private static void TestSizeParamIndex()
        {
            Console.WriteLine("Testing SizeParamIndex");
            byte byte_Array_Size;
            byte[] arrByte;

            VerifySizeParamIndex(out arrByte, out byte_Array_Size);
            ThrowIfNotEquals(10, byte_Array_Size, "out size failed.");
            bool pass = true;
            for (int i = 0; i < byte_Array_Size; i++)
            {
                if (arrByte[i] != i)
                {
                    pass = false;
                    break;
                }
            }
            ThrowIfNotEquals(true, pass, "SizeParamIndex failed.");
        }

        private static void TestDelegate()
        {
            Console.WriteLine("Testing Delegate");
            Delegate_Int del = new Delegate_Int(Sum);
            ThrowIfNotEquals(true, ReversePInvoke_Int(del), "Delegate marshalling failed.");
            unsafe
            {
                //
                // We haven't instantiated Delegate_Unused and nobody
                // allocates it. If a EEType is not constructed for Delegate_Unused
                // it will fail during linking.
                //
                ReversePInvoke_Unused(null);
            }
        }

        static int Sum(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j)
        {
            return a + b + c + d + e + f + g + h + i + j;
        }
        [StructLayout(LayoutKind.Auto)]
        public struct AutoStruct
        {
            public short f0;
            public int f1;
            public float f2;
            [MarshalAs(UnmanagedType.LPStr)]
            public String f3;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SequentialStruct
        {
            public short f0;
            public int f1;
            public float f2;
            [MarshalAs(UnmanagedType.LPStr)]
            public String f3;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct ExplicitStruct
        {
            [FieldOffset(0)]
            public int f1;

            [FieldOffset(12)]
            public float f2;

            [FieldOffset(24)]
            [MarshalAs(UnmanagedType.LPStr)]
            public String f3;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NestedStruct
        {
            public int f1;

            public ExplicitStruct f2;
        }

        private static void TestStruct()
        {
            Console.WriteLine("Testing Structs");
            SequentialStruct ss = new SequentialStruct();
            ss.f0 = 100;
            ss.f1 = 1;
            ss.f2 = 10.0f;
            ss.f3 = "Hello";

            ThrowIfNotEquals(true, StructTest(ss), "Struct marshalling scenario1 failed.");

            StructTest_ByRef(ref ss);
            ThrowIfNotEquals(true,  ss.f1 == 2 && ss.f2 == 11.0 && ss.f3.Equals("Ifmmp"), "Struct marshalling scenario2 failed.");

            SequentialStruct ss2 = new SequentialStruct();
            StructTest_ByOut(out ss2);
            ThrowIfNotEquals(true, ss2.f0 == 1 && ss2.f1 == 1.0 &&  ss2.f2 == 1.0 && ss2.f3.Equals("0123456"), "Struct marshalling scenario3 failed.");

            ExplicitStruct es = new ExplicitStruct();
            es.f1 = 100;
            es.f2 = 100.0f;
            es.f3 = "Hello";
            ThrowIfNotEquals(true, StructTest_Explicit(es), "Struct marshalling scenario4 failed.");

            NestedStruct ns = new NestedStruct();
            ns.f1 = 100;
            ns.f2 = es;
            ThrowIfNotEquals(true, StructTest_Nested(ns), "Struct marshalling scenario5 failed.");

// RhpThrowEx is not implemented in CPPCodeGen
#if !CODEGEN_CPP
            bool pass = false;
            AutoStruct autoStruct = new AutoStruct();
            try
            {
                // passing struct with Auto layout should throw exception.
                StructTest_Auto(autoStruct);
            }
            catch (Exception)
            {
                pass = true;
            }
            ThrowIfNotEquals(true, pass, "Struct marshalling scenario6 failed.");
#endif
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
