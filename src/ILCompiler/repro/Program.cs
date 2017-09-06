// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

public struct Foo<T>
{
    public override int GetHashCode()
    {
        return 123;
    }
}

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello world");

        Func<int> ghc = new Foo<string>().GetHashCode;

        int a = ghc();

        Console.WriteLine(a);

        var mi = RuntimeReflectionExtensions.GetMethodInfo(ghc);

        Console.WriteLine(mi);
    }
}
