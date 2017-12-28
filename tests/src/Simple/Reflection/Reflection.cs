// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if MULTIMODULE_BUILD && !DEBUG
// Some tests won't work if we're using optimizing codegen, but scanner doesn't run.
// This currently happens in optimized multi-obj builds.
#define OPTIMIZED_MODE_WITHOUT_SCANNER
#endif

using System;
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
#endif
        TestAttributeInheritance.Run();
        TestStringConstructor.Run();
        TestAssemblyAndModuleAttributes.Run();
        TestAttributeExpressions.Run();

        //
        // Mostly functionality tests
        //
        TestCreateDelegate.Run();
        TestInstanceFields.Run();
        TestReflectionInvoke.Run();

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
        }

        public static void Run()
        {
            Console.WriteLine(nameof(TestReflectionInvoke));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                new InvokeTests().ToString();
                InvokeTests.GetHello(null);
                InvokeTests.GetHelloGeneric<int>(0);
                InvokeTests.GetHelloGeneric<double>(0);
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

        public static void Run()
        {
            Console.WriteLine(nameof(TestInterfaceMethod));

            // Ensure things we reflect on are in the static callgraph
            if (string.Empty.Length > 0)
            {
                ((IFoo)new Foo()).Frob(1);
            }

            object result = InvokeTestMethod(typeof(IFoo), "Frob", new Foo(), 42);
            if ((string)result != "42")
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

    #endregion
}

class TestAssemblyAttribute : Attribute { }
class TestModuleAttribute : Attribute { }
