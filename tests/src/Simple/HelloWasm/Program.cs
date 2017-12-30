// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using CpObj;

internal static class Program
{
    private static int staticInt;
    [ThreadStatic]
    private static int threadStaticInt;
    private static unsafe void Main(string[] args)
    {
        Add(1, 2);
        int tempInt = 0;
        (*(&tempInt)) = 9;
        if(tempInt == 9)
        {
            PrintLine("Hello from C#!");
        }

        TestClass tempObj = new TestDerivedClass(1337);
        tempObj.TestMethod("Hello");
        tempObj.TestVirtualMethod("Hello");
        tempObj.TestVirtualMethod2("Hello");

        TwoByteStr str = new TwoByteStr() { first = 1, second = 2 };
        TwoByteStr str2 = new TwoByteStr() { first = 3, second = 4 };
        *(&str) = str2;
        str2 = *(&str);

        if (str2.second == 4)
        {
            PrintLine("value type int field test: Ok.");
        }
        
        staticInt = 5;
        if (staticInt == 5)
        {
            PrintLine("static int field test: Ok.");
        }

        if(threadStaticInt == 0)
        {
            PrintLine("thread static int initial value field test: Ok.");
        }

        threadStaticInt = 9;
        if(threadStaticInt == 9)
        {
            PrintLine("thread static int field test: Ok.");
        }

        var boxedInt = (object)tempInt;
        if(((int)boxedInt) == 9)
        {
            PrintLine("box test: Ok.");
        }

        var boxedStruct = (object)new BoxStubTest { Value = "Boxed Stub Test: Ok." };
        PrintLine(boxedStruct.ToString());

        var not = Not(0xFFFFFFFF) == 0x00000000;
        if (not)
        {
            PrintLine("not test: Ok.");
        }

        var negInt = Neg(42) == -42;
        if (negInt)
        {
            PrintLine("negInt test: Ok.");
        }

        var shiftLeft = ShiftLeft(1, 2) == 4;
        if (shiftLeft)
        {
            PrintLine("shiftLeft test: Ok.");
        }

        var shiftRight = ShiftRight(4, 2) == 1;
        if (shiftRight)
        {
            PrintLine("shiftRight test: Ok.");
        }

        var unsignedShift = UnsignedShift(0xFFFFFFFFu, 4) == 0x0FFFFFFFu;
        if (unsignedShift)
        {
            PrintLine("unsignedShift test: Ok.");
        }
        
        var switchTest0 = SwitchOp(5, 5, 0);
        if (switchTest0 == 10)
        {
            PrintLine("SwitchOp0 test: Ok.");
        }

        var switchTest1 = SwitchOp(5, 5, 1);
        if (switchTest1 == 25)
        {
            PrintLine("SwitchOp1 test: Ok.");
        }

        var switchTestDefault = SwitchOp(5, 5, 20);
        if (switchTestDefault == 0)
        {
            PrintLine("SwitchOpDefault test: Ok.");
        }

        var cpObjTestA = new TestValue { Field = 1234 };
        var cpObjTestB = new TestValue { Field = 5678 };
        CpObjTest.CpObj(ref cpObjTestB, ref cpObjTestA);
        if (cpObjTestB.Field == 1234)
        {
            PrintLine("CpObj test: Ok.");
        }

        Func<int> staticDelegate = StaticDelegateTarget;
        if(staticDelegate() == 7)
        {
            PrintLine("Static delegate test: Ok.");
        }

        tempObj.TestInt = 8;
        Func<int> instanceDelegate = tempObj.InstanceDelegateTarget;
        if(instanceDelegate() == 8)
        {
            PrintLine("Instance delegate test: Ok.");
        }

        Action virtualDelegate = tempObj.VirtualDelegateTarget;
        virtualDelegate();

        var arrayTest = new BoxStubTest[] { new BoxStubTest { Value = "Hello" }, new BoxStubTest { Value = "Array" }, new BoxStubTest { Value = "Test" } };
        foreach(var element in arrayTest)
            PrintLine(element.Value);

        arrayTest[1].Value = "Array load/store test: Ok.";
        
        PrintLine(arrayTest[1].Value);

        PrintLine("Done");
    }

    private static int StaticDelegateTarget()
    {
        return 7;
    }

    private static unsafe void PrintString(string s)
    {
        int length = s.Length;
        fixed (char* curChar = s)
        {
            for (int i = 0; i < length; i++)
            {
                TwoByteStr curCharStr = new TwoByteStr();
                curCharStr.first = (byte)(*(curChar + i));
                printf((byte*)&curCharStr, null);
            }
        }
    }
    
    public static void PrintLine(string s)
    {
        PrintString(s);
        PrintString("\n");
    }

    private static int Add(int a, int b)
    {
        return a + b;
    }

    private static uint Not(uint a)
    {
        return ~a;
    }

    private static int Neg(int a)
    {
        return -a;
    }

    private static int ShiftLeft(int a, int b)
    {
        return a << b;
    }

    private static int ShiftRight(int a, int b)
    {
        return a >> b;
    }

    private static uint UnsignedShift(uint a, int b)
    {
        return a >> b;
    }
    
    private static int SwitchOp(int a, int b, int mode)
    {
        switch(mode)
        {
          case 0:
            return a + b;
          case 1:
            return a * b;
          case 2:
            return a / b;
          case 3:
            return a - b;
          default:
            return 0;
        }
    }

    [DllImport("*")]
    private static unsafe extern int printf(byte* str, byte* unused);
}

public struct TwoByteStr
{
    public byte first;
    public byte second;
}

public struct BoxStubTest
{
    public string Value;
    public override string ToString()
    {
        return Value;
    }

    public string GetValue()
    {
        Program.PrintLine("BoxStubTest.GetValue called");
        Program.PrintLine(Value);
        return Value;
    }
}

public class TestClass
{
    public string TestString { get; set; }
    public int TestInt { get; set; }


    public TestClass(int number)
    {
        if(number != 1337)
            throw new Exception();
    }

    public void TestMethod(string str)
    {
        TestString = str;
        if (TestString == str)
            Program.PrintLine("Instance method call test: Ok.");
    }
    public virtual void TestVirtualMethod(string str)
    {
        Program.PrintLine("Virtual Slot Test: Ok If second");
    }
	
	public virtual void TestVirtualMethod2(string str)
    {
        Program.PrintLine("Virtual Slot Test 2: Ok");
    }

    public int InstanceDelegateTarget()
    {
        return TestInt;
    }

    public virtual void VirtualDelegateTarget()
    {
        Program.PrintLine("Virtual delegate incorrectly dispatched to base.");
    }
}

public class TestDerivedClass : TestClass
{
    public TestDerivedClass(int number) : base(number)
    {

    }
    public override void TestVirtualMethod(string str)
    {
        Program.PrintLine("Virtual Slot Test: Ok");
        base.TestVirtualMethod(str);
    }
    
    public override string ToString()
    {
        throw new Exception();
    }

    public override void VirtualDelegateTarget()
    {
        Program.PrintLine("Virtual Delegate Test: Ok");
    }
}

