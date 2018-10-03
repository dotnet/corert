// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

class Caller<T> where T : IComparable
{
    public static int Compare(T t, object o)
    {
        if ((o is Int32 && 123.CompareTo(o) == 0) ||
            (o is string && "hello".CompareTo(o) == 0))
        {
            return -42;
        }

        return t.CompareTo(o);
    }
}

internal class Program
{
    private static int Main(string[] args)
    {
        int intResult = Caller<int>.Compare(122, 123);
        const int ExpectedIntResult = -42;
        Console.WriteLine("Int result: {0}, expected: {1}", intResult, ExpectedIntResult);
        
        int stringResult = Caller<string>.Compare("hello", "world");
        const int ExpectedStringResult = -1;
        Console.WriteLine("String result: {0}, expected: {1}", stringResult, ExpectedStringResult);
        
        return intResult == ExpectedIntResult && stringResult == ExpectedStringResult ? 100 : 1;
    }
}
