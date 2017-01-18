// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        TestDelegateVirtualMethod.Run();
        TestDelegateInterfaceMethod.Run();
        TestThreadStaticFieldAccess.Run();
        TestConstrainedMethodCalls.Run();
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
        T Generic<T>(object o) where T : class
        {
            Func<object, T> f = OtherGeneric<T>;
            return f(o);
        }

        T OtherGeneric<T>(object o) where T : class
        {
            return o as T;
        }

        public static void Run()
        {
            string hw = "Hello World";
            string roundtrip = new TestDelegateFatFunctionPointers().Generic<string>(hw);
            if (roundtrip != hw)
                throw new Exception();
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
