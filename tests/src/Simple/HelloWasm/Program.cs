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

    internal static bool Success;
    private static unsafe int Main(string[] args)
    {
        Success = true;
        PrintLine("Starting");

        Add(1, 2);
        PrintLine("Hello from C#!");
        int tempInt = 0;
        int tempInt2 = 0;
        StartTest("Address/derefernce test");
        (*(&tempInt)) = 9;
        EndTest(tempInt == 9);

        int* targetAddr = (tempInt > 0) ? (&tempInt2) : (&tempInt);

        StartTest("basic block stack entry Test");
        (*targetAddr) = 1;
        EndTest(tempInt2 == 1 && tempInt == 9);

        StartTest("Inline assign byte Test");
        EndTest(ILHelpers.ILHelpersTest.InlineAssignByte() == 100);

        StartTest("dup test");
        int dupTestInt = 9;
        EndTest(ILHelpers.ILHelpersTest.DupTest(ref dupTestInt) == 209 && dupTestInt == 209);

        TestClass tempObj = new TestDerivedClass(1337);
        tempObj.TestMethod("Hello");
        tempObj.TestVirtualMethod("Hello");
        tempObj.TestVirtualMethod2("Hello");

        TwoByteStr str = new TwoByteStr() { first = 1, second = 2 };
        TwoByteStr str2 = new TwoByteStr() { first = 3, second = 4 };
        *(&str) = str2;
        str2 = *(&str);

        StartTest("value type int field test");
        EndTest(str2.second == 4);

        StartTest("static int field test");
        staticInt = 5;
        EndTest(staticInt == 5);

        StartTest("thread static int initial value field test");
        EndTest(threadStaticInt == 0);

        StartTest("thread static int field test");
        threadStaticInt = 9;
        EndTest(threadStaticInt == 9);

        StaticCtorTest();

        StartTest("box test");
        var boxedInt = (object)tempInt;
        if (((int)boxedInt) == 9)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Value:");
            PrintLine(boxedInt.ToString());
        }

        var boxedStruct = (object)new BoxStubTest { Value = "Boxed Stub Test: Ok." };
        PrintLine(boxedStruct.ToString());

        StartTest("Subtraction Test");
        int subResult = tempInt - 1;
        EndTest(subResult == 8);

        StartTest("Division Test");
        int divResult = tempInt / 3;
        EndTest(divResult == 3);

        StartTest("Addition of byte and short test");
        byte aByte = 2;
        short aShort = 0x100;
        short byteAndShortResult = (short)(aByte + aShort);
        EndTest(byteAndShortResult == 0x102);

        StartTest("not test");
        var not = Not(0xFFFFFFFF) == 0x00000000;
        EndTest(not);

        StartTest("negInt test");
        var negInt = Neg(42) == -42;
        EndTest(negInt);

        StartTest("shiftLeft test");
        var shiftLeft = ShiftLeft(1, 2) == 4;
        EndTest(shiftLeft);

        StartTest("shiftRight test");
        var shiftRight = ShiftRight(4, 2) == 1;
        EndTest(shiftRight);

        StartTest("unsignedShift test");
        var unsignedShift = UnsignedShift(0xFFFFFFFFu, 4) == 0x0FFFFFFFu;
        EndTest(unsignedShift);

        StartTest("shiftLeft byte to short test");
        byte byteConstant = (byte)0x80;
        ushort shiftedToShort = (ushort)(byteConstant << 1);
        EndTest((int)shiftedToShort == 0x0100);

        StartTest("SwitchOp0 test");
        var switchTest0 = SwitchOp(5, 5, 0);
        EndTest(switchTest0 == 10);

        StartTest("SwitchOp1 test");
        var switchTest1 = SwitchOp(5, 5, 1);
        EndTest(switchTest1 == 25);

        StartTest("SwitchOpDefault test");
        var switchTestDefault = SwitchOp(5, 5, 20);
        EndTest(switchTestDefault == 0);

#if PLATFORM_WINDOWS
        StartTest("CpObj test");
        var cpObjTestA = new TestValue { Field = 1234 };
        var cpObjTestB = new TestValue { Field = 5678 };
        CpObjTest.CpObj(ref cpObjTestB, ref cpObjTestA);
        EndTest (cpObjTestB.Field == 1234);
#endif

        StartTest("Static delegate test");
        Func<int> staticDelegate = StaticDelegateTarget;
        EndTest(staticDelegate() == 7);

        StartTest("Instance delegate test");
        tempObj.TestInt = 8;
        Func<int> instanceDelegate = tempObj.InstanceDelegateTarget;
        EndTest(instanceDelegate() == 8);

        StartTest("Virtual Delegate Test");
        Action virtualDelegate = tempObj.VirtualDelegateTarget;
        virtualDelegate();

        var arrayTest = new BoxStubTest[] { new BoxStubTest { Value = "Hello" }, new BoxStubTest { Value = "Array" }, new BoxStubTest { Value = "Test" } };
        foreach (var element in arrayTest)
            PrintLine(element.Value);

        arrayTest[1].Value = "Array load/store test: Ok.";
        PrintLine(arrayTest[1].Value);

        int ii = 0;
        arrayTest[ii++].Value = "dup ref test: Ok.";
        PrintLine(arrayTest[0].Value);

        StartTest("Large array load/store test");
        var largeArrayTest = new long[] { Int64.MaxValue, 0, Int64.MinValue, 0 };
        EndTest(largeArrayTest[0] == Int64.MaxValue &&
                largeArrayTest[1] == 0 &&
                largeArrayTest[2] == Int64.MinValue &&
                largeArrayTest[3] == 0);

        StartTest("Small array load/store test");
        var smallArrayTest = new long[] { Int16.MaxValue, 0, Int16.MinValue, 0 };
        EndTest(smallArrayTest[0] == Int16.MaxValue &&
                smallArrayTest[1] == 0 &&
                smallArrayTest[2] == Int16.MinValue &&
                smallArrayTest[3] == 0);

        StartTest("Newobj value type test");
        IntPtr returnedIntPtr = NewobjValueType();
        EndTest(returnedIntPtr.ToInt32() == 3);

        StackallocTest();

        IntToStringTest();

        CastingTestClass castingTest = new DerivedCastingTestClass1();

        PrintLine("interface call test: Ok " + (castingTest as ICastingTest1).GetValue().ToString());

        StartTest("Type casting with isinst & castclass to class test");
        EndTest(((DerivedCastingTestClass1)castingTest).GetValue() == 1 && !(castingTest is DerivedCastingTestClass2));

        StartTest("Type casting with isinst & castclass to interface test");
        // Instead of checking the result of `GetValue`, we use null check by now until interface dispatch is implemented.
        EndTest((ICastingTest1)castingTest != null && !(castingTest is ICastingTest2));

        StartTest("Type casting with isinst & castclass to array test");
        object arrayCastingTest = new BoxStubTest[] { new BoxStubTest { Value = "Array" }, new BoxStubTest { Value = "Cast" }, new BoxStubTest { Value = "Test" } };
        PrintLine(((BoxStubTest[])arrayCastingTest)[0].Value);
        PrintLine(((BoxStubTest[])arrayCastingTest)[1].Value);
        PrintLine(((BoxStubTest[])arrayCastingTest)[2].Value);
        EndTest(!(arrayCastingTest is CastingTestClass[]));

        ldindTest();

        InterfaceDispatchTest();

        StartTest("Runtime.Helpers array initialization test");
        var testRuntimeHelpersInitArray = new long[] { 1, 2, 3 };
        EndTest(testRuntimeHelpersInitArray[0] == 1 &&
                testRuntimeHelpersInitArray[1] == 2 &&
                testRuntimeHelpersInitArray[2] == 3);

        StartTest("Multi-dimension array instantiation test");
        var testMdArrayInstantiation = new int[2, 2];
        EndTest(testMdArrayInstantiation != null && testMdArrayInstantiation.GetLength(0) == 2 && testMdArrayInstantiation.GetLength(1) == 2);

        StartTest("Multi-dimension array get/set test");
        testMdArrayInstantiation[0, 0] = 1;
        testMdArrayInstantiation[0, 1] = 2;
        testMdArrayInstantiation[1, 0] = 3;
        testMdArrayInstantiation[1, 1] = 4;
        EndTest(testMdArrayInstantiation[0, 0] == 1 
                && testMdArrayInstantiation[0, 1] == 2
                && testMdArrayInstantiation[1, 0] == 3
                && testMdArrayInstantiation[1, 1] == 4);


        FloatDoubleTest();

        StartTest("long comparison");
        long l = 0x1;
        EndTest(l < 0x7FF0000000000000);

        // Create a ByReference<char> through the ReadOnlySpan ctor and call the ByReference.Value via the indexer.
        StartTest("ByReference intrinsics exercise via ReadOnlySpan");
        var span = "123".AsSpan();
        if (span[0] != '1'
            || span[1] != '2'
            || span[2] != '3')
        {
            FailTest();
            PrintLine(span[0].ToString());
            PrintLine(span[1].ToString());
            PrintLine(span[2].ToString());
        }
        else
        {
            PassTest();
        }

        TestConstrainedClassCalls();

        TestValueTypeElementIndexing();

        TestArrayItfDispatch();

        TestMetaData();

        TestTryFinally();

        StartTest("RVA static field test");
        int rvaFieldValue = ILHelpers.ILHelpersTest.StaticInitedInt;
        if (rvaFieldValue == 0x78563412)
        {
            PassTest();
        }
        else
        {
            FailTest(rvaFieldValue.ToString());
        }

        TestNativeCallback();

        TestArgsWithMixedTypesAndExceptionRegions();

        TestThreadStaticsForSingleThread();

        TestDispose();

        TestCallToGenericInterfaceMethod();

        TestInitObjDouble();

        // This test should remain last to get other results before stopping the debugger
        PrintLine("Debugger.Break() test: Ok if debugger is open and breaks.");
        System.Diagnostics.Debugger.Break();

        PrintLine("Done");
        return Success ? 100 : -1;
    }

    private static void StartTest(string testDescription)
    {
        PrintString(testDescription + ": ");
    }

    private static void EndTest(bool result, string failMessage = null)
    {
        if (result)
        {
            PassTest();
        }
        else
        {
            FailTest(failMessage);
        }
    }

    internal static void PassTest()
    {
        PrintLine("Ok.");
    }

    internal static void FailTest(string failMessage = null)
    {
        Success = false;
        PrintLine("Failed.");
        if (failMessage != null) PrintLine(failMessage + "-");
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
        StartTest("Stackalloc test");
        int* intSpan = stackalloc int[2];
        intSpan[0] = 3;
        intSpan[1] = 7;

        EndTest(intSpan[0] == 3 && intSpan[1] == 7);
    }

    private static void IntToStringTest()
    {
        StartTest("Int to String Test: Ok if says 42");
        string intString = 42.ToString();
        PrintLine(intString);
        EndTest(intString == "42");
    }

    private unsafe static void ldindTest()
    {
        StartTest("ldind test");
        var ldindTarget = new TwoByteStr { first = byte.MaxValue, second = byte.MinValue };
        var ldindField = &ldindTarget.first;
        if((*ldindField) == byte.MaxValue)
        {
            ldindTarget.second = byte.MaxValue;
            *ldindField = byte.MinValue;
            //ensure there isnt any overwrite of nearby fields
            if(ldindTarget.first == byte.MinValue && ldindTarget.second == byte.MaxValue)
            {
                PassTest();
            }
            else if(ldindTarget.first != byte.MinValue)
            {
                FailTest("didnt update target.");
            }
            else
            {
                FailTest("overwrote data");
            }
        }
        else
        {
            uint ldindFieldValue = *ldindField;
            FailTest(ldindFieldValue.ToString());
        }
    }

    private static void InterfaceDispatchTest()
    {
        StartTest("Struct interface test");
        ItfStruct itfStruct = new ItfStruct();
        EndTest(ItfCaller(itfStruct) == 4);

        ClassWithSealedVTable classWithSealedVTable = new ClassWithSealedVTable();
        StartTest("Interface dispatch with sealed vtable test");
        EndTest(CallItf(classWithSealedVTable) == 37);
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
            StartTest("BeforeFieldInit test");
            int x = BeforeFieldInitTest.TestField;
            EndTest(StaticsInited.BeforeFieldInitInited, "cctor not run");
        }

        StartTest("NonBeforeFieldInit test");
        NonBeforeFieldInitTest.Nop();
        EndTest(StaticsInited.NonBeforeFieldInitInited, "cctor not run");
    }

    private static void TestConstrainedClassCalls()
    {
        string s = "utf-8";

        StartTest("Direct ToString test");
        string stringDirectToString = s.ToString();
        if (s.Equals(stringDirectToString))
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintString("Returned string:\"");
            PrintString(stringDirectToString);
            PrintLine("\"");
        }
       
        // Generic calls on methods not defined on object
        uint dataFromBase = GenericGetData<MyBase>(new MyBase(11));
        StartTest("Generic call to base class test");
        EndTest(dataFromBase == 11);

        uint dataFromUnsealed = GenericGetData<UnsealedDerived>(new UnsealedDerived(13));
        StartTest("Generic call to unsealed derived class test");
        EndTest(dataFromUnsealed == 26);

        uint dataFromSealed = GenericGetData<SealedDerived>(new SealedDerived(15));
        StartTest("Generic call to sealed derived class test");
        EndTest(dataFromSealed == 45);

        uint dataFromUnsealedAsBase = GenericGetData<MyBase>(new UnsealedDerived(17));
        StartTest("Generic call to unsealed derived class as base test");
        EndTest(dataFromUnsealedAsBase == 34);

        uint dataFromSealedAsBase = GenericGetData<MyBase>(new SealedDerived(19));
        StartTest("Generic call to sealed derived class as base test");
        EndTest(dataFromSealedAsBase == 57);

        // Generic calls to methods defined on object
        uint hashCodeOfSealedViaGeneric = (uint)GenericGetHashCode<MySealedClass>(new MySealedClass(37));
        StartTest("Generic GetHashCode for sealed class test");
        EndTest(hashCodeOfSealedViaGeneric == 74);

        uint hashCodeOfUnsealedViaGeneric = (uint)GenericGetHashCode<MyUnsealedClass>(new MyUnsealedClass(41));
        StartTest("Generic GetHashCode for unsealed class test");
        EndTest(hashCodeOfUnsealedViaGeneric == 82);
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
        StartTest("Array interface dispatch test");
        EndTest(arrayItfDispatchTest.Count == 37,
            "Failed.  asm.js (WASM=1) known to fail due to alignment problem, although this problem sometimes means we don't even get this far and fails with an invalid function pointer.");
    }

    private static void TestValueTypeElementIndexing()
    {
        var chars = new[] { 'i', 'p', 's', 'u', 'm' };
        StartTest("Value type element indexing: ");
        EndTest(chars[0] == 'i' && chars[1] == 'p' && chars[2] == 's' && chars[3] == 'u' && chars[4] == 'm');
    }

    private static void FloatDoubleTest()
    {
        StartTest("(double) cast test");
        int intToCast = 1;
        double castedDouble = (double)intToCast;
        if (castedDouble == 1d)
        {
            PassTest();
        }
        else
        {
            var toInt = (int)castedDouble;
            //            PrintLine("expected 1m, but was " + castedDouble.ToString());  // double.ToString is not compiling at the time of writing, but this would be better output
            FailTest("Back to int on next line");
            PrintLine(toInt.ToString());
        }

        StartTest("different width float comparisons");
        EndTest(1f < 2d && 1d < 2f && 1f == 1d);

        StartTest("double precision comparison");
        // floats are 7 digits precision, so check some double more precise to make sure there is no loss occurring through some inadvertent cast to float
        EndTest(10.23456789d != 10.234567891d);

        StartTest("float comparison");
        EndTest(12.34567f == 12.34567f && 12.34567f != 12.34568f);

        StartTest("Test comparison of float constant");
        var maxFloat = Single.MaxValue;
        EndTest(maxFloat == Single.MaxValue);

        StartTest("Test comparison of double constant");
        var maxDouble = Double.MaxValue;
        EndTest(maxDouble == Double.MaxValue);
    }

    private static bool callbackResult;
    private static unsafe void TestNativeCallback()
    {
        StartTest("Native callback test");
        CallMe(123);
        EndTest(callbackResult);
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
        StartTest("type == null.  Simple class metadata test");
        var typeGetType = Type.GetType("System.Char, System.Private.CoreLib");
        if (typeGetType == null)
        {
            FailTest("type == null.  Simple class metadata test");
        }
        else
        {
            if (typeGetType.FullName != "System.Char")
            {
                FailTest("type != System.Char.  Simple class metadata test");
            }
            else PassTest();
        }

        StartTest("Simple struct metadata test (typeof(Char))");
        var typeofChar = typeof(Char);
        if (typeofChar == null)
        {
            FailTest("type == null.  Simple struct metadata test");
        }
        else
        {
            if (typeofChar.FullName != "System.Char")
            {
                FailTest("type != System.Char.  Simple struct metadata test");
            }
            else PassTest();
        }

        var gentT = new Gen<int>();
        var genParamType = gentT.TestTypeOf();
        StartTest("type of generic parameter");
        if (genParamType.FullName != "System.Int32")
        {
            FailTest("expected System.Int32 but was " + genParamType.FullName);
        }
        else
        {
            PassTest();
        }

        var arrayType = typeof(object[]);
        StartTest("type of array");
        if (arrayType.FullName != "System.Object[]")
        {
            FailTest("expected System.Object[] but was " + arrayType.FullName);
        }
        else
        {
            PassTest();
        }

        var genericType = typeof(List<object>);
        StartTest("type of generic");
        if (genericType.FullName != "System.Collections.Generic.List`1[[System.Object, System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a]]")
        {
            FailTest("expected System.Collections.Generic.List`1[[System.Object, System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a]] but was " + genericType.FullName);
        }
        else
        {
            PassTest();
        }

        StartTest("Type GetFields length");
        var x = new ClassForMetaTests();
        var s = x.StringField;  
        var i = x.IntField;
        var classForMetaTestsType = typeof(ClassForMetaTests);
        FieldInfo[] fields = classForMetaTestsType.GetFields();
        EndTest(fields.Length == 3);

        StartTest("Type get string field via reflection");
        var stringFieldInfo = classForMetaTestsType.GetField("StringField");
        EndTest((string)stringFieldInfo.GetValue(x) == s);

        StartTest("Type get int field via reflection");
        var intFieldInfo = classForMetaTestsType.GetField("IntField");
        EndTest((int)intFieldInfo.GetValue(x) == i);

        StartTest("Type get static int field via reflection");
        var staticIntFieldInfo = classForMetaTestsType.GetField("StaticIntField");
        EndTest((int)staticIntFieldInfo.GetValue(x) == 23);

        StartTest("Type set string field via reflection");
        stringFieldInfo.SetValue(x, "bcd");
        EndTest(x.StringField == "bcd");

        StartTest("Type set int field via reflection");
        intFieldInfo.SetValue(x, 456);
        EndTest(x.IntField == 456);

        StartTest("Type set static int field via reflection");
        staticIntFieldInfo.SetValue(x, 987);
        EndTest(ClassForMetaTests.StaticIntField == 987);

        var st = new StructForMetaTests();
        st.StringField = "xyz";
        var fieldStructType = typeof(StructForMetaTests);
        var structStringFieldInfo = fieldStructType.GetField("StringField");
        StartTest("Struct get string field via reflection");
        EndTest((string)structStringFieldInfo.GetValue(st) == "xyz");

        StartTest("Class get+invoke ctor via reflection");
        var ctor = classForMetaTestsType.GetConstructor(new Type[0]);
        ClassForMetaTests instance = (ClassForMetaTests)ctor.Invoke(null);
        EndTest(instance.IntField == 12);

        instance.ReturnTrueIf1(0); // force method output
        instance.ReturnTrueIf1AndThis(0, null); // force method output
        ClassForMetaTests.ReturnsParam(null); // force method output

        StartTest("Class get+invoke simple method via reflection");
        var mtd = classForMetaTestsType.GetMethod("ReturnTrueIf1");
        bool shouldBeTrue = (bool)mtd.Invoke(instance, new object[] {1});
        bool shouldBeFalse = (bool)mtd.Invoke(instance, new object[] {2});
        EndTest(shouldBeTrue && !shouldBeFalse);

        StartTest("Class get+invoke method with ref param via reflection");
        var mtdWith2Params = classForMetaTestsType.GetMethod("ReturnTrueIf1AndThis");
        shouldBeTrue = (bool)mtdWith2Params.Invoke(instance, new object[] { 1, instance });
        shouldBeFalse = (bool)mtdWith2Params.Invoke(instance, new object[] { 1, new ClassForMetaTests() });
        EndTest(shouldBeTrue && !shouldBeFalse);

        StartTest("Class get+invoke static method with ref param via reflection");
        var staticMtd = classForMetaTestsType.GetMethod("ReturnsParam");
        var retVal = (ClassForMetaTests)staticMtd.Invoke(null, new object[] { instance });
        EndTest(Object.ReferenceEquals(retVal, instance));
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

        public bool ReturnTrueIf1(int i)
        {
            return i == 1;
        }

        public bool ReturnTrueIf1AndThis(int i, object anInstance)
        {
            return i == 1 && object.ReferenceEquals(this, anInstance);
        }

        public static object ReturnsParam(object p1)
        {
            return p1;
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
        StartTest("Try/Finally test");
        uint result = TryFinallyInner();
        if (result == 1111)
        {
            PassTest();
        }
        else
        {
            FailTest("Result: " + result.ToString());
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

    private static void TestCallToGenericInterfaceMethod()
    {
        StartTest("Call generic method on interface test");

        TestGenItf implInt = new TestGenItf();
        implInt.Log<object>(new object());
        EndTest(true);
    }

    public interface ITestGenItf
    {
        bool Log<TState>(TState state);
    }

    public class TestGenItf : ITestGenItf
    {
        public bool Log<TState>(TState state)
        {
            return true;
        }
    }

    private static void TestArgsWithMixedTypesAndExceptionRegions()
    {
        new MixedArgFuncClass().MixedArgFunc(1, null, 2, null);
    }

    class MixedArgFuncClass
    {
        public void MixedArgFunc(int firstInt, object shadowStackArg, int secondInt, object secondShadowStackArg)
        {
            Program.StartTest("MixedParamFuncWithExceptionRegions does not overwrite args");
            bool ok = true;
            int p1 = firstInt;
            try // add a try/catch to get _exceptionRegions.Length > 0 and copy stack args to shadow stack
            {
                if (shadowStackArg != null)
                {
                    FailTest("shadowStackArg != null");
                    ok = false;
                }
            }
            catch (Exception)
            {
                throw;
            }
            if (p1 != 1)
            {
                FailTest("p1 not 1, was ");
                PrintLine(p1.ToString());
                ok = false;
            }

            if (secondInt != 2)
            {
                FailTest("secondInt not 2, was ");
                PrintLine(secondInt.ToString());
                ok = false;
            }
            if (secondShadowStackArg != null)
            {
                FailTest("secondShadowStackArg != null");
                ok = false;
            }
            if (ok)
            {
                PassTest();
            }
        }
    }

    private static void TestThreadStaticsForSingleThread()
    {
        var firstClass = new ClassWithFourThreadStatics();
        int firstClassStatic = firstClass.GetStatic();
        StartTest("Static should be initialised");
        if (firstClassStatic == 2)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + firstClassStatic.ToString());
        }
        StartTest("Second class with same statics should be initialised");
        int secondClassStatic = new AnotherClassWithFourThreadStatics().GetStatic();
        if (secondClassStatic == 13)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + secondClassStatic.ToString());
        }

        StartTest("First class increment statics");
        firstClass.IncrementStatics();
        firstClassStatic = firstClass.GetStatic();
        if (firstClassStatic == 3)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + firstClassStatic.ToString());
        }

        StartTest("Second class should not be overwritten"); // catches a type of bug where beacuse the 2 types share the same number and types of ThreadStatics, the first class can end up overwriting the second
        secondClassStatic = new AnotherClassWithFourThreadStatics().GetStatic();
        if (secondClassStatic == 13)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + secondClassStatic.ToString());
        }

        StartTest("First class 2nd instance should share static");
        int secondInstanceOfFirstClassStatic = new ClassWithFourThreadStatics().GetStatic();
        if (secondInstanceOfFirstClassStatic == 3)
        {
            PassTest();
        }
        else
        {
            FailTest();
            PrintLine("Was: " + secondInstanceOfFirstClassStatic.ToString());
        }
        Thread.Sleep(10);
    }

    private static void TestDispose()
    {
        StartTest("using calls Dispose");
        var disposable = new DisposableTest();
        using (disposable)
        {
        }
        EndTest(disposable.Disposed);
    }

    private static void TestInitObjDouble()
    {
        StartTest("Init struct with double field test");
        StructWithDouble strt = new StructWithDouble();
        EndTest(strt.DoubleField == 0d);
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
        Program.FailTest("Virtual delegate incorrectly dispatched to base.");
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
        Program.PassTest();
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

public struct StructWithDouble
{
    public double DoubleField;
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

class DisposableTest : IDisposable
{
    public bool Disposed;

    public void Dispose()
    {
        Disposed = true;
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
