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
        if (TestStaticBases() == Fail)
            return Fail;

        if (TestSharedGenerics() == Fail)
            return Fail;

        return Pass;
    }
    
    public static int TestStaticBases()
    {
        Console.WriteLine("Testing static bases in library code are available..");
        MultiModuleLibrary.ReturnValue = 50;
        MultiModuleLibrary.ThreadStaticInt = 50;
        
        MultiModuleLibrary.StaticString = MultiModuleLibrary.ReturnValue.ToString() + MultiModuleLibrary.ThreadStaticInt.ToString();
        if (MultiModuleLibrary.StaticString != "5050")
            return Fail;
        
        if (MultiModuleLibrary.ReturnValue + MultiModuleLibrary.ThreadStaticInt != 100)
            return Fail;
        
        return Pass;
    }

    public static int TestSharedGenerics()
    {
        // Use a generic dictionary that also exists in the library
        if (!MultiModuleLibrary.GenericClass<string>.IsT("Hello"))
            return Fail;
        if (!MultiModuleLibrary.GenericClass<string>.IsMdArrayOfT(new string[0, 0]))
            return Fail;

        if (!MultiModuleLibrary.GenericClass<MultiModuleLibrary.GenericStruct<string>>.IsArrayOfT(new MultiModuleLibrary.GenericStruct<string>[0]))
            return Fail;
        if (!MultiModuleLibrary.GenericClass<MultiModuleLibrary.GenericStruct<string>>.IsT(new MultiModuleLibrary.GenericStruct<string>()))
            return Fail;

        if (!MultiModuleLibrary.MethodThatUsesGenerics())
            return Fail;

        return Pass;
    }
}
