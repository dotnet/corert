// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if MULTIMODULE_BUILD && !DEBUG
// Some tests won't work if we're using optimizing codegen, but scanner doesn't run.
// This currently happens in optimized multi-obj builds.
#define OPTIMIZED_MODE_WITHOUT_SCANNER
#endif

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;

[assembly: TestAssembly]
[module: TestModule]

internal class ReflectionTest
{
    private static int Main()
    {
        // Things I would like to test, but we don't fully support yet:
        // * Interface method is reflectable if we statically called it through a constrained call
        // * Delegate Invoke method is reflectable if we statically called it

        //
        // Tests for dependency graph in the compiler
        //
#if !OPTIMIZED_MODE_WITHOUT_SCANNER
        TestContainment.Run();
        TestInterfaceMethod.Run();
        TestByRefLikeTypeMethod.Run();
#endif
        TestAttributeInheritance.Run();
        TestStringConstructor.Run();
        TestAssemblyAndModuleAttributes.Run();
        TestAttributeExpressions.Run();
        TestParameterAttributes.Run();

        //
        // Mostly functionality tests
        //
        TestCreateDelegate.Run();
        TestInstanceFields.Run();
        TestReflectionInvoke.Run();
        TestByRefReturnInvoke.Run();

        return 100;
    }

    class TestReflectionInvoke
    {
        internal class InvokeTests
        {
            private string _world = "world";

            public InvokeTests() { }

            public InvokeTests(string message) { _world = message; }

#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
            public static string GetHello(string name)
            {
                return "Hello " + name;
            }

#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
            public static void GetHelloByRef(string name, out string result)
            {
                result = "Hello " + name;
            }

#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
            public static string GetHelloGeneric<T>(T obj)
            {
                return "Hello " + obj;
            }


#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
            public string GetHelloInstance()
            {
                return "Hello " + _world;
            }

#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
            public static unsafe string GetHelloPointer(char* ptr)
            {
                return "Hello " + unchecked((int)ptr);
            }

#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
            public static unsafe string GetHelloPointerToo(char** ptr)
            {
                return "Hello " + unchecked((int)ptr);
            }

#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
            public static unsafe bool* GetPointer(void* ptr, object dummyJustToMakeThisUseSharedThunk)
            {
                return (bool*)ptr;
            }

        }

        public static unsafe void Run()
        {
            Console.WriteLine(nameof(TestReflectionInvoke));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                new InvokeTests().ToString();
                InvokeTests.GetHello(null);
                InvokeTests.GetHelloGeneric<int>(0);
                InvokeTests.GetHelloGeneric<double>(0);
                InvokeTests.GetHelloPointer(null);
                InvokeTests.GetHelloPointerToo(null);
                InvokeTests.GetPointer(null, null);
                string unused;
                InvokeTests.GetHelloByRef(null, out unused);
                unused.ToString();
            }

            {
                MethodInfo helloMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetHello");
                string result = (string)helloMethod.Invoke(null, new object[] { "world" });
                if (result != "Hello world")
                    throw new Exception();
            }

            {
                MethodInfo helloGenericMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetHelloGeneric").MakeGenericMethod(typeof(int));
                string result = (string)helloGenericMethod.Invoke(null, new object[] { 12345 });
                if (result != "Hello 12345")
                    throw new Exception();
            }

            {
                MethodInfo helloByRefMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetHelloByRef");
                object[] args = new object[] { "world", null };
                helloByRefMethod.Invoke(null, args);
                if ((string)args[1] != "Hello world")
                    throw new Exception();
            }

            {
                MethodInfo helloPointerMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetHelloPointer");
                string resultNull = (string)helloPointerMethod.Invoke(null, new object[] { null });
                if (resultNull != "Hello 0")
                    throw new Exception();

                string resultVal = (string)helloPointerMethod.Invoke(null, new object[] { Pointer.Box((void*)42, typeof(char*)) });
                if (resultVal != "Hello 42")
                    throw new Exception();
            }

            {
                MethodInfo helloPointerTooMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetHelloPointerToo");
                string result = (string)helloPointerTooMethod.Invoke(null, new object[] { Pointer.Box((void*)85, typeof(char**)) });
                if (result != "Hello 85")
                    throw new Exception();
            }

            {
                MethodInfo getPointerMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetPointer");
                object result = getPointerMethod.Invoke(null, new object[] { Pointer.Box((void*)2018, typeof(void*)), null });
                if (Pointer.Unbox(result) != (void*)2018)
                    throw new Exception();
            }
        }
    }

    class TestInstanceFields
    {
        public class FieldInvokeSample
        {
            public String InstanceField;
        }

        public static void Run()
        {
            Console.WriteLine(nameof(TestInstanceFields));

            TypeInfo ti = typeof(FieldInvokeSample).GetTypeInfo();

            FieldInfo instanceField = ti.GetDeclaredField("InstanceField");
            FieldInvokeSample obj = new FieldInvokeSample();

            String value = (String)(instanceField.GetValue(obj));
            if (value != null)
                throw new Exception();

            obj.InstanceField = "Hi!";
            value = (String)(instanceField.GetValue(obj));
            if (value != "Hi!")
                throw new Exception();

            instanceField.SetValue(obj, "Bye!");
            if (obj.InstanceField != "Bye!")
                throw new Exception();

            value = (String)(instanceField.GetValue(obj));
            if (value != "Bye!")
                throw new Exception();
        }
    }

    class TestCreateDelegate
    {
        internal class Greeter
        {
            private string _who;

            public Greeter(string who) { _who = who; }

#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
            public string Greet()
            {
                return "Hello " + _who;
            }
        }

        delegate string GetHelloInstanceDelegate(Greeter o);

        public static void Run()
        {
            Console.WriteLine(nameof(TestCreateDelegate));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                new Greeter(null).Greet();
                GetHelloInstanceDelegate d = null;
                Func<Greeter, string> d2 = d.Invoke;
                d = d2.Invoke;
            }

            TypeInfo ti = typeof(Greeter).GetTypeInfo();
            MethodInfo mi = ti.GetDeclaredMethod(nameof(Greeter.Greet));
            {
                var d = (GetHelloInstanceDelegate)mi.CreateDelegate(typeof(GetHelloInstanceDelegate));
                if (d(new Greeter("mom")) != "Hello mom")
                    throw new Exception();
            }

            {
                var d = (Func<Greeter, string>)mi.CreateDelegate(typeof(Func<Greeter, string>));
                if (d(new Greeter("pop")) != "Hello pop")
                    throw new Exception();
            }
        }
    }

    class TestParameterAttributes
    {
#if OPTIMIZED_MODE_WITHOUT_SCANNER
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
#endif
        public static bool Method([Parameter] ParameterType parameter)
        {
            return parameter == null;
        }

        public class ParameterType { }

        class ParameterAttribute : Attribute
        {
            public ParameterAttribute([CallerMemberName] string memberName = null)
            {
                MemberName = memberName;
            }

            public string MemberName { get; }
        }

        public static void Run()
        {
            Console.WriteLine(nameof(TestParameterAttributes));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                Method(null);
            }

            MethodInfo method = typeof(TestParameterAttributes).GetMethod(nameof(Method));

            var attribute = method.GetParameters()[0].GetCustomAttribute<ParameterAttribute>();
            if (attribute.MemberName != nameof(Method))
                throw new Exception();
        }
    }

    class TestAttributeExpressions
    {
        struct FirstNeverUsedType { }

        struct SecondNeverUsedType { }

        class Gen<T> { }

        class TypeAttribute : Attribute
        {
            public Type SomeType { get; set; }

            public TypeAttribute() { }
            public TypeAttribute(Type someType)
            {
                SomeType = someType;
            }
        }

        enum MyEnum { }

        class EnumArrayAttribute : Attribute
        {
            public MyEnum[] EnumArray;
        }

        [Type(typeof(FirstNeverUsedType*[,]))]
        class Holder1 { }

        [Type(SomeType = typeof(Gen<SecondNeverUsedType>))]
        class Holder2 { }

        [EnumArray(EnumArray = new MyEnum[] { 0 })]
        class Holder3 { }

        public static void Run()
        {
            Console.WriteLine(nameof(TestAttributeExpressions));

            TypeAttribute attr1 = typeof(Holder1).GetCustomAttribute<TypeAttribute>();
            if (attr1.SomeType.ToString() != "ReflectionTest+TestAttributeExpressions+FirstNeverUsedType*[,]")
                throw new Exception();

            TypeAttribute attr2 = typeof(Holder2).GetCustomAttribute<TypeAttribute>();
            if (attr2.SomeType.ToString() != "ReflectionTest+TestAttributeExpressions+Gen`1[ReflectionTest+TestAttributeExpressions+SecondNeverUsedType]")
                throw new Exception();

            EnumArrayAttribute attr3 = typeof(Holder3).GetCustomAttribute<EnumArrayAttribute>();
            if (attr3.EnumArray[0] != 0)
                throw new Exception();
        }
    }

    class TestAssemblyAndModuleAttributes
    {
        public static void Run()
        {
            Console.WriteLine(nameof(TestAssemblyAndModuleAttributes));

            // Also tests GetExecutingAssembly
            var assAttr = Assembly.GetExecutingAssembly().GetCustomAttribute<TestAssemblyAttribute>();
            if (assAttr == null)
                throw new Exception();

            // Also tests GetEntryAssembly
            var modAttr = Assembly.GetEntryAssembly().ManifestModule.GetCustomAttribute<TestModuleAttribute>();
            if (modAttr == null)
                throw new Exception();
        }
    }

    class TestStringConstructor
    {
        public static void Run()
        {
            Console.WriteLine(nameof(TestStringConstructor));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                new string(new char[] { }, 0, 0);
            }

            ConstructorInfo ctor = typeof(string).GetConstructor(new Type[] { typeof(char[]), typeof(int), typeof(int) });
            object str = ctor.Invoke(new object[] { new char[] { 'a' }, 0, 1 });
            if ((string)str != "a")
                throw new Exception();
        }
    }

    class TestAttributeInheritance
    {
        class BaseAttribute : Attribute
        {
            public string Field;
            public int Property { get; set; }
        }

        class DerivedAttribute : BaseAttribute { }

        [Derived(Field = "Hello", Property = 100)]
        class TestType { }

        public static void Run()
        {
            Console.WriteLine(nameof(TestAttributeInheritance));

            DerivedAttribute attr = typeof(TestType).GetCustomAttribute<DerivedAttribute>();
            if (attr.Field != "Hello" || attr.Property != 100)
                throw new Exception();
        }
    }

    class TestByRefLikeTypeMethod
    {
        ref struct ByRefLike
        {
            public readonly int Value;

            public ByRefLike(int value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString();
            }
        }

        ref struct ByRefLike<T>
        {
            public readonly T Value;

            public ByRefLike(T value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return Value.ToString() + " " + typeof(T).ToString();
            }
        }

        delegate string ToStringDelegate(ref ByRefLike thisObj);
        delegate string ToStringDelegate<T>(ref ByRefLike<T> thisObj);

        public static void Run()
        {
            Console.WriteLine(nameof(TestByRefLikeTypeMethod));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                default(ByRefLike).ToString();
                ToStringDelegate s = null;
                s = s.Invoke;
                default(ByRefLike<object>).ToString();
                ToStringDelegate<object> s2 = null;
                s2 = s2.Invoke;
            }

            {
                Type byRefLikeType = GetTestType(nameof(TestByRefLikeTypeMethod), nameof(ByRefLike));
                MethodInfo toStringMethod = byRefLikeType.GetMethod("ToString");
                var toString = (ToStringDelegate)toStringMethod.CreateDelegate(typeof(ToStringDelegate));

                ByRefLike foo = new ByRefLike(123);
                if (toString(ref foo) != "123")
                    throw new Exception();
            }

            {
                Type byRefLikeGenericType = typeof(ByRefLike<string>);
                MethodInfo toStringGenericMethod = byRefLikeGenericType.GetMethod("ToString");
                var toStringGeneric = (ToStringDelegate<string>)toStringGenericMethod.CreateDelegate(typeof(ToStringDelegate<string>));

                ByRefLike<string> fooGeneric = new ByRefLike<string>("Hello");
                if (toStringGeneric(ref fooGeneric) != "Hello System.String")
                    throw new Exception();
            }

            {
                Type byRefLikeGenericType = typeof(ByRefLike<object>);
                MethodInfo toStringGenericMethod = byRefLikeGenericType.GetMethod("ToString");
                var toStringGeneric = (ToStringDelegate<object>)toStringGenericMethod.CreateDelegate(typeof(ToStringDelegate<object>));

                ByRefLike<object> fooGeneric = new ByRefLike<object>("Hello");
                if (toStringGeneric(ref fooGeneric) != "Hello System.Object")
                    throw new Exception();
            }
        }
    }

    class TestInterfaceMethod
    {
        interface IFoo
        {
            string Frob(int x);
        }

        class Foo : IFoo
        {
            public string Frob(int x)
            {
                return x.ToString();
            }
        }

        class Gen<T> { }

        interface IFoo<out T>
        {
            string Frob();
        }

        class Foo<T> : IFoo<Gen<T>>
        {
            public string Frob()
            {
                return typeof(T).ToString();
            }
        }

        public static void Run()
        {
            Console.WriteLine(nameof(TestInterfaceMethod));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                ((IFoo)new Foo()).Frob(1);
                ((IFoo<object>)new Foo<string>()).Frob();
            }

            object result = InvokeTestMethod(typeof(IFoo), "Frob", new Foo(), 42);
            if ((string)result != "42")
                throw new Exception();

            result = InvokeTestMethod(typeof(IFoo<object>), "Frob", new Foo<string>());
            if ((string)result != "System.String")
                throw new Exception();
        }
    }

    class TestContainment
    {
        class NeverUsedContainerType
        {
            public class UsedNestedType
            {
                public static int CallMe()
                {
                    return 42;
                }
            }
        }


        public static void Run()
        {
            Console.WriteLine(nameof(TestContainment));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                NeverUsedContainerType.UsedNestedType.CallMe();
            }

            Type neverUsedContainerType = GetTestType(nameof(TestContainment), nameof(NeverUsedContainerType));
            Type usedNestedType = neverUsedContainerType.GetNestedType(nameof(NeverUsedContainerType.UsedNestedType));

            // Since we called CallMe, it has reflection metadata and it is invokable
            object o = InvokeTestMethod(usedNestedType, nameof(NeverUsedContainerType.UsedNestedType.CallMe));
            if ((int)o != 42)
                throw new Exception();

            // We can get a type handle for the nested type (the invoke mapping table needs it)
            if (!HasTypeHandle(usedNestedType))
                throw new Exception($"{nameof(NeverUsedContainerType.UsedNestedType)} should have an EEType");

            // But the containing type doesn't need an EEType
            if (HasTypeHandle(neverUsedContainerType))
                throw new Exception($"{nameof(NeverUsedContainerType)} should not have an EEType");
        }
    }

    class TestByRefReturnInvoke
    {
        enum Mine { One = 2018 }

        [StructLayout(LayoutKind.Sequential)]
        struct BigStruct { public ulong X, Y, Z, W, A, B, C, D; }

        public ref struct ByRefLike { }

        private sealed class TestClass<T>
        {
            private T _value;

            public TestClass(T value) { _value = value; }
            public ref T RefReturningProp
            {
#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [MethodImpl(MethodImplOptions.NoInlining)]
#endif
                get => ref _value;
            }
#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [MethodImpl(MethodImplOptions.NoInlining)]
#endif
            public static unsafe ref ByRefLike ByRefLikeRefReturningMethod(ByRefLike* a) => ref *a;
        }

        private sealed unsafe class TestClassIntPointer
        {
            private int* _value;

            public TestClassIntPointer(int* value) { _value = value; }
            public ref int* RefReturningProp
            {
#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [MethodImpl(MethodImplOptions.NoInlining)]
#endif
                get => ref _value;
            }
            public unsafe ref int* NullRefReturningProp
            {
#if OPTIMIZED_MODE_WITHOUT_SCANNER
            [MethodImpl(MethodImplOptions.NoInlining)]
#endif
                get => ref *(int**)null;
            }
        }

        public static void TestRefReturnPropertyGetValue()
        {
            TestRefReturnInvoke('a', (p, t) => p.GetValue(t));
            TestRefReturnInvoke(Mine.One, (p, t) => p.GetValue(t));
            TestRefReturnInvoke("Hello", (p, t) => p.GetValue(t));
            TestRefReturnInvoke(new BigStruct { X = 123, D = 456 }, (p, t) => p.GetValue(t));
            TestRefReturnInvoke(new object(), (p, t) => p.GetValue(t));
            TestRefReturnInvoke((object)null, (p, t) => p.GetValue(t));
        }

        public static void TestRefReturnMethodInvoke()
        {
            TestRefReturnInvoke(Mine.One, (p, t) => p.GetGetMethod().Invoke(t, Array.Empty<object>()));
            TestRefReturnInvoke("Hello", (p, t) => p.GetGetMethod().Invoke(t, Array.Empty<object>()));
            TestRefReturnInvoke(new BigStruct { X = 123, D = 456 }, (p, t) => p.GetGetMethod().Invoke(t, Array.Empty<object>()));
            TestRefReturnInvoke(new object(), (p, t) => p.GetGetMethod().Invoke(t, Array.Empty<object>()));
            TestRefReturnInvoke((object)null, (p, t) => p.GetGetMethod().Invoke(t, Array.Empty<object>()));
        }

        public static void TestRefReturnNullable()
        {
            TestRefReturnInvokeNullable<int>(42);
            TestRefReturnInvokeNullable<Mine>(Mine.One);
            TestRefReturnInvokeNullable<BigStruct>(new BigStruct { X = 987, D = 543 });
        }

        public static void TestRefReturnNullableNoValue()
        {
            TestRefReturnInvokeNullable<int>(default(int?));
            TestRefReturnInvokeNullable<Mine>(default(Mine?));
            TestRefReturnInvokeNullable<BigStruct>(default(BigStruct?));
        }

        public static unsafe void TestRefReturnOfPointer()
        {
            int* expected = (int*)0x1122334455667788;
            TestClassIntPointer tc = new TestClassIntPointer(expected);

            if (string.Empty.Length > 0)
            {
                ((IntPtr)tc.RefReturningProp).ToString();
            }

            PropertyInfo p = typeof(TestClassIntPointer).GetProperty(nameof(TestClassIntPointer.RefReturningProp));
            object rv = p.GetValue(tc);
            Assert.True(rv is Pointer);
            int* actual = (int*)(Pointer.Unbox(rv));
            Assert.Equal((IntPtr)expected, (IntPtr)actual);
        }

        public static unsafe void TestNullRefReturnOfPointer()
        {
            TestClassIntPointer tc = new TestClassIntPointer(null);

            if (string.Empty.Length > 0)
            {
                ((IntPtr)tc.NullRefReturningProp).ToString();
            }

            PropertyInfo p = typeof(TestClassIntPointer).GetProperty(nameof(TestClassIntPointer.NullRefReturningProp));
            Assert.NotNull(p);
            Assert.Throws<NullReferenceException>(() => p.GetValue(tc));
        }

        public static unsafe void TestByRefLikeRefReturn()
        {
            if (string.Empty.Length > 0)
            {
                TestClass<int>.ByRefLikeRefReturningMethod(null);
            }

            ByRefLike brl = new ByRefLike();
            ByRefLike* pBrl = &brl;
            MethodInfo mi = typeof(TestClass<int>).GetMethod(nameof(TestClass<int>.ByRefLikeRefReturningMethod));
            try
            {
                // Don't use Assert.Throws because that will make a lambda and invalidate the pointer
                object o = mi.Invoke(null, new object[] { Pointer.Box(pBrl, typeof(ByRefLike*)) });
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
        }

        private static void TestRefReturnInvoke<T>(T value, Func<PropertyInfo, TestClass<T>, object> invoker)
        {
            TestClass<T> tc = new TestClass<T>(value);

            if (String.Empty.Length > 0)
            {
                tc.RefReturningProp.ToString();
            }

            PropertyInfo p = typeof(TestClass<T>).GetProperty(nameof(TestClass<T>.RefReturningProp));
            object rv = invoker(p, tc);
            if (rv != null)
            {
                Assert.Equal(typeof(T), rv.GetType());
            }

            if (typeof(T).IsValueType)
            {
                Assert.Equal(value, rv);
            }
            else
            {
                Assert.Same(value, rv);
            }
        }

        private static void TestRefReturnInvokeNullable<T>(T? nullable) where T : struct
        {
            TestClass<T?> tc = new TestClass<T?>(nullable);

            if (string.Empty.Length > 0)
            {
                tc.RefReturningProp.ToString();
            }

            PropertyInfo p = typeof(TestClass<T?>).GetProperty(nameof(TestClass<T?>.RefReturningProp));
            object rv = p.GetValue(tc);
            if (rv != null)
            {
                Assert.Equal(typeof(T), rv.GetType());
            }
            if (nullable.HasValue)
            {
                Assert.Equal(nullable.Value, rv);
            }
            else
            {
                Assert.Null(rv);
            }
        }

        public static void Run()
        {
            Console.WriteLine(nameof(TestByRefReturnInvoke));
            TestRefReturnPropertyGetValue();
            TestRefReturnMethodInvoke();
            TestRefReturnNullable();
            TestRefReturnNullableNoValue();
            TestRefReturnOfPointer();
            TestNullRefReturnOfPointer();
            TestByRefLikeRefReturn();
        }
    }

    #region Helpers

    private static Type GetTestType(string testName, string typeName)
    {
        string fullTypeName = $"{nameof(ReflectionTest)}+{testName}+{typeName}";
        Type result = Type.GetType(fullTypeName);
        if (result == null)
            throw new Exception($"'{fullTypeName}' could not be located");
        return result;
    }

    private static object InvokeTestMethod(Type type, string methodName, object thisObj = null, params object[] param)
    {
        MethodInfo method = type.GetMethod(methodName);
        if (method == null)
            throw new Exception($"Method '{methodName}' not found on type {type}");

        return method.Invoke(thisObj, param);
    }

    private static bool HasTypeHandle(Type type)
    {
        try
        {
            RuntimeTypeHandle typeHandle = type.TypeHandle;
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    class Assert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (object.ReferenceEquals(expected, actual))
                return;

            if ((object)expected == null || (object)actual == null)
                throw new Exception();

            if (!expected.Equals(actual))
                throw new Exception();
        }

        public static void Same<T>(T expected, T actual)
        {
            if (!object.ReferenceEquals(expected, actual))
                throw new Exception();
        }

        public static void Null(object x)
        {
            if (x != null)
                throw new Exception();
        }

        public static void NotNull(object x)
        {
            if (x == null)
                throw new Exception();
        }

        public static void True(bool x)
        {
            if (!x)
                throw new Exception();
        }

        public static void Fail()
        {
            throw new Exception();
        }

        public static void Throws<T>(Action a)
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                if (ex.GetType() != typeof(T))
                    throw new Exception();
                return;
            }

            throw new Exception();
        }
    }

    #endregion
}

class TestAssemblyAttribute : Attribute { }
class TestModuleAttribute : Attribute { }
