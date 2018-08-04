// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

internal class Program
{

	private static bool MultiModule()
    {
		//return true;
		TestingClass t = new TestingClass(4);
		Console.WriteLine($@"N = {t.GetN()}");
		return t.GetN() == 4;
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
