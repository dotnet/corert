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
}
