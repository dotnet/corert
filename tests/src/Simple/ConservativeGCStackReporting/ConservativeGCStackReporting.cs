// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

public class BringUpTests
{

    const int Pass = 100;
    const int Fail = -1;

    public static int Main()
    {
        int result = Pass;

        if (!TestConservativeGCStackReporting())
        {
            Console.WriteLine("Failed");
            result = Fail;
        }

        return result;
    }

    private static bool TestConservativeGCStackReporting()
    {
        List<Thread> threadsToStart = new List<Thread>();
        int numThreads = 100;
        int numIterations = 10000;
        for(int i = 0; i < numThreads ; i++)
        {
            //threadsToStart.Add(new Thread(() => RunDelegateTest(numIterations)));
            threadsToStart.Add(new Thread(() => RunInterfaceTest(numIterations)));            
        }

        threadsToStart.Add(new Thread(() => RunGC(numIterations)));

        foreach(Thread t in threadsToStart)
        {
            t.Start();
        }

        foreach(Thread t in threadsToStart)
        {
            t.Join();
        }

        return true;
    }

    private static void RunDelegateTest(int numIterations)
    {
        while (numIterations > 0)
        {
            TestDelegate();
            numIterations--;
        }
    }
    private static void RunInterfaceTest(int numIterations)
    {
        while (numIterations > 0)
        {
            TestInterfaceCache();
            numIterations--;
        }
    }

    private static void RunGC(int numIterations)
    {
        while (numIterations > 0)
        {
            GC.Collect();
            numIterations--;
        }
    }

    #region Interface Dispatch Cache Test

    private static int TestInterfaceCache()
    {
        MyInterface[] itfs = new MyInterface[50];

        itfs[0] = new Foo0();
        itfs[1] = new Foo1();
        itfs[2] = new Foo2();
        itfs[3] = new Foo3();
        itfs[4] = new Foo4();
        itfs[5] = new Foo5();
        itfs[6] = new Foo6();
        itfs[7] = new Foo7();
        itfs[8] = new Foo8();
        itfs[9] = new Foo9();
        itfs[10] = new Foo10();
        itfs[11] = new Foo11();
        itfs[12] = new Foo12();
        itfs[13] = new Foo13();
        itfs[14] = new Foo14();
        itfs[15] = new Foo15();
        itfs[16] = new Foo16();
        itfs[17] = new Foo17();
        itfs[18] = new Foo18();
        itfs[19] = new Foo19();
        itfs[20] = new Foo20();
        itfs[21] = new Foo21();
        itfs[22] = new Foo22();
        itfs[23] = new Foo23();
        itfs[24] = new Foo24();
        itfs[25] = new Foo25();
        itfs[26] = new Foo26();
        itfs[27] = new Foo27();
        itfs[28] = new Foo28();
        itfs[29] = new Foo29();
        itfs[30] = new Foo30();
        itfs[31] = new Foo31();
        itfs[32] = new Foo32();
        itfs[33] = new Foo33();
        itfs[34] = new Foo34();
        itfs[35] = new Foo35();
        itfs[36] = new Foo36();
        itfs[37] = new Foo37();
        itfs[38] = new Foo38();
        itfs[39] = new Foo39();
        itfs[40] = new Foo40();
        itfs[41] = new Foo41();
        itfs[42] = new Foo42();
        itfs[43] = new Foo43();
        itfs[44] = new Foo44();
        itfs[45] = new Foo45();
        itfs[46] = new Foo46();
        itfs[47] = new Foo47();
        itfs[48] = new Foo48();
        itfs[49] = new Foo49();

        StringBuilder sb = new StringBuilder();
        int counter = 0;
        for (int i = 0; i < 50; i++)
        {
            sb.Append(itfs[i].GetAString());
            counter += itfs[i].GetAnInt();
        }

        string expected = "Foo0Foo1Foo2Foo3Foo4Foo5Foo6Foo7Foo8Foo9Foo10Foo11Foo12Foo13Foo14Foo15Foo16Foo17Foo18Foo19Foo20Foo21Foo22Foo23Foo24Foo25Foo26Foo27Foo28Foo29Foo30Foo31Foo32Foo33Foo34Foo35Foo36Foo37Foo38Foo39Foo40Foo41Foo42Foo43Foo44Foo45Foo46Foo47Foo48Foo49";

        if (!expected.Equals(sb.ToString()))
        {
            Console.WriteLine("Concatenating strings from interface calls failed.");
            Console.Write("Expected: ");
            Console.WriteLine(expected);
            Console.Write(" Actual: ");
            Console.WriteLine(sb.ToString());
            return Fail;
        }

        if (counter != 1225)
        {
            Console.WriteLine("Summing ints from interface calls failed.");
            Console.WriteLine("Expected: 1225");
            Console.Write("Actual: ");
            Console.WriteLine(counter);
            return Fail;
        }

        return 100;
    }

    interface MyInterface
    {
        int GetAnInt();
        string GetAString();
    }

    class Foo0 : MyInterface { public int GetAnInt() { return 0; } public string GetAString() { return "Foo0"; } }
    class Foo1 : MyInterface { public int GetAnInt() { return 1; } public string GetAString() { return "Foo1"; } }
    class Foo2 : MyInterface { public int GetAnInt() { return 2; } public string GetAString() { return "Foo2"; } }
    class Foo3 : MyInterface { public int GetAnInt() { return 3; } public string GetAString() { return "Foo3"; } }
    class Foo4 : MyInterface { public int GetAnInt() { return 4; } public string GetAString() { return "Foo4"; } }
    class Foo5 : MyInterface { public int GetAnInt() { return 5; } public string GetAString() { return "Foo5"; } }
    class Foo6 : MyInterface { public int GetAnInt() { return 6; } public string GetAString() { return "Foo6"; } }
    class Foo7 : MyInterface { public int GetAnInt() { return 7; } public string GetAString() { return "Foo7"; } }
    class Foo8 : MyInterface { public int GetAnInt() { return 8; } public string GetAString() { return "Foo8"; } }
    class Foo9 : MyInterface { public int GetAnInt() { return 9; } public string GetAString() { return "Foo9"; } }
    class Foo10 : MyInterface { public int GetAnInt() { return 10; } public string GetAString() { return "Foo10"; } }
    class Foo11 : MyInterface { public int GetAnInt() { return 11; } public string GetAString() { return "Foo11"; } }
    class Foo12 : MyInterface { public int GetAnInt() { return 12; } public string GetAString() { return "Foo12"; } }
    class Foo13 : MyInterface { public int GetAnInt() { return 13; } public string GetAString() { return "Foo13"; } }
    class Foo14 : MyInterface { public int GetAnInt() { return 14; } public string GetAString() { return "Foo14"; } }
    class Foo15 : MyInterface { public int GetAnInt() { return 15; } public string GetAString() { return "Foo15"; } }
    class Foo16 : MyInterface { public int GetAnInt() { return 16; } public string GetAString() { return "Foo16"; } }
    class Foo17 : MyInterface { public int GetAnInt() { return 17; } public string GetAString() { return "Foo17"; } }
    class Foo18 : MyInterface { public int GetAnInt() { return 18; } public string GetAString() { return "Foo18"; } }
    class Foo19 : MyInterface { public int GetAnInt() { return 19; } public string GetAString() { return "Foo19"; } }
    class Foo20 : MyInterface { public int GetAnInt() { return 20; } public string GetAString() { return "Foo20"; } }
    class Foo21 : MyInterface { public int GetAnInt() { return 21; } public string GetAString() { return "Foo21"; } }
    class Foo22 : MyInterface { public int GetAnInt() { return 22; } public string GetAString() { return "Foo22"; } }
    class Foo23 : MyInterface { public int GetAnInt() { return 23; } public string GetAString() { return "Foo23"; } }
    class Foo24 : MyInterface { public int GetAnInt() { return 24; } public string GetAString() { return "Foo24"; } }
    class Foo25 : MyInterface { public int GetAnInt() { return 25; } public string GetAString() { return "Foo25"; } }
    class Foo26 : MyInterface { public int GetAnInt() { return 26; } public string GetAString() { return "Foo26"; } }
    class Foo27 : MyInterface { public int GetAnInt() { return 27; } public string GetAString() { return "Foo27"; } }
    class Foo28 : MyInterface { public int GetAnInt() { return 28; } public string GetAString() { return "Foo28"; } }
    class Foo29 : MyInterface { public int GetAnInt() { return 29; } public string GetAString() { return "Foo29"; } }
    class Foo30 : MyInterface { public int GetAnInt() { return 30; } public string GetAString() { return "Foo30"; } }
    class Foo31 : MyInterface { public int GetAnInt() { return 31; } public string GetAString() { return "Foo31"; } }
    class Foo32 : MyInterface { public int GetAnInt() { return 32; } public string GetAString() { return "Foo32"; } }
    class Foo33 : MyInterface { public int GetAnInt() { return 33; } public string GetAString() { return "Foo33"; } }
    class Foo34 : MyInterface { public int GetAnInt() { return 34; } public string GetAString() { return "Foo34"; } }
    class Foo35 : MyInterface { public int GetAnInt() { return 35; } public string GetAString() { return "Foo35"; } }
    class Foo36 : MyInterface { public int GetAnInt() { return 36; } public string GetAString() { return "Foo36"; } }
    class Foo37 : MyInterface { public int GetAnInt() { return 37; } public string GetAString() { return "Foo37"; } }
    class Foo38 : MyInterface { public int GetAnInt() { return 38; } public string GetAString() { return "Foo38"; } }
    class Foo39 : MyInterface { public int GetAnInt() { return 39; } public string GetAString() { return "Foo39"; } }
    class Foo40 : MyInterface { public int GetAnInt() { return 40; } public string GetAString() { return "Foo40"; } }
    class Foo41 : MyInterface { public int GetAnInt() { return 41; } public string GetAString() { return "Foo41"; } }
    class Foo42 : MyInterface { public int GetAnInt() { return 42; } public string GetAString() { return "Foo42"; } }
    class Foo43 : MyInterface { public int GetAnInt() { return 43; } public string GetAString() { return "Foo43"; } }
    class Foo44 : MyInterface { public int GetAnInt() { return 44; } public string GetAString() { return "Foo44"; } }
    class Foo45 : MyInterface { public int GetAnInt() { return 45; } public string GetAString() { return "Foo45"; } }
    class Foo46 : MyInterface { public int GetAnInt() { return 46; } public string GetAString() { return "Foo46"; } }
    class Foo47 : MyInterface { public int GetAnInt() { return 47; } public string GetAString() { return "Foo47"; } }
    class Foo48 : MyInterface { public int GetAnInt() { return 48; } public string GetAString() { return "Foo48"; } }
    class Foo49 : MyInterface { public int GetAnInt() { return 49; } public string GetAString() { return "Foo49"; } }

    #endregion

    #region PInvokeTests

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
    
    static int Sum(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j)
    {
        return a + b + c + d + e + f + g + h + i + j;
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
    
    [DllImport("*", CallingConvention = CallingConvention.StdCall)]
    static extern Delegate_String GetDelegate();

    delegate int Delegate_Int(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j);
    [DllImport("*", CallingConvention = CallingConvention.StdCall)]
    static extern bool ReversePInvoke_Int(Delegate_Int del);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
    delegate bool Delegate_String(string s);
    [DllImport("*", CallingConvention = CallingConvention.StdCall)]
    static extern bool ReversePInvoke_String(Delegate_String del);
    
    [DllImport("*", CallingConvention = CallingConvention.StdCall)]    
    static extern bool Callback(ref Delegate_String d);

    [DllImport("*", CallingConvention = CallingConvention.StdCall)]
    internal static extern IntPtr GetFunctionPointer();

    delegate void Delegate_Unused();
    [DllImport("*", CallingConvention = CallingConvention.StdCall)]
    static extern unsafe int* ReversePInvoke_Unused(Delegate_Unused del);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
    public unsafe delegate void SetLastErrorFuncDelegate(int errorCode);

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
        
        IntPtr procAddress = GetFunctionPointer();
        SetLastErrorFuncDelegate funcDelegate =
            Marshal.GetDelegateForFunctionPointer<SetLastErrorFuncDelegate>(procAddress);
        
        funcDelegate(0x204);
        ThrowIfNotEquals(0x204, Marshal.GetLastWin32Error(), "Not match");
    }

    #endregion
}