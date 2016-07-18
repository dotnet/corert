// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

internal class Program
{
    private static void Main(string[] args)
    {
        int firstNum = 3;
        string foo = "foo";
        firstNum++;
        string bar = "bar";
        Program pr = new Program();
        Foo fooObj = new Foo();
        firstNum = fooObj.doStuff();
        pr.doSomething();
        Console.WriteLine("Hello world " + bar + foo);
    }

    public void doSomething()
    {

    }
}
