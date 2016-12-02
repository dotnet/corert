// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

public class ReflectionTest
{
    const int Pass = 100;
    const int Fail = -1;

    public static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        if (TestNames() == Fail)
            return Fail;

        if (TestUnification() == Fail)
            return Fail;

        if (TestTypeOf() == Fail)
            return Fail;

        if (TestGenericComposition() == Fail)
            return Fail;

        if (TestReflectionInvoke() == Fail)
            return Fail;

        if (TestReflectionFieldAccess() == Fail)
            return Fail;

        return Pass;
    }

    private static int TestNames()
    {
        string hello = "Hello";

        Type stringType = hello.GetType();

        if (stringType.FullName != "System.String")
        {
            Console.WriteLine("Bad name");
            return Fail;
        }

        return Pass;
    }

    private static int TestUnification()
    {
        Console.WriteLine("Testing unification");

        // ReflectionTest type doesn't have an EEType and is metadata only.
        Type programType = Type.GetType("ReflectionTest");
        TypeInfo programTypeInfo = programType.GetTypeInfo();

        Type programBaseType = programTypeInfo.BaseType;

        Type objectType = (new Object()).GetType();

        if (!objectType.Equals(programBaseType))
        {
            Console.WriteLine("Unification failed");
            return Fail;
        }

        return Pass;
    }

    private static int TestTypeOf()
    {
        Console.WriteLine("Testing typeof()");

        Type intType = typeof(int);

        if (intType.FullName != "System.Int32")
        {
            Console.WriteLine("Bad name");
            return Fail;
        }

        if (12.GetType() != typeof(int))
        {
            Console.WriteLine("Bad compare");
            return Fail;
        }

        // This type only has a limited EEType (without a vtable) because it's not constructed.
        if (typeof(UnallocatedType).FullName != "UnallocatedType")
        {
            return Fail;
        }

        if (typeof(int) != typeof(int))
        {
            Console.WriteLine("Bad compare");
            return Fail;
        }

        return Pass;
    }

    private static int TestGenericComposition()
    {
        Console.WriteLine("Testing generic composition");

        Type nullableOfIntType = typeof(int?);

        string fullName = nullableOfIntType.FullName;
        if (fullName.Contains("System.Nullable`1") && fullName.Contains("System.Int32"))
            return Pass;

        return Fail;
    }

    internal class InvokeTests
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetHello(string name)
        {
            return "Hello " + name;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetHelloByRef(string name, out string result)
        {
            result = "Hello " + name;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetHelloGeneric<T>(T obj)
        {
            return "Hello " + obj;
        }
    }

    private static int TestReflectionInvoke()
    {
        Console.WriteLine("Testing reflection invoke");

        // Dummy code to make sure the reflection targets are compiled.
        if (String.Empty.Length > 0)
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
                return Fail;
        }

        {
            MethodInfo helloGenericMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetHelloGeneric").MakeGenericMethod(typeof(int));
            string result = (string)helloGenericMethod.Invoke(null, new object[] { 12345 });
            if (result != "Hello 12345")
                return Fail;
        }

        {
            MethodInfo helloGenericMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetHelloGeneric").MakeGenericMethod(typeof(double));
            string result = (string)helloGenericMethod.Invoke(null, new object[] { 3.14 });
            if (result != "Hello 3.14")
                return Fail;
        }

        // TODO: Can't be tested temporarily since it would end up calling into type loader to load a ByRef type.
        //{
        //    MethodInfo helloByRefMethod = typeof(InvokeTests).GetTypeInfo().GetDeclaredMethod("GetHelloByRef");
        //    object[] args = new object[] { "world", null };
        //    helloByRefMethod.Invoke(null, args);
        //    if ((string)args[1] != "Hello world")
        //        return Fail;
        //}

        return Pass;
    }

    public class FieldInvokeSample
    {
        public String InstanceField;
    }

    private static int TestReflectionFieldAccess()
    {
        Console.WriteLine("Testing reflection field access");

        if (string.Empty.Length > 0)
        {
            new FieldInvokeSample().ToString();
        }

        TypeInfo ti = typeof(FieldInvokeSample).GetTypeInfo();
        {
            FieldInfo instanceField = ti.GetDeclaredField("InstanceField");
            FieldInvokeSample obj = new FieldInvokeSample();

            String value = (String)(instanceField.GetValue(obj));
            if (value != null)
                return Fail;

            obj.InstanceField = "Hi!";
            value = (String)(instanceField.GetValue(obj));
            if (value != "Hi!")
                return Fail;

            instanceField.SetValue(obj, "Bye!");
            if (obj.InstanceField != "Bye!")
                return Fail;

            value = (String)(instanceField.GetValue(obj));
            if (value != "Bye!")
                return Fail;

            return Pass;
        }
    }
}

class UnallocatedType { }
