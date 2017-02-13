// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

class Program
{
    static int Main()
    {
        TestDictionaryDependencyTracking.Run();
        TestStaticBaseLookups.Run();
        TestInitThisClass.Run();
        TestDelegateFatFunctionPointers.Run();
        TestVirtualMethodUseTracking.Run();
        TestSlotsInHierarchy.Run();
        TestReflectionInvoke.Run();
        TestDelegateVirtualMethod.Run();
        TestDelegateInterfaceMethod.Run();
        TestThreadStaticFieldAccess.Run();
        TestConstrainedMethodCalls.Run();
        TestInstantiatingUnboxingStubs.Run();
        TestMDArrayAddressMethod.Run();
        TestNameManglingCollisionRegression.Run();
        TestUnusedGVMsDoNotCrashCompiler.Run();

        return 100;
    }

    /// <summary>
    /// Tests that we properly track dictionary dependencies of generic methods.
    /// (Getting this wrong is a linker failure.)
    /// </summary>
    class TestDictionaryDependencyTracking
    {
        static object Gen1<T>()
        {
            return MakeArray<ClassGen<T>>();
        }

        static object MakeArray<T>()
        {
            return new T[0];
        }

        class Gen<T>
        {
            public object Frob()
            {
                return new ValueGen<T[]>();
            }

            public object Futz()
            {
                return Gen1<ValueGen<T>>();
            }
        }

        struct ValueGen<T>
        {
        }

        class ClassGen<T>
        {
        }

        public static void Run()
        {
            new Gen<string>().Frob();
            new Gen<object>().Futz();
        }
    }

    /// <summary>
    /// Tests static base access.
    /// </summary>
    class TestStaticBaseLookups
    {
        class C1 { }
        class C2 { }
        class C3 { }

        class GenHolder<T>
        {
            public static int IntField;
            public static string StringField;
        }

        class GenAccessor<T>
        {
            public static string Read()
            {
                return GenHolder<T>.IntField.ToString() + GenHolder<T>.StringField;
            }

            public static void SetSimple(int i, string s)
            {
                GenHolder<T>.IntField = i;
                GenHolder<T>.StringField = s;
            }

            public static void SetComplex<U>(int i, string s)
            {
                GenHolder<T>.IntField = i;
                GenHolder<T>.StringField = s;
                GenHolder<U>.IntField = i + 1;
                GenHolder<U>.StringField = s + "`";
            }
        }

        public static void Run()
        {
            GenAccessor<C1>.SetComplex<C2>(42, "Hello");
            GenAccessor<C3>.SetSimple(85, "World");

            if (GenAccessor<C1>.Read() != "42Hello")
                throw new Exception();

            if (GenHolder<C2>.IntField != 43 || GenHolder<C2>.StringField != "Hello`")
                throw new Exception();

            if (GenAccessor<C3>.Read() != "85World")
                throw new Exception();
        }
    }

    /// <summary>
    /// Tests that we can use a delegate that points to a generic method.
    /// </summary>
    class TestDelegateFatFunctionPointers
    {
        struct SmallStruct
        {
            public int X;
        }

        struct MediumStruct
        {
            public int X, Y, Z, W;
        }

        unsafe struct BigStruct
        {
            public const int Length = 128;
            public fixed byte Bytes[Length];
        }

        T Generic<T>(object o) where T : class
        {
            Func<object, T> f = OtherGeneric<T>;
            return f(o);
        }

        T OtherGeneric<T>(object o) where T : class
        {
            return o as T;
        }

        delegate void VoidGenericDelegate<T>(ref T x, T val);
        void VoidGeneric<T>(ref T x, T val)
        {
            x = val;
        }

        SmallStruct SmallStructGeneric<T>(SmallStruct x)
        {
            return x;
        }

        MediumStruct MediumStructGeneric<T>(MediumStruct x)
        {
            return x;
        }

        BigStruct BigStructGeneric<T>(BigStruct x)
        {
            return x;
        }

        public static void Run()
        {
            var o = new TestDelegateFatFunctionPointers();

            string hw = "Hello World";
            string roundtrip = o.Generic<string>(hw);
            if (roundtrip != hw)
                throw new Exception();

            {
                VoidGenericDelegate<object> f = o.VoidGeneric;
                object obj = new object();
                object location = null;
                f(ref location, obj);
                if (location != obj)
                    throw new Exception();
            }

            {
                Func<SmallStruct, SmallStruct> f = o.SmallStructGeneric<object>;
                SmallStruct x = new SmallStruct { X = 12345 };
                SmallStruct result = f(x);
                if (result.X != x.X)
                    throw new Exception();
            }

            {
                Func<MediumStruct, MediumStruct> f = o.MediumStructGeneric<object>;
                MediumStruct x = new MediumStruct { X = 12, Y = 34, Z = 56, W = 78 };
                /*MediumStruct result = f(x);
                if (result.X != x.X || result.Y != x.Y || result.Z != x.Z || result.W != x.W)
                    throw new Exception();*/
            }

            unsafe
            {
                Func<BigStruct, BigStruct> f = o.BigStructGeneric<object>;
                BigStruct x = new BigStruct();
                for (int i = 0; i < BigStruct.Length; i++)
                    x.Bytes[i] = (byte)(i * 2);

                /*BigStruct result = f(x);

                for (int i = 0; i < BigStruct.Length; i++)
                    if (x.Bytes[i] != result.Bytes[i])
                        throw new Exception();*/
            }
        }
    }

    class TestDelegateVirtualMethod
    {
        static void Generic<T>()
        {
            Base<T> o = new Derived<T>();
            Func<string> f = o.Do;
            if (f() != "Derived")
                throw new Exception();

            o = new Base<T>();
            f = o.Do;
            if (f() != "Base")
                throw new Exception();
        }

        public static void Run()
        {
            Generic<string>();
        }

        class Base<T>
        {
            public virtual string Do() => "Base";
        }

        class Derived<T> : Base<T>
        {
            public override string Do() => "Derived";
        }
    }

    class TestDelegateInterfaceMethod
    {
        static void Generic<T>()
        {
            IFoo<T> o = new Foo<T>();
            Func<string> f = o.Do;
            if (f() != "Foo")
                throw new Exception();
        }

        public static void Run()
        {
            Generic<string>();
        }

        interface IFoo<T>
        {
            string Do();
        }

        class Foo<T> : IFoo<T>
        {
            public string Do() => "Foo";
        }
    }

    /// <summary>
    /// Tests RyuJIT's initThisClass.
    /// </summary>
    class TestInitThisClass
    {
        class Gen1<T> where T : class
        {
            static string s_str1;
            static string s_str2;

            static Gen1()
            {
                s_str1 = ("Hello" as T) as string;
                s_str2 = ("World" as T) as string;
            }

            public static string Get1()
            {
                return (s_str1 as T) as string;
            }

            public static string Get2<U>()
            {
                return (s_str2 as T) as string;
            }
        }

        class Gen2<T> where T : class
        {
            public static string GetFromClassParam()
            {
                return (Gen1<T>.Get1() as T) as string;
            }

            public static string GetFromMethodParam()
            {
                return (Gen1<T>.Get2<T>() as T) as string;
            }
        }

        public static void Run()
        {
            if (Gen2<string>.GetFromClassParam() != "Hello")
                throw new Exception();

            if (Gen2<string>.GetFromMethodParam() != "World")
                throw new Exception();
        }
    }

    /// <summary>
    /// Tests that lazily built vtables for canonically equivalent types have the same shape.
    /// </summary>
    class TestVirtualMethodUseTracking
    {
        class C1 { }
        class C2 { }

        class Base<T> where T : class
        {
            public virtual T As(object o)
            {
                return o as T;
            }
        }

        class Derived<T> : Base<T> where T : class
        {
            public T AsToo(object o)
            {
                return o as T;
            }
        }

        public static void Run()
        {
            C1 c1 = new C1();
            if (new Derived<C1>().As(c1) != c1)
                throw new Exception();

            C2 c2 = new C2();
            if (new Derived<C2>().AsToo(c2) != c2)
                throw new Exception();
        }
    }

    /// <summary>
    /// Makes sure that during the base slot computation for types such as
    /// Derived&lt;__Canon&gt; (where the base type ends up being Base&lt;__Canon, string&gt;),
    /// the lazy vtable slot computation works.
    /// </summary>
    class TestSlotsInHierarchy
    {
        class Base<T, U>
        {
            public virtual int Do()
            {
                return 42;
            }
        }

        class Derived<T> : Base<T, string> where T : class
        {
            public T Cast(object v)
            {
                return v as T;
            }
        }

        public static void Run()
        {
            var derived = new Derived<string>();
            var derivedAsBase = (Base<string, string>)derived;

            if (derivedAsBase.Do() != 42)
                throw new Exception();

            if (derived.Cast("Hello") != "Hello")
                throw new Exception();
        }
    }

    class TestReflectionInvoke
    {
        struct Foo<T>
        {
            public int Value;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public bool SetAndCheck<U>(int value, U check)
            {
                Value = value;
                return check != null && typeof(T) == typeof(U);
            }
        }

        public static void Run()
        {
            if (String.Empty.Length > 0)
            {
                // Make sure we compile this method body.
                var tmp = new Foo<string>();
                tmp.SetAndCheck<string>(0, null);
            }

            object o = new Foo<string>();

            {
                MethodInfo mi = typeof(Foo<string>).GetTypeInfo().GetDeclaredMethod("SetAndCheck").MakeGenericMethod(typeof(string));
                if (!(bool)mi.Invoke(o, new object[] { 123, "hello" }))
                    throw new Exception();

                var foo = (Foo<string>)o;
                if (foo.Value != 123)
                    throw new Exception();

                if ((bool)mi.Invoke(o, new object[] { 123, null }))
                    throw new Exception();
            }

            // Uncomment when we have the type loader to buld invoke stub dictionaries.
            /*{
                MethodInfo mi = typeof(Foo<string>).GetTypeInfo().GetDeclaredMethod("SetAndCheck").MakeGenericMethod(typeof(object));
                if ((bool)mi.Invoke(o, new object[] { 123, new object() }))
                    throw new Exception();
            }*/
        }
    }

    class TestThreadStaticFieldAccess
    {
        class TypeWithThreadStaticField<T>
        {
            [ThreadStatic]
            public static int X;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static int Read()
            {
                return X;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Write(int x)
            {
                X = x;
            }
        }

        class BeforeFieldInitType<T>
        {
            [ThreadStatic]
            public static int X = 1985;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadFromBeforeFieldInitType<T>()
        {
            return BeforeFieldInitType<T>.X;
        }

        public static void Run()
        {
            // This will set the field to a value from non-shared code
            TypeWithThreadStaticField<object>.X = 42;

            // Now read the value from shared code
            if (TypeWithThreadStaticField<object>.Read() != 42)
                throw new Exception();

            // Set the value from shared code
            TypeWithThreadStaticField<string>.Write(112);

            // Now read the value from non-shared code
            if (TypeWithThreadStaticField<string>.X != 112)
                throw new Exception();

            // Check that the storage locations for string and object instantiations differ
            if (TypeWithThreadStaticField<object>.Read() != 42)
                throw new Exception();

            // Make sure we run the cctor
            if (ReadFromBeforeFieldInitType<object>() != 1985)
                throw new Exception();
        }
    }

    class TestConstrainedMethodCalls
    {
        interface IFoo<T>
        {
            void Frob();
        }

        struct Foo<T> : IFoo<T>
        {
            public int FrobbedValue;

            public void Frob()
            {
                FrobbedValue = 12345;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DoFrob<T, U>(ref T t) where T : IFoo<U>
        {
            // Perform a constrained interface call from shared code.
            // This should have been resolved to a direct call at compile time.
            t.Frob();
        }

        public static void Run()
        {
            var foo = new Foo<object>();
            DoFrob<Foo<object>, object>(ref foo);

            // If the FrobbedValue doesn't change when we frob, we must have done box+interface call.
            if (foo.FrobbedValue != 12345)
                throw new Exception();
        }
    }

    class TestInstantiatingUnboxingStubs
    {
        static volatile IFoo s_foo;

        interface IFoo
        {
            bool IsInst(object o);

            void Set(int value);
        }

        struct Foo<T> : IFoo
        {
            public int Value;

            public bool IsInst(object o)
            {
                return o is T;
            }

            public void Set(int value)
            {
                Value = value;
            }
        }

        public static void Run()
        {
            s_foo = new Foo<string>();

            // Make sure the instantiation argument is properly passed
            if (!s_foo.IsInst("ab"))
                throw new Exception();

            if (s_foo.IsInst(new object()))
                throw new Exception();

            // Make sure the byref to 'this' is properly passed
            s_foo.Set(42);

            var foo = (Foo<string>)s_foo;
            if (foo.Value != 42)
                throw new Exception();
        }
    }

    class TestMDArrayAddressMethod
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PassByRef(ref object x)
        {
            x = new Object();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoGen<T>(object[,] arr)
        {
            // Here, the array type is known statically at the time of compilation
            PassByRef(ref arr[0, 0]);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PassByRef2<T>(ref T x)
        {
            x = default(T);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoGen2<T>(T[,] arr)
        {
            // Here, the array type needs to be looked up from the dictionary
            PassByRef2<T>(ref arr[0, 0]);
        }

        public static void Run()
        {
            int exceptionsSeen = 0;

            try
            {
                DoGen<object>(new string[1, 1]);
            }
            catch (ArrayTypeMismatchException)
            {
                exceptionsSeen++;
            }

            DoGen<object>(new object[1, 1]);

            try
            {
                DoGen2<object>(new string[1, 1]);
            }
            catch (ArrayTypeMismatchException)
            {
                exceptionsSeen++;
            }

            DoGen2<object>(new object[1, 1]);

            if (exceptionsSeen != 2)
                throw new Exception();
        }
    }

    //
    // Regression test for issue https://github.com/dotnet/corert/issues/1964
    //
    class TestNameManglingCollisionRegression
    {
        class Gen1<T>
        {
            public Gen1(T t) {}
        }

        public static void Run()
        {
            Gen1<object[]>[] g1 = new Gen1<object[]>[1];
            g1[0] = new Gen1<object[]>(new object[] {new object[1]});

            Gen1<object[][]> g2 = new Gen1<object[][]>(new object[1][]);
        }
    }

    class TestUnusedGVMsDoNotCrashCompiler
    {
        interface GvmItf
        {
            T Bar<T>(T t);
        }

        class HasGvm : GvmItf
        {
            public virtual T Foo<T>(T t)
            {
                return t;
            }

            public virtual T Bar<T>(T t)
            {
                return t;
            }

            public virtual string DoubleString(string s)
            {
                return s + s;
            }
        }

        public static void Run()
        {
            HasGvm hasGvm = new HasGvm();
            if (hasGvm.DoubleString("Hello") != "HelloHello")
                throw new Exception();
        }
    }
}
