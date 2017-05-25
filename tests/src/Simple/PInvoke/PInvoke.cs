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

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int VerifyAnsiStringOut(out string str);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int VerifyAnsiStringRef(ref string str);

        [DllImport("*", EntryPoint = "VerifyAnsiStringRef", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int VerifyAnsiStringInRef([In]ref string str);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyUnicodeString(string str);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyUnicodeStringOut(out string str);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyUnicodeStringRef(ref string str);

        [DllImport("*", EntryPoint = "VerifyUnicodeStringRef", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyUnicodeStringInRef([In]ref string str);

        [DllImport("*", CharSet = CharSet.Ansi)]
        private static extern int VerifyAnsiStringArray([In, MarshalAs(UnmanagedType.LPArray)]string[] str);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern bool VerifyAnsiCharArrayIn(char[] a);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern bool VerifyAnsiCharArrayOut([Out]char[] a);

        [DllImport("*", CharSet = CharSet.Ansi)]
        private static extern void ToUpper([In, Out, MarshalAs(UnmanagedType.LPArray)]string[] str);

        [DllImport("*", CharSet = CharSet.Ansi)]
        private static extern bool VerifySizeParamIndex(
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] out byte[] arrByte, out byte arrSize);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "VerifyUnicodeStringBuilder")]
        private static extern int VerifyUnicodeStringBuilder(StringBuilder sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "VerifyUnicodeStringBuilder")]
        private static extern int VerifyUnicodeStringBuilderIn([In]StringBuilder sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int VerifyUnicodeStringBuilderOut([Out]StringBuilder sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "VerifyAnsiStringBuilder")]
        private static extern int VerifyAnsiStringBuilder(StringBuilder sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "VerifyAnsiStringBuilder")]
        private static extern int VerifyAnsiStringBuilderIn([In]StringBuilder sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int VerifyAnsiStringBuilderOut([Out]StringBuilder sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SafeHandleTest(SafeMemoryHandle sh1, Int64 sh1Value);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        public static extern int SafeHandleOutTest(out SafeMemoryHandle sh1);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool LastErrorTest();

        delegate int Delegate_Int(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j);
        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool ReversePInvoke_Int(Delegate_Int del);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        delegate bool Delegate_String(string s);
        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool ReversePInvoke_String(Delegate_String del);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern Delegate_String GetDelegate();

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool Callback(ref Delegate_String d);

        delegate void Delegate_Unused();
        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern unsafe int* ReversePInvoke_Unused(Delegate_Unused del);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, EntryPoint = "StructTest")]
        static extern bool StructTest_Auto(AutoStruct ss);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool StructTest_Sequential2(NesterOfSequentialStruct.SequentialStruct ss);

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

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool StructTest_Array(SequentialStruct []ns, int length);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern bool IsNULL(char[] a);

        [DllImport("*", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern bool IsNULL(String sb);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool IsNULL(Foo[] foo);

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool IsNULL(SequentialStruct[] foo);

        [StructLayout(LayoutKind.Sequential, CharSet= CharSet.Ansi, Pack = 4)]
        public unsafe struct InlineArrayStruct
        {
            public int f0;
            public int f1;
            public int f2;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public short[] inlineArray;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
            public string inlineString;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
        public unsafe struct InlineUnicodeStruct
        {
            public int f0;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
            public string inlineString;
        }

        [DllImport("*", CallingConvention = CallingConvention.StdCall)]
        static extern bool InlineArrayTest(ref InlineArrayStruct ias, ref InlineUnicodeStruct ius);

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
            ThrowIfNotEquals(100, Square(10), "Int marshalling failed");
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

            Foo[] arr_foo = null;
            ThrowIfNotEquals(true, IsNULL(arr_foo), "Blittable array null check failed");
            
            arr_foo = new Foo[ArraySize];
            for (int i = 0; i < ArraySize; i++)
            {
                arr_foo[i].a = i;
                arr_foo[i].b = i;
            }

            ThrowIfNotEquals(0, CheckIncremental_Foo(arr_foo, ArraySize), "Array marshalling failed");

            char[] a = "Hello World".ToCharArray();
            ThrowIfNotEquals(true, VerifyAnsiCharArrayIn(a), "Ansi Char Array In failed");

            char[] b = new char[12];
            ThrowIfNotEquals(true, VerifyAnsiCharArrayOut(b), "Ansi Char Array Out failed");
            ThrowIfNotEquals("Hello World!", new String(b), "Ansi Char Array Out failed2");

            char[] c = null;
            ThrowIfNotEquals(true, IsNULL(c), "AnsiChar Array null check failed");
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
            string s;
            ThrowIfNotEquals(1, VerifyAnsiStringOut(out s), "Out Ansi String marshalling failed");
            ThrowIfNotEquals("Hello World", s, "Out Ansi String marshalling failed");

            VerifyAnsiStringInRef(ref s);
            ThrowIfNotEquals("Hello World", s, "In Ref ansi String marshalling failed");

            VerifyAnsiStringRef(ref s);
            ThrowIfNotEquals("Hello World!", s, "Ref ansi String marshalling failed");

            ThrowIfNotEquals(1, VerifyUnicodeStringOut(out s), "Out Unicode String marshalling failed");
            ThrowIfNotEquals("Hello World", s, "Out Unicode String marshalling failed");

            VerifyUnicodeStringInRef(ref s);
            ThrowIfNotEquals("Hello World", s, "In Ref Unicode String marshalling failed");

            VerifyUnicodeStringRef(ref s);
            ThrowIfNotEquals("Hello World!", s, "Ref Unicode String marshalling failed");

            string ss = null;
            ThrowIfNotEquals(true, IsNULL(ss), "Ansi String null check failed");
        
        }

        private static void TestStringBuilder()
        {
            Console.WriteLine("Testing marshalling string builder");
            StringBuilder sb = new StringBuilder("Hello World");
            ThrowIfNotEquals(1, VerifyUnicodeStringBuilder(sb), "Unicode StringBuilder marshalling failed");
            ThrowIfNotEquals("HELLO WORLD", sb.ToString(), "Unicode StringBuilder marshalling failed.");

            StringBuilder sb1 = null;
            // for null stringbuilder it should return -1
            ThrowIfNotEquals(-1, VerifyUnicodeStringBuilder(sb1), "Null unicode StringBuilder marshalling failed");

            StringBuilder sb2 = new StringBuilder("Hello World");
            ThrowIfNotEquals(1, VerifyUnicodeStringBuilderIn(sb2), "In unicode StringBuilder marshalling failed");
            // Only [In] should change stringbuilder value
            ThrowIfNotEquals("Hello World", sb2.ToString(), "In unicode StringBuilder marshalling failed");

            StringBuilder sb3 = new StringBuilder();
            ThrowIfNotEquals(1, VerifyUnicodeStringBuilderOut(sb3), "Out Unicode string marshalling failed");
            ThrowIfNotEquals("Hello World", sb3.ToString(), "Out Unicode StringBuilder marshalling failed");

            StringBuilder sb4 = new StringBuilder("Hello World");
            ThrowIfNotEquals(1, VerifyAnsiStringBuilder(sb4), "Ansi StringBuilder marshalling failed");
            ThrowIfNotEquals("HELLO WORLD", sb4.ToString(), "Ansi StringBuilder marshalling failed.");

            StringBuilder sb5 = null;
            // for null stringbuilder it should return -1
            ThrowIfNotEquals(-1, VerifyAnsiStringBuilder(sb5), "Null Ansi StringBuilder marshalling failed");

            StringBuilder sb6 = new StringBuilder("Hello World");
            ThrowIfNotEquals(1, VerifyAnsiStringBuilderIn(sb6), "In unicode StringBuilder marshalling failed");
            // Only [In] should change stringbuilder value
            ThrowIfNotEquals("Hello World", sb6.ToString(), "In unicode StringBuilder marshalling failed");

            StringBuilder sb7 = new StringBuilder();
            ThrowIfNotEquals(1, VerifyAnsiStringBuilderOut(sb7), "Out Ansi string marshalling failed");
            ThrowIfNotEquals("Hello World!", sb7.ToString(), "Out Ansi StringBuilder marshalling failed");
        }


        private static void TestStringArray()
        {
            Console.WriteLine("Testing marshalling string array");
            string[] strArray = new string[] { "Hello", "World" };
            ThrowIfNotEquals(1, VerifyAnsiStringArray(strArray), "Ansi string array in marshalling failed.");
            ToUpper(strArray);

            ThrowIfNotEquals(true, "HELLO" == strArray[0] && "WORLD" == strArray[1], "Ansi string array  out marshalling failed.");
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
            long val = hndIntPtr.ToInt64(); //return the 64-bit value associated with hnd

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

        private class ClosedDelegateCLass
        {
            public int Sum(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j)
            {
                return a + b + c + d + e + f + g + h + i + j;
            }

            public bool GetString(String s)
            {
                return s == "Hello World";
            }
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

            Delegate_Int closed = new Delegate_Int((new ClosedDelegateCLass()).Sum);
            ThrowIfNotEquals(true, ReversePInvoke_Int(closed), "Closed Delegate marshalling failed.");

            Delegate_String ret = GetDelegate();
            ThrowIfNotEquals(true, ret("Hello World!"), "Delegate as P/Invoke return failed");

            Delegate_String d = new Delegate_String(new ClosedDelegateCLass().GetString);
            ThrowIfNotEquals(true, Callback(ref d), "Delegate IN marshalling failed");
            ThrowIfNotEquals(true, d("Hello World!"), "Delegate OUT marshalling failed");

            Delegate_String ds = new Delegate_String((new ClosedDelegateCLass()).GetString);
            ThrowIfNotEquals(true, ReversePInvoke_String(ds), "Delegate marshalling failed.");
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

        // A second struct with the same name but nested. Regression test against native types being mangled into
        // the compiler-generated type and losing fully qualified type name information.
        class NesterOfSequentialStruct
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct SequentialStruct
            {
                public float f1;
                public int f2;
            }
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

            NesterOfSequentialStruct.SequentialStruct ss3 = new NesterOfSequentialStruct.SequentialStruct();
            ss3.f1 = 10.0f;
            ss3.f2 = 123;

            ThrowIfNotEquals(true, StructTest_Sequential2(ss3), "Struct marshalling scenario1 failed.");

            ExplicitStruct es = new ExplicitStruct();
            es.f1 = 100;
            es.f2 = 100.0f;
            es.f3 = "Hello";
            ThrowIfNotEquals(true, StructTest_Explicit(es), "Struct marshalling scenario4 failed.");

            NestedStruct ns = new NestedStruct();
            ns.f1 = 100;
            ns.f2 = es;
            ThrowIfNotEquals(true, StructTest_Nested(ns), "Struct marshalling scenario5 failed.");

            SequentialStruct[] ssa = null;
            ThrowIfNotEquals(true, IsNULL(ssa), "Non-blittable array null check failed");

            ssa = new SequentialStruct[3];
            for (int i = 0; i < 3; i++)
            {
                ssa[i].f1 = 0;
                ssa[i].f1 = i;
                ssa[i].f2 = i*i;
                ssa[i].f3 = i.LowLevelToString(); 
            }
            ThrowIfNotEquals(true, StructTest_Array(ssa, ssa.Length), "Array of struct marshalling failed");

            InlineArrayStruct ias = new InlineArrayStruct();
            ias.inlineArray = new short[128];

            for (short i = 0; i < 128; i++)
            {
                ias.inlineArray[i] = i;
            }

            ias.inlineString = "Hello";

            InlineUnicodeStruct ius = new InlineUnicodeStruct();
            ius.inlineString = "Hello World";

#if !CODEGEN_CPP
            ThrowIfNotEquals(true, InlineArrayTest(ref ias, ref ius), "inline array marshalling failed");
            bool pass = true;
            for (short i = 0; i < 128; i++)
            {
                if (ias.inlineArray[i] != i + 1)
                {
                    pass = false;
                }
            }
            ThrowIfNotEquals(true, pass, "inline array marshalling failed");

            ThrowIfNotEquals("Hello World", ias.inlineString, "Inline ByValTStr Ansi marshalling failed");

            ThrowIfNotEquals("Hello World", ius.inlineString, "Inline ByValTStr Unicode marshalling failed");

            // RhpThrowEx is not implemented in CPPCodeGen
            pass = false;
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

    public static class LowLevelExtensions
    {
        // Int32.ToString() calls into glob/loc garbage that hits CppCodegen limitations
        public static string LowLevelToString(this int i)
        {
            char[] digits = new char[11];
            int numDigits = 0;

            if (i == int.MinValue)
                return "-2147483648";

            bool negative = i < 0;
            if (negative)
                i = -i;

            do
            {
                digits[numDigits] = (char)('0' + (i % 10));
                numDigits++;
                i /= 10;
            }
            while (i != 0);
            if (negative)
            {
                digits[numDigits] = '-';
                numDigits++;
            }
            Array.Reverse(digits);
            return new string(digits, digits.Length - numDigits, numDigits);
        }
    }
}
