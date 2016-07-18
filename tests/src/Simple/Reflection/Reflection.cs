// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

public class ReflectionTest
{
    const int Pass = 100;
    const int Fail = -1;

    public static int Main()
    {
        if (TestNames() == Fail)
            return Fail;

        if (TestUnification() == Fail)
            return Fail;

        if (TestTypeOf() == Fail)
            return Fail;

        if (TestGenericComposition() == Fail)
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
        Type programType = Type.GetType("ReflectionTest, Reflection");
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
}

class UnallocatedType { }
