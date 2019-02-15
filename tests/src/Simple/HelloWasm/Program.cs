// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Reflection;

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

        FloatDoubleTest();

        long l = 0x1;
        if (l > 0x7FF0000000000000)
        {
            PrintLine("long comparison: Failed");
        }
        else
        {
            PrintLine("long comparison: Ok");
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

        TestValueTypeElementIndexing();
        
        TestArrayItfDispatch();

        TestMetaData();
        
        TestTryFinally();

        int rvaFieldValue = ILHelpers.ILHelpersTest.StaticInitedInt;
        if (rvaFieldValue == 0x78563412)
        {
            PrintLine("RVA static field test: Ok.");
        }
        else
        {
            PrintLine("RVA static field test: Failed.");
            PrintLine(rvaFieldValue.ToString());
        }

        TestNativeCallback();

        TestArgsWithMixedTypesAndExceptionRegions();

        TestThreadStaticsForSingleThread();

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

        ClassWithSealedVTable classWithSealedVTable = new ClassWithSealedVTable();
        PrintString("Interface dispatch with sealed vtable test: ");
        if (CallItf(classWithSealedVTable) == 37)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }
    }

    // Calls the ITestItf interface via a generic to ensure the concrete type is known and
    // an interface call is generated instead of a virtual or direct call
    private static int ItfCaller<T>(T obj) where T : ITestItf
    {
        return obj.GetValue();
    }

    private static int CallItf(ISomeItf asItf)
    {
        return asItf.GetValue();
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

    private static void TestArrayItfDispatch()
    {
        ICollection<int> arrayItfDispatchTest = new int[37];
        PrintString("Array interface dispatch test: ");
        if (arrayItfDispatchTest.Count == 37)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.  asm.js (WASM=1) known to fail due to alignment problem, although this problem sometimes means we don't even get this far and fails with an invalid function pointer.");
        }
    }

    private static void TestValueTypeElementIndexing()
    {
        var chars = new[] { 'i', 'p', 's', 'u', 'm' };
        PrintString("Value type element indexing: ");
        if (chars[0] == 'i' && chars[1] == 'p' && chars[2] == 's' && chars[3] == 'u' && chars[4] == 'm')
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }
    }

    private static void FloatDoubleTest()
    {
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

        PrintString("Test comparison of float constant: ");
        var maxFloat = Single.MaxValue;
        if (maxFloat == Single.MaxValue)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        PrintString("Test comparison of double constant: ");
        var maxDouble = Double.MaxValue;
        if (maxDouble == Double.MaxValue)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }
    }

    private static bool callbackResult;
    private static unsafe void TestNativeCallback()
    {
        CallMe(123);
        PrintString("Native callback test: ");
        if (callbackResult)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }
    }

    [System.Runtime.InteropServices.NativeCallable(EntryPoint = "CallMe")]
    private static void _CallMe(int x)
    {
        if (x == 123)
        {
            callbackResult = true;
        }
    }

    [System.Runtime.InteropServices.DllImport("*")]
    private static extern void CallMe(int x);

    private static void TestMetaData()
    {

        var typeGetType = Type.GetType("System.Char, System.Private.CoreLib");
        if (typeGetType == null)
        {
            PrintLine("type == null.  Simple class metadata test: Failed");
        }
        else
        {
            if (typeGetType.FullName != "System.Char")
            {
                PrintLine("type != System.Char.  Simple class metadata test: Failed");
            }
            else PrintLine("Simple class metadata test: Ok.");
        }

        var typeofChar = typeof(Char);
        if (typeofChar == null)
        {
            PrintLine("type == null.  Simple struct metadata test: Failed");
        }
        else
        {
            if (typeofChar.FullName != "System.Char")
            {
                PrintLine("type != System.Char.  Simple struct metadata test: Failed");
            }
            else PrintLine("Simple struct metadata test (typeof(Char)): Ok.");
        }

        var gentT = new Gen<int>();
        var genParamType = gentT.TestTypeOf();
        PrintString("type of generic parameter: ");
        if (genParamType.FullName != "System.Int32")
        {
            PrintString("expected System.Int32 but was " + genParamType.FullName);
            PrintLine(" Failed.");
        }
        else
        {
            PrintLine("Ok.");
        }

        var arrayType = typeof(object[]);
        PrintString("type of array: ");
        if (arrayType.FullName != "System.Object[]")
        {
            PrintString("expected System.Object[] but was " + arrayType.FullName);
            PrintLine(" Failed.");
        }
        else
        {
            PrintLine("Ok.");
        }

        var genericType = typeof(List<object>);
        PrintString("type of generic : ");
        if (genericType.FullName != "System.Collections.Generic.List`1[[System.Object, System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a]]")
        {
            PrintString("expected System.Collections.Generic.List`1[[System.Object, System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a]] but was " + genericType.FullName);
            PrintLine(" Failed.");
        }
        else
        {
            PrintLine("Ok.");
        }

        PrintString("Type GetFields length: ");
        var x = new ClassForMetaTests();
        var s = x.StringField;  
        var i = x.IntField;
        var fieldClassType = typeof(ClassForMetaTests);
        FieldInfo[] fields = fieldClassType.GetFields();
        if (fields.Length == 3)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine(" Failed.");
        }

        PrintString("Type get string field via reflection: ");
        var stringFieldInfo = fieldClassType.GetField("StringField");
        if ((string)stringFieldInfo.GetValue(x) == s)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }
        PrintString("Type get int field via reflection: ");
        var intFieldInfo = fieldClassType.GetField("IntField");
        if ((int)intFieldInfo.GetValue(x) == i)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        PrintString("Type get static int field via reflection: ");
        var staticIntFieldInfo = fieldClassType.GetField("StaticIntField");
        if ((int)staticIntFieldInfo.GetValue(x) == 23)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        PrintString("Type set string field via reflection: ");
        stringFieldInfo.SetValue(x, "bcd");
        if (x.StringField == "bcd")
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        PrintString("Type set int field via reflection: ");
        intFieldInfo.SetValue(x, 456);
        if (x.IntField == 456)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }

        PrintString("Type set static int field via reflection: ");
        staticIntFieldInfo.SetValue(x, 987);
        if (ClassForMetaTests.StaticIntField == 987)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }
        var st = new StructForMetaTests();
        st.StringField = "xyz";
        var fieldStructType = typeof(StructForMetaTests);
        var structStringFieldInfo = fieldStructType.GetField("StringField");
        PrintString("Struct get string field via reflection: ");
        if ((string)structStringFieldInfo.GetValue(st) == "xyz")
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
        }
    }

    public class ClassForMetaTests
    {
        // used via reflection
#pragma warning disable 0169
        public int IntField;
        public string StringField;
#pragma warning restore 0169
        public static int StaticIntField;

        public ClassForMetaTests()
        {
            StringField = "ab";
            IntField = 12;
            StaticIntField = 23;
        }
    }

    public struct StructForMetaTests
    {
        public string StringField;
    }


    /// <summary>
    /// Ensures all of the blocks of a try/finally function are hit when there aren't exceptions
    /// </summary>
    private static void TestTryFinally()
    {
        PrintString("Try/Finally test: ");
        uint result = TryFinallyInner();
        if (result == 1111)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed. Result: " + result.ToString());
        }
    }

    private static uint TryFinallyInner()
    {
        uint result = 1;
        try
        {
            result += 10;
        }
        finally
        {
            result += 100;
        }
        result += 1000;

        return result;
    }

    private static void TestArgsWithMixedTypesAndExceptionRegions()
    {
        new MixedArgFuncClass().MixedArgFunc(1, null, 2, null);
    }

    class MixedArgFuncClass
    {
        public void MixedArgFunc(int firstInt, object shadowStackArg, int secondInt, object secondShadowStackArg)
        {
            PrintString("MixedParamFuncWithExceptionRegions does not overwrite args : ");
            bool ok = true;
            int p1 = firstInt;
            try // add a try/catch to get _exceptionRegions.Length > 0 and copy stack args to shadow stack
            {
                if (shadowStackArg != null)
                {
                    ok = false;
                }
            }
            catch (Exception)
            {
                throw;
            }
            if (p1 != 1)
            {
                PrintString("p1 not 1, was ");
                PrintLine(p1.ToString());
                ok = false;
            }

            if (secondInt != 2)
            {
                PrintString("secondInt not 2, was ");
                PrintLine(secondInt.ToString());
                ok = false;
            }
            if (secondShadowStackArg != null)
            {
                PrintLine("secondShadowStackArg != null");
                ok = false;
            }
            if (ok)
            {
                PrintLine("Ok.");
            }
            else
            {
                PrintLine("Failed.");
            }
        }
    }

    private static void TestThreadStaticsForSingleThread()
    {
        var firstClass = new ClassWithFourThreadStatics();
        int firstClassStatic = firstClass.GetStatic();
        PrintString("Static should be initialised: ");
        if (firstClassStatic == 2)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
            PrintLine("Was: " + firstClassStatic.ToString());
        }
        PrintString("Second class with same statics should be initialised: ");
        int secondClassStatic = new AnotherClassWithFourThreadStatics().GetStatic();
        if (secondClassStatic == 13)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
            PrintLine("Was: " + secondClassStatic.ToString());
        }

        PrintString("First class increment statics: ");
        firstClass.IncrementStatics();
        firstClassStatic = firstClass.GetStatic();
        if (firstClassStatic == 3)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
            PrintLine("Was: " + firstClassStatic.ToString());
        }

        PrintString("Second class should not be overwritten: "); // catches a type of bug where beacuse the 2 types share the same number and types of ThreadStatics, the first class can end up overwriting the second
        secondClassStatic = new AnotherClassWithFourThreadStatics().GetStatic();
        if (secondClassStatic == 13)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
            PrintLine("Was: " + secondClassStatic.ToString());
        }

        PrintString("First class 2nd instance should share static: ");
        int secondInstanceOfFirstClassStatic = new ClassWithFourThreadStatics().GetStatic();
        if (secondInstanceOfFirstClassStatic == 3)
        {
            PrintLine("Ok.");
        }
        else
        {
            PrintLine("Failed.");
            PrintLine("Was: " + secondInstanceOfFirstClassStatic.ToString());
        }
        Thread.Sleep(10);
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

public class Gen<T>
{
    internal Type TestTypeOf()
    {
        return typeof(T);
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

class ClassWithSealedVTable : ISomeItf
{
    public int GetValue()
    {
        return 37;
    }
}

interface ISomeItf
{
    int GetValue();
}

class ClassWithFourThreadStatics
{
    [ThreadStatic] static int classStatic;
    [ThreadStatic] static int classStatic2 = 2;
    [ThreadStatic] static int classStatic3;
    [ThreadStatic] static int classStatic4;
    [ThreadStatic] static int classStatic5;

    public int GetStatic()
    {
        return classStatic2;
    }

    public void IncrementStatics()
    {
        classStatic++;
        classStatic2++;
        classStatic3++;
        classStatic4++;
        classStatic5++;
    }
}

class AnotherClassWithFourThreadStatics
{
    [ThreadStatic] static int classStatic = 13;
    [ThreadStatic] static int classStatic2;
    [ThreadStatic] static int classStatic3;
    [ThreadStatic] static int classStatic4;
    [ThreadStatic] static int classStatic5;

    public int GetStatic()
    {
        return classStatic;
    }

    /// <summary>
    /// stops field unused compiler error, but never called
    /// </summary>
    public void IncrementStatics()
    {
        classStatic2++;
        classStatic3++;
        classStatic4++;
        classStatic5++;
    }
}

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Any method marked with NativeCallableAttribute can be directly called from
    /// native code. The function token can be loaded to a local variable using LDFTN
    /// and passed as a callback to native method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NativeCallableAttribute : Attribute
    {
        public NativeCallableAttribute()
        {
        }

        /// <summary>
        /// Optional. If omitted, compiler will choose one for you.
        /// </summary>
        public CallingConvention CallingConvention;

        /// <summary>
        /// Optional. If omitted, then the method is native callable, but no EAT is emitted.
        /// </summary>
        public string EntryPoint;
    }

    [AttributeUsage((System.AttributeTargets.Method | System.AttributeTargets.Class))]
    internal class McgIntrinsicsAttribute : Attribute
    {
    }
}
