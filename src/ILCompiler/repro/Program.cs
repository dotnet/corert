// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

internal class Program:IInterface
{
    private static void Main(string[] args)
    {
        Program foo = new Program();
        IInterface iFoo = (IInterface)foo;
        string result = iFoo.Print();
        string otherResult = foo.Test();
        Console.WriteLine(result + " " + otherResult);
    }
    public string Print()
    {
        return "Hello";
    }
    public string Test()
    {
        return "world";
    }
}
interface IInterface
{
    string Print();
}