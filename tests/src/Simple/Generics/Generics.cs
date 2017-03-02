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
        TestDelegateToCanonMethods.Run();
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
        TestSimpleGVMScenarios.Run();
        TestGvmDependencies.Run();

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
                MediumStruct result = f(x);
                if (result.X != x.X || result.Y != x.Y || result.Z != x.Z || result.W != x.W)
                    throw new Exception();
            }

            unsafe
            {
                Func<BigStruct, BigStruct> f = o.BigStructGeneric<object>;
                BigStruct x = new BigStruct();
                for (int i = 0; i < BigStruct.Length; i++)
                    x.Bytes[i] = (byte)(i * 2);

                BigStruct result = f(x);

                for (int i = 0; i < BigStruct.Length; i++)
                    if (x.Bytes[i] != result.Bytes[i])
                        throw new Exception();
            }
        }
    }

    class TestDelegateToCanonMethods
    {
        class Foo
        {
            public readonly int Value;
            public Foo(int value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString();
            }
        }

        class Bar
        {
            public readonly int Value;
            public Bar(int value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString();
            }
        }

        class GenClass<T>
        {
            public readonly T X;

            public GenClass(T x)
            {
                X = x;
            }

            public string MakeString()
            {
                // Use a constructed type that is not used elsewhere
                return typeof(T[,]).GetElementType().Name + ": " + X.ToString();
            }

            public string MakeGenString<U>()
            {
                // Use a constructed type that is not used elsewhere
                return typeof(T[,,]).GetElementType().Name + ", " + 
                    typeof(U[,,,]).GetElementType().Name + ": " + X.ToString();
            }
        }

        struct GenStruct<T>
        {
            public readonly T X;

            public GenStruct(T x)
            {
                X = x;
            }

            public string MakeString()
            {
                // Use a constructed type that is not used elsewhere
                return typeof(T[,]).GetElementType().Name + ": " + X.ToString();
            }

            public string MakeGenString<U>()
            {
                // Use a constructed type that is not used elsewhere
                return typeof(T[,,]).GetElementType().Name + ", " +
                    typeof(U[,,,]).GetElementType().Name + ": " + X.ToString();
            }
        }

        public static void Run()
        {
            // Delegate to a shared nongeneric reference type instance method
            {
                GenClass<Foo> g = new GenClass<Foo>(new Foo(42));
                Func<string> f = g.MakeString;
                if (f() != "Foo: 42")
                    throw new Exception();
            }

            // Delegate to a unshared nongeneric reference type instance method
            {
                GenClass<int> g = new GenClass<int>(85);
                Func<string> f = g.MakeString;
                if (f() != "Int32: 85")
                    throw new Exception();
            }

            // Delegate to a shared generic reference type instance method
            {
                GenClass<Foo> g = new GenClass<Foo>(new Foo(42));
                Func<string> f = g.MakeGenString<Foo>;
                if (f() != "Foo, Foo: 42")
                    throw new Exception();
            }

            // Delegate to a unshared generic reference type instance method
            {
                GenClass<int> g = new GenClass<int>(85);
                Func<string> f = g.MakeGenString<int>;
                if (f() != "Int32, Int32: 85")
                    throw new Exception();
            }

            // Delegate to a shared nongeneric value type instance method
            /*{
                GenStruct<Bar> g = new GenStruct<Bar>(new Bar(42));
                Func<string> f = g.MakeString;
                if (f() != "Bar: 42")
                    throw new Exception();
            }*/

            // Delegate to a unshared nongeneric value type instance method
            {
                GenStruct<int> g = new GenStruct<int>(85);
                Func<string> f = g.MakeString;
                if (f() != "Int32: 85")
                    throw new Exception();
            }

            // Delegate to a shared generic value type instance method
            /*{
                GenStruct<Bar> g = new GenStruct<Bar>(new Bar(42));
                Func<string> f = g.MakeGenString<Bar>;
                if (f() != "Bar, Bar: 42")
                    throw new Exception();
            }*/

            // Delegate to a unshared generic value type instance method
            {
                GenStruct<int> g = new GenStruct<int>(85);
                Func<string> f = g.MakeGenString<int>;
                if (f() != "Int32, Int32: 85")
                    throw new Exception();
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

        class NonGeneric
        {
            public static readonly string Message;

            static NonGeneric()
            {
                Message = "Hi there";
            }

            public static string Get<T>(object o)
            {
                if (o is T[])
                    return Message;
                return null;
            }
        }

        public static void Run()
        {
            if (Gen2<string>.GetFromClassParam() != "Hello")
                throw new Exception();

            if (Gen2<string>.GetFromMethodParam() != "World")
                throw new Exception();

            if (NonGeneric.Get<object>(new object[0]) != "Hi there")
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

    class TestSimpleGVMScenarios
    {
        interface IFoo<out U>
        {
            string IMethod1<T>(T t1, T t2);
        }

        class Base : IFoo<string>, IFoo<int>
        {
            public virtual string GMethod1<T>(T t1, T t2) { return "Base.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            public virtual string IMethod1<T>(T t1, T t2) { return "Base.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class Derived : Base, IFoo<string>, IFoo<int>
        {
            public override string GMethod1<T>(T t1, T t2) { return "Derived.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            string IFoo<string>.IMethod1<T>(T t1, T t2) { return "Derived.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class SuperDerived : Derived, IFoo<string>, IFoo<int>
        {
            string IFoo<int>.IMethod1<T>(T t1, T t2) { return "SuperDerived.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        

        class GenBase<A> : IFoo<string>, IFoo<int>
        {
            public virtual string GMethod1<T>(T t1, T t2) { return "GenBase<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            public virtual string IMethod1<T>(T t1, T t2) { return "GenBase<" + typeof(A) + ">.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class GenDerived<A> : GenBase<A>, IFoo<string>, IFoo<int>
        {
            public override string GMethod1<T>(T t1, T t2) { return "GenDerived<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            string IFoo<string>.IMethod1<T>(T t1, T t2) { return "GenDerived<" + typeof(A) + ">.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        class GenSuperDerived<A> : GenDerived<A>, IFoo<string>, IFoo<int>
        {
            string IFoo<int>.IMethod1<T>(T t1, T t2) { return "GenSuperDerived<" + typeof(A) + ">.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }

        struct MyStruct1 : IFoo<string>, IFoo<int>
        {
            string IFoo<string>.IMethod1<T>(T t1, T t2) { return "MyStruct1.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            string IFoo<int>.IMethod1<T>(T t1, T t2) { return "MyStruct1.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        struct MyStruct2 : IFoo<string>, IFoo<int>
        {
            string IFoo<string>.IMethod1<T>(T t1, T t2) { return "MyStruct2.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            public string IMethod1<T>(T t1, T t2) { return "MyStruct2.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }
        struct MyStruct3 : IFoo<string>, IFoo<int>
        {
            string IFoo<int>.IMethod1<T>(T t1, T t2) { return "MyStruct3.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
            public string IMethod1<T>(T t1, T t2) { return "MyStruct3.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
        }

        static string s_GMethod1;
        static string s_IFooString;
        static string s_IFooObject;
        static string s_IFooInt;

        static int s_NumErrors = 0;

        private static void TestWithStruct(IFoo<string> ifooStr, IFoo<object> ifooObj, IFoo<int> ifooInt)
        {
            var res = ifooStr.IMethod1<int>(1, 2);
            WriteLineWithVerification(res, s_IFooString);

            res = ifooObj.IMethod1<int>(3, 4);
            WriteLineWithVerification(res, s_IFooObject);

            res = ifooInt.IMethod1<int>(5, 6);
            WriteLineWithVerification(res, s_IFooInt);
        }

        private static void TestWithClass(object o)
        {
            Base b = o as Base;
            var res = b.GMethod1<int>(1, 2);
            WriteLineWithVerification(res, s_GMethod1);

            IFoo<string> ifoo1 = o as IFoo<string>;
            res = ifoo1.IMethod1<int>(3, 4);
            WriteLineWithVerification(res, s_IFooString);

            IFoo<object> ifoo2 = o as IFoo<object>;
            res = ifoo2.IMethod1<int>(5, 6);
            WriteLineWithVerification(res, s_IFooObject);

            IFoo<int> ifoo3 = o as IFoo<int>;
            res = ifoo3.IMethod1<int>(7, 8);
            WriteLineWithVerification(res, s_IFooInt);
        }

        private static void TestWithGenClass<T>(object o)
        {
            GenBase<T> b = o as GenBase<T>;
            var res = b.GMethod1<int>(1, 2);
            WriteLineWithVerification(res, s_GMethod1);

            IFoo<string> ifoo1 = o as IFoo<string>;
            res = ifoo1.IMethod1<int>(3, 4);
            WriteLineWithVerification(res, s_IFooString);

            IFoo<object> ifoo2 = o as IFoo<object>;
            res = ifoo2.IMethod1<int>(5, 6);
            WriteLineWithVerification(res, s_IFooObject);

            IFoo<int> ifoo3 = o as IFoo<int>;
            res = ifoo3.IMethod1<int>(7, 8);
            WriteLineWithVerification(res, s_IFooInt);
        }

        private static void WriteLineWithVerification(string actual, string expected)
        {
            if (actual != expected)
            {
                Console.WriteLine("ACTUAL   : " + actual);
                Console.WriteLine("EXPECTED : " + expected);
                s_NumErrors++;
            }
            else
            {
                Console.WriteLine(actual);
            }
        }

        public static void Run()
        {
            {
                s_GMethod1 = "Base.GMethod1<System.Int32>(1,2)";
                s_IFooString = "Base.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "Base.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "Base.IMethod1<System.Int32>(7,8)";
                TestWithClass(new Base());
                Console.WriteLine("====================");


                s_GMethod1 = "Derived.GMethod1<System.Int32>(1,2)";
                s_IFooString = "Derived.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "Derived.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "Base.IMethod1<System.Int32>(7,8)";
                TestWithClass(new Derived());
                Console.WriteLine("====================");


                s_GMethod1 = "Derived.GMethod1<System.Int32>(1,2)";
                s_IFooString = "Derived.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "Derived.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "SuperDerived.IFoo<int>.IMethod1<System.Int32>(7,8)";
                TestWithClass(new SuperDerived());
                Console.WriteLine("====================");
            }

            {
                s_GMethod1 = "GenBase<System.Byte>.GMethod1<System.Int32>(1,2)";
                s_IFooString = "GenBase<System.Byte>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "GenBase<System.Byte>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "GenBase<System.Byte>.IMethod1<System.Int32>(7,8)";
                TestWithGenClass<byte>(new GenBase<byte>());
                Console.WriteLine("====================");


                s_GMethod1 = "GenDerived<System.Byte>.GMethod1<System.Int32>(1,2)";
                s_IFooString = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "GenBase<System.Byte>.IMethod1<System.Int32>(7,8)";
                TestWithGenClass<byte>(new GenDerived<byte>());
                Console.WriteLine("====================");


                s_GMethod1 = "GenDerived<System.String>.GMethod1<System.Int32>(1,2)";
                s_IFooString = "GenDerived<System.String>.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "GenDerived<System.String>.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "GenBase<System.String>.IMethod1<System.Int32>(7,8)";
                TestWithGenClass<String>(new GenDerived<String>());
                Console.WriteLine("====================");


                s_GMethod1 = "GenDerived<System.Byte>.GMethod1<System.Int32>(1,2)";
                s_IFooString = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooObject = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(5,6)";
                s_IFooInt = "GenSuperDerived<System.Byte>.IFoo<int>.IMethod1<System.Int32>(7,8)";
                TestWithGenClass<byte>(new GenSuperDerived<byte>());
                Console.WriteLine("====================");
            }

            {
                s_IFooString = "MyStruct1.IFoo<string>.IMethod1<System.Int32>(1,2)";
                s_IFooObject = "MyStruct1.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooInt = "MyStruct1.IFoo<int>.IMethod1<System.Int32>(5,6)";
                TestWithStruct(new MyStruct1(), new MyStruct1(), new MyStruct1());
                Console.WriteLine("====================");


                s_IFooString = "MyStruct2.IFoo<string>.IMethod1<System.Int32>(1,2)";
                s_IFooObject = "MyStruct2.IFoo<string>.IMethod1<System.Int32>(3,4)";
                s_IFooInt = "MyStruct2.IMethod1<System.Int32>(5,6)";
                TestWithStruct(new MyStruct2(), new MyStruct2(), new MyStruct2());
                Console.WriteLine("====================");


                s_IFooString = "MyStruct3.IMethod1<System.Int32>(1,2)";
                s_IFooObject = "MyStruct3.IMethod1<System.Int32>(3,4)";
                s_IFooInt = "MyStruct3.IFoo<int>.IMethod1<System.Int32>(5,6)";
                TestWithStruct(new MyStruct3(), new MyStruct3(), new MyStruct3());
                Console.WriteLine("====================");
            }

            if (s_NumErrors != 0)
                throw new Exception();
        }
    }

    class TestGvmDependencies
    {
        class Atom { }

        class Foo
        {
            public virtual object Frob<T>()
            {
                return new T[0, 0];
            }
        }

        class Bar : Foo
        {
            public override object Frob<T>()
            {
                return new T[0, 0, 0];
            }
        }

        public static void Run()
        {
            {
                Foo x = new Foo();
                x.Frob<Atom>();
            }

            {
                Foo x = new Bar();
                x.Frob<Atom>();
            }
        }
    }
}
