// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class BringUpTest
{
    const int Pass = 100;
    const int Fail = -1;

    public static int Main()
    {
        if (TestInterfaceCache() == Fail)
            return Fail;

        if (TestMultipleInterfaces() == Fail)
            return Fail;

        if (TestArrayInterfaces() == Fail)
            return Fail;

        if (TestVariantInterfaces() == Fail)
            return Fail;

        if (TestSpecialArrayInterfaces() == Fail)
            return Fail;

        if (TestIterfaceCallOptimization() == Fail)
            return Fail;

        return Pass;
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

    #region Implicit Interface Test

    private static int TestMultipleInterfaces()
    {
        TestClass<int> testInt = new TestClass<int>(5);

        MyInterface myInterface = testInt as MyInterface;
        if (!myInterface.GetAString().Equals("TestClass"))
        {
            Console.Write("On type TestClass, MyInterface.GetAString() returned ");
            Console.Write(myInterface.GetAString());
            Console.WriteLine(" Expected: TestClass");
            return Fail;
        }


        if (myInterface.GetAnInt() != 1)
        {
            Console.Write("On type TestClass, MyInterface.GetAnInt() returned ");
            Console.Write(myInterface.GetAnInt());
            Console.WriteLine(" Expected: 1");
            return Fail;
        }

        Interface<int> itf = testInt as Interface<int>;
        if (itf.GetT() != 5)
        {
            Console.Write("On type TestClass, Interface<int>::GetT() returned ");
            Console.Write(itf.GetT());
            Console.WriteLine(" Expected: 5");
            return Fail;
        }

        return Pass;
    }

    interface Interface<T>
    {
        T GetT();
    }

    class TestClass<T> : MyInterface, Interface<T>
    {
        T _t;
        public TestClass(T t)
        {
            _t = t;
        }

        public T GetT()
        {
            return _t;
        }

        public int GetAnInt()
        {
            return 1;
        }

        public string GetAString()
        {
            return "TestClass";
        }
    }
    #endregion

    #region Array Interfaces Test
    private static int TestArrayInterfaces()
    {
        {
            object stringArray = new string[] { "A", "B", "C", "D" };

            Console.WriteLine("Testing IEnumerable<T> on array...");
            string result = String.Empty;
            foreach (var s in (System.Collections.Generic.IEnumerable<string>)stringArray)
                result += s;

            if (result != "ABCD")
            {
                Console.WriteLine("Failed.");
                return Fail;
            }
        }

        {
            object stringArray = new string[] { "A", "B", "C", "D" };

            Console.WriteLine("Testing IEnumerable on array...");
            string result = String.Empty;
            foreach (var s in (System.Collections.IEnumerable)stringArray)
                result += s;

            if (result != "ABCD")
            {
                Console.WriteLine("Failed.");
                return Fail;
            }
        }

        {
            object intArray = new int[5, 5];

            Console.WriteLine("Testing IList on MDArray...");
            if (((System.Collections.IList)intArray).Count != 25)
            {
                Console.WriteLine("Failed.");
                return Fail;
            }
        }

        return Pass;
    }
    #endregion

    #region Variant interface tests

    interface IContravariantInterface<in T>
    {
        string DoContravariant(T value);
    }

    interface ICovariantInterface<out T>
    {
        T DoCovariant(object value);
    }

    class TypeWithVariantInterfaces<T> : IContravariantInterface<T>, ICovariantInterface<T>
    {
        public string DoContravariant(T value)
        {
            return value.ToString();
        }

        public T DoCovariant(object value)
        {
            return value is T ? (T)value : default(T);
        }
    }

    static IContravariantInterface<string> s_contravariantObject = new TypeWithVariantInterfaces<object>();
    static ICovariantInterface<object> s_covariantObject = new TypeWithVariantInterfaces<string>();
    static IEnumerable<int> s_arrayCovariantObject = (IEnumerable<int>)(object)new uint[] { 5, 10, 15 };

    private static int TestVariantInterfaces()
    {
        if (s_contravariantObject.DoContravariant("Hello") != "Hello")
            return Fail;

        if (s_covariantObject.DoCovariant("World") as string != "World")
            return Fail;

        int sum = 0;
        foreach (var e in s_arrayCovariantObject)
            sum += e;

        if (sum != 30)
            return Fail;

        return Pass;
    }

    class SpecialArrayBase { }
    class SpecialArrayDerived : SpecialArrayBase { }

    // NOTE: ICollection is not a variant interface, but arrays can cast with it as if it was
    static ICollection<SpecialArrayBase> s_specialDerived = new SpecialArrayDerived[42];
    static ICollection<uint> s_specialInt = (ICollection<uint>)(object)new int[85];

    private static int TestSpecialArrayInterfaces()
    {
        if (s_specialDerived.Count != 42)
            return Fail;

        if (s_specialInt.Count != 85)
            return Fail;

        return Pass;
    }

    #endregion

    #region Interface call optimization tests

    public interface ISomeInterface
    {
        int SomeValue { get; }
    }

    public abstract class SomeAbstractBaseClass : ISomeInterface
    {
        public abstract int SomeValue { get; }
    }

    public class SomeClass : SomeAbstractBaseClass
    {
        public override int SomeValue
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get { return 14; }
        }
    }

    private static int TestIterfaceCallOptimization()
    {
        ISomeInterface test = new SomeClass();
        int v = test.SomeValue;
        return (v == 14) ? Pass : Fail;
    }

    #endregion
}
