// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using TestingNamespace;

internal class Program
{

    private static bool MultiModule()
    {
        //return true;
        TestingClass testingClass = new TestingClass(1);
        TestingClassInNamespace testingClassInNamespace = new TestingClassInNamespace(2);
        TestingClassInNamespace.TestingClassNested testingClassNested = new TestingClassInNamespace.TestingClassNested(3);
        return testingClass.GetN() == 1 && testingClassInNamespace.GetN() == 2 && testingClassNested.GetN() == 3;
    }
    
    public static int Main(string[] args)
    {
        bool result = MultiModule();

        if (result)
        {
            Console.WriteLine("Test passed!");
            return 100;
        }
        else
        {
            Console.Error.WriteLine("Test failed");
            return 100;
        }
    }
}
