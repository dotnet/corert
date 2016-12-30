// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

interface IFoo<U>
{
    string IMethod1<T>(T t);
}

class Base : IFoo<int>, IFoo<string>
{
    public virtual string GMethod1<T>(T t)
    {
        return "Base.GMethod1<" + t.ToString() + ">";
    }

    public virtual string IMethod1<T>(T t)
    {
        return "Base.IMethod1<" + t.ToString() + ">";
    }
}

class Derived : Base, IFoo<int>, IFoo<string>
{
    public override string GMethod1<T>(T t)
    {
        return "Derived.GMethod1<" + t.ToString() + ">";
    }

    string IFoo<int>.IMethod1<T>(T t)
    {
        return "IFoo<int>.IMethod1<" + t.ToString() + ">";
    }
}

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello world");

        Base b = new Base();
        Test(b);

        Console.WriteLine("====================");

        Base c = new Derived();
        Test(c);
    }

    private static void Test(object o)
    {
        Base b = o as Base;
        var res = b.GMethod1<int>(123);
        Console.WriteLine(res);

        res = b.IMethod1<int>(123);
        Console.WriteLine(res);

        IFoo<int> ifoo1 = o as IFoo<int>;
        res = ifoo1.IMethod1<int>(456);
        Console.WriteLine(res);

        IFoo<string> ifoo2 = o as IFoo<string>;
        res = ifoo2.IMethod1<int>(789);
        Console.WriteLine(res);
    }
}
