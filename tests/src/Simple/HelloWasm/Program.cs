// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
#if PLATFORM_WINDOWS
using CpObj;
#endif

internal static class Program
{
    private static int staticInt;
    [ThreadStatic]
    private static int threadStaticInt;
    private static unsafe int Main(string[] args)
    {
        PrintLine("Starting");

        Add(1, 2);
        int tempInt = 0;
        int tempInt2 = 0;
        (*(&tempInt)) = 9;
        if(tempInt == 9)
        {
            PrintLine("Hello from C#!");
        }

        int* targetAddr = (tempInt > 0) ? (&tempInt2) : (&tempInt);

        (*targetAddr) = 1;
        if(tempInt2 == 1 && tempInt == 9)
        {
            PrintLine("basic block stack entry Test: Ok.");
        }

        if(ILHelpers.ILHelpersTest.InlineAssignByte() == 100)
        {
            PrintLine("Inline assign byte Test: Ok.");
        }
        else
        {
            PrintLine("Inline assign byte Test: Failed.");
        }

        int dupTestInt = 9;
        if(ILHelpers.ILHelpersTest.DupTest(ref dupTestInt) == 209 && dupTestInt == 209)
        {
            PrintLine("dup test: Ok.");
        }
        else
        {
            PrintLine("dup test: Failed.");
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

        StaticCtorTest();

        var boxedInt = (object)tempInt;
        if(((int)boxedInt) == 9)
        {
            PrintLine("box test: Ok.");
        }
        else
        {
            PrintLine("box test: Failed. Value:");
            PrintLine(boxedInt.ToString());
        }
        
        var boxedStruct = (object)new BoxStubTest { Value = "Boxed Stub Test: Ok." };
        PrintLine(boxedStruct.ToString());

        int subResult = tempInt - 1;
        if (subResult == 8)
        {
            PrintLine("Subtraction Test: Ok.");
        }

        int divResult = tempInt / 3;
        if (divResult == 3)
        {
            PrintLine("Division Test: Ok.");
        }

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

#if PLATFORM_WINDOWS
        var cpObjTestA = new TestValue { Field = 1234 };
        var cpObjTestB = new TestValue { Field = 5678 };
        CpObjTest.CpObj(ref cpObjTestB, ref cpObjTestA);
        if (cpObjTestB.Field == 1234)
        {
            PrintLine("CpObj test: Ok.");
        }
#endif

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

        int ii = 0;
        arrayTest[ii++].Value = "dup ref test: Ok.";
        PrintLine(arrayTest[0].Value);
        
        var largeArrayTest = new long[] { Int64.MaxValue, 0, Int64.MinValue, 0 };
        if(largeArrayTest[0] == Int64.MaxValue &&
            largeArrayTest[1] == 0 &&
            largeArrayTest[2] == Int64.MinValue &&
            largeArrayTest[3] == 0)
        {
            PrintLine("Large array load/store test: Ok.");
        }

        var smallArrayTest = new long[] { Int16.MaxValue, 0, Int16.MinValue, 0 };
        if(smallArrayTest[0] == Int16.MaxValue &&
            smallArrayTest[1] == 0 &&
            smallArrayTest[2] == Int16.MinValue &&
            smallArrayTest[3] == 0)
        {
            PrintLine("Small array load/store test: Ok.");
        }

        IntPtr returnedIntPtr = NewobjValueType();
        if (returnedIntPtr.ToInt32() == 3)
        {
            PrintLine("Newobj value type test: Ok.");
        }

        StackallocTest();

        IntToStringTest();

        CastingTestClass castingTest = new DerivedCastingTestClass1();

        PrintLine("interface call test: Ok " + (castingTest as ICastingTest1).GetValue().ToString());

        if (((DerivedCastingTestClass1)castingTest).GetValue() == 1 && !(castingTest is DerivedCastingTestClass2))
        {
            PrintLine("Type casting with isinst & castclass to class test: Ok.");
        }

        // Instead of checking the result of `GetValue`, we use null check by now until interface dispatch is implemented.
        if ((ICastingTest1)castingTest != null && !(castingTest is ICastingTest2))
        {
            PrintLine("Type casting with isinst & castclass to interface test: Ok.");
        }

        object arrayCastingTest = new BoxStubTest[] { new BoxStubTest { Value = "Array" }, new BoxStubTest { Value = "Cast" }, new BoxStubTest { Value = "Test" } };
        PrintLine(((BoxStubTest[])arrayCastingTest)[0].Value);
        PrintLine(((BoxStubTest[])arrayCastingTest)[1].Value);
        PrintLine(((BoxStubTest[])arrayCastingTest)[2].Value);
        if (!(arrayCastingTest is CastingTestClass[]))
        {   
            PrintLine("Type casting with isinst & castclass to array test: Ok.");
        }

        ldindTest();

        InterfaceDispatchTest();

        var testRuntimeHelpersInitArray = new long[] { 1, 2, 3 };
        if (testRuntimeHelpersInitArray[0] == 1 &&
            testRuntimeHelpersInitArray[1] == 2 &&
            testRuntimeHelpersInitArray[2] == 3)
        {
            PrintLine("Runtime.Helpers array initialization test: Ok.");
        }

        var testMdArrayInstantiation = new int[2, 2];
        if (testMdArrayInstantiation != null && testMdArrayInstantiation.GetLength(0) == 2 && testMdArrayInstantiation.GetLength(1) == 2)
            PrintLine("Multi-dimension array instantiation test: Ok.");

        int intToCast = 1;
        double castedDouble = (double)intToCast;
        if (castedDouble == 1d)
        {
            PrintLine("(double) cast test: Ok.");
        }
        else
        {
            var toInt = (int)castedDouble;
//            PrintLine("expected 1m, but was " + castedDouble.ToString());  // double.ToString is not compiling at the time of writing, but this would be better output
            PrintLine($"(double) cast test : Failed. Back to int on next line");
            PrintLine(toInt.ToString());
        }

        if (1f < 2d && 1d < 2f && 1f == 1d)
        {
            PrintLine("different width float comparisons: Ok.");
        }

        // floats are 7 digits precision, so check some double more precise to make sure there is no loss occurring through some inadvertent cast to float
        if (10.23456789d != 10.234567891d)
        {
            PrintLine("double precision comparison: Ok.");
        }

        if (12.34567f == 12.34567f && 12.34567f != 12.34568f)
        {
            PrintLine("float comparison: Ok.");
        }

        // Create a ByReference<char> through the ReadOnlySpan ctor and call the ByReference.Value via the indexer.
        var span = "123".AsSpan();
        if (span[0] != '1'
            || span[1] != '2'
            || span[2] != '3')
        {
            PrintLine("ByReference intrinsics exercise via ReadOnlySpan failed");
            PrintLine(span[0].ToString());
            PrintLine(span[1].ToString());
            PrintLine(span[2].ToString());
        }
        else
        {
            PrintLine("ByReference intrinsics exercise via ReadOnlySpan OK.");
        }

        TestConstrainedClassCalls();

        // This test should remain last to get other results before stopping the debugger
        PrintLine("Debugger.Break() test: Ok if debugger is open and breaks.");
        System.Diagnostics.Debugger.Break();

        PrintLine("Done");
        return 100;
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

    private static IntPtr NewobjValueType()
    {
        return new IntPtr(3);
    }

    private unsafe static void StackallocTest()
    {
        int* intSpan = stackalloc int[2];
        intSpan[0] = 3;
        intSpan[1] = 7;

        if (intSpan[0] == 3 && intSpan[1] == 7)
        {
            PrintLine("Stackalloc test: Ok.");
        }
    }

    private static void IntToStringTest()
    {
        PrintLine("Int to String Test: Ok if next line says 42.");
        string intString = 42.ToString();
        PrintLine(intString);
    }

    private unsafe static void ldindTest()
    {
        var ldindTarget = new TwoByteStr { first = byte.MaxValue, second = byte.MinValue };
        var ldindField = &ldindTarget.first;
        if((*ldindField) == byte.MaxValue)
        {
            ldindTarget.second = byte.MaxValue;
            *ldindField = byte.MinValue;
            //ensure there isnt any overwrite of nearby fields
            if(ldindTarget.first == byte.MinValue && ldindTarget.second == byte.MaxValue)
            {
                PrintLine("ldind test: Ok.");
            }
            else if(ldindTarget.first != byte.MinValue)
            {
                PrintLine("ldind test: Failed didnt update target.");
            }
            else
            {
                PrintLine("ldind test: Failed overwrote data");
            }
        }
        else
        {
            uint ldindFieldValue = *ldindField;
            PrintLine("ldind test: Failed." + ldindFieldValue.ToString());
        }
    }

    private static void InterfaceDispatchTest()
    {
        ItfStruct itfStruct = new ItfStruct();
        if (ItfCaller(itfStruct) == 4)
        {
            PrintLine("Struct interface test: Ok.");
        }
    }

    // Calls the ITestItf interface via a generic to ensure the concrete type is known and
    // an interface call is generated instead of a virtual or direct call
    private static int ItfCaller<T>(T obj) where T : ITestItf
    {
        return obj.GetValue();
    }

    private static void StaticCtorTest()
    {
        BeforeFieldInitTest.Nop();
        if (StaticsInited.BeforeFieldInitInited)
        {
            PrintLine("BeforeFieldInitType inited too early");
        }
        else
        {
            int x = BeforeFieldInitTest.TestField;
            if (StaticsInited.BeforeFieldInitInited)
            {
                PrintLine("BeforeFieldInit test: Ok.");
            }
            else
            {
                PrintLine("BeforeFieldInit cctor not run");
            }
        }

        NonBeforeFieldInitTest.Nop();
        if (StaticsInited.NonBeforeFieldInitInited)
        {
            PrintLine("NonBeforeFieldInit test: Ok.");
        }
        else
        { 
            PrintLine("NonBeforeFieldInitType cctor not run");
        }
    }

    private static void TestConstrainedClassCalls()
    {
        string s = "utf-8";

        PrintString("Direct ToString test: ");
        string stringDirectToString = s.ToString();
        if (s.Equals(stringDirectToString))
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintString("Failed. Returned string:\"");
            PrintString(stringDirectToString);
            PrintLine("\"");
        }
       
        // Generic calls on methods not defined on object
        uint dataFromBase = GenericGetData<MyBase>(new MyBase(11));
        PrintString("Generic call to base class test: ");
        if (dataFromBase == 11)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        uint dataFromUnsealed = GenericGetData<UnsealedDerived>(new UnsealedDerived(13));
        PrintString("Generic call to unsealed derived class test: ");
        if (dataFromUnsealed == 26)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        uint dataFromSealed = GenericGetData<SealedDerived>(new SealedDerived(15));
        PrintString("Generic call to sealed derived class test: ");
        if (dataFromSealed == 45)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        uint dataFromUnsealedAsBase = GenericGetData<MyBase>(new UnsealedDerived(17));
        PrintString("Generic call to unsealed derived class as base test: ");
        if (dataFromUnsealedAsBase == 34)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        uint dataFromSealedAsBase = GenericGetData<MyBase>(new SealedDerived(19));
        PrintString("Generic call to sealed derived class as base test: ");
        if (dataFromSealedAsBase == 57)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        // Generic calls to methods defined on object
        uint hashCodeOfSealedViaGeneric = (uint)GenericGetHashCode<MySealedClass>(new MySealedClass(37));
        PrintString("Generic GetHashCode for sealed class test: ");
        if (hashCodeOfSealedViaGeneric == 74)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        uint hashCodeOfUnsealedViaGeneric = (uint)GenericGetHashCode<MyUnsealedClass>(new MyUnsealedClass(41));
        PrintString("Generic GetHashCode for unsealed class test: ");
        if (hashCodeOfUnsealedViaGeneric == 82)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }
    }

    static uint GenericGetData<T>(T obj) where T : MyBase
    {
        return obj.GetData();
    }

    static int GenericGetHashCode<T>(T obj)
    {
        return obj.GetHashCode();
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

public class StaticsInited
{
    public static bool BeforeFieldInitInited;
    public static bool NonBeforeFieldInitInited;
}

public class BeforeFieldInitTest
{
    public static int TestField = BeforeFieldInit();

    public static void Nop() { }

    static int BeforeFieldInit()
    {
        StaticsInited.BeforeFieldInitInited = true;
        return 3;
    }
}

public class NonBeforeFieldInitTest
{
    public static int TestField;

    public static void Nop() { }

    static NonBeforeFieldInitTest()
    {
        TestField = 4;
        StaticsInited.NonBeforeFieldInitInited = true;
    }
}

public interface ICastingTest1
{
    int GetValue();
}

public interface ICastingTest2
{
    int GetValue();
}

public abstract class CastingTestClass
{
    public abstract int GetValue();
}

public class DerivedCastingTestClass1 : CastingTestClass, ICastingTest1
{
    public override int GetValue() => 1;
}

public class DerivedCastingTestClass2 : CastingTestClass, ICastingTest2
{
    public override int GetValue() => 2;
}

public interface ITestItf
{
    int GetValue();
}

public struct ItfStruct : ITestItf
{
    public int GetValue()
    {
        return 4;
    }
}

public sealed class MySealedClass
{
    uint _data;

    public MySealedClass()
    {
        _data = 104;
    }

    public MySealedClass(uint data)
    {
        _data = data;
    }

    public uint GetData()
    {
        return _data;
    }

    public override int GetHashCode()
    {
        return (int)_data * 2;
    }

    public override string ToString()
    {
        Program.PrintLine("MySealedClass.ToString called. Data:");
        Program.PrintLine(_data.ToString());
        return _data.ToString();
    }
}

public class MyUnsealedClass
{
    uint _data;

    public MyUnsealedClass()
    {
        _data = 24;
    }

    public MyUnsealedClass(uint data)
    {
        _data = data;
    }

    public uint GetData()
    {
        return _data;
    }

    public override int GetHashCode()
    {
        return (int)_data * 2;
    }

    public override string ToString()
    {
        return _data.ToString();
    }
}

public class MyBase
{
    protected uint _data;
    public MyBase(uint data)
    {
        _data = data;
    }

    public virtual uint GetData()
    {
        return _data;
    }
}

public class UnsealedDerived : MyBase
{
    public UnsealedDerived(uint data) : base(data) { }
    public override uint GetData()
    {
        return _data * 2;
    }
}

public sealed class SealedDerived : MyBase
{
    public SealedDerived(uint data) : base(data) { }
    public override uint GetData()
    {
        return _data * 3;
    }
}
