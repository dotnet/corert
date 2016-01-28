// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

internal class Program
{
    private static int Main(string[] args)
    {
        string s123 = 123.ToString();
        if (s123 != "123")
        {
            Console.WriteLine("Unexpected: " + s123);
            return 1;
        }

        Console.WriteLine("{0}", "Hi");
        return 100;
    }
}
