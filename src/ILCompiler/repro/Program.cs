// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

interface IFoo<out U>
{
    string IMethod1<T>(T t1, T t2);
}

class Base : IFoo<string>, IFoo<int>
{
    public virtual string GMethod1<T>(T t1, T t2)
    {
        return "Base.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }

    public virtual string IMethod1<T>(T t1, T t2)
    {
        return "Base.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}

class Derived : Base, IFoo<string>, IFoo<int>
{
    public override string GMethod1<T>(T t1, T t2)
    {
        return "Derived.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }

    string IFoo<string>.IMethod1<T>(T t1, T t2)
    {
        return "Derived.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}

class SuperDerived : Derived, IFoo<string>, IFoo<int>
{
    string IFoo<int>.IMethod1<T>(T t1, T t2)
    {
        return "SuperDerived.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}



class GenBase<A> : IFoo<string>, IFoo<int>
{
    public virtual string VMethod1()
    {
        return "GenBase<" + typeof(A) + ">.VMethod1()";
    }

    public virtual string GMethod1<T>(T t1, T t2)
    {
        return "GenBase<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }

    public virtual string IMethod1<T>(T t1, T t2)
    {
        return "GenBase<" + typeof(A) + ">.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}

class GenDerived<A> : GenBase<A>, IFoo<string>, IFoo<int>
{
    public override string VMethod1()
    {
        return "GenBase<" + typeof(A) + ">.VMethod1()";
    }

    public override string GMethod1<T>(T t1, T t2)
    {
        return "GenDerived<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }

    string IFoo<string>.IMethod1<T>(T t1, T t2)
    {
        return "GenDerived<" + typeof(A) + ">.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}

class GenSuperDerived<A> : GenDerived<A>, IFoo<string>, IFoo<int>
{
    string IFoo<int>.IMethod1<T>(T t1, T t2)
    {
        return "GenSuperDerived<" + typeof(A) + ">.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}



struct MyStruct1 : IFoo<string>, IFoo<int>
{
    string IFoo<string>.IMethod1<T>(T t1, T t2)
    {
        return "MyStruct1.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
    string IFoo<int>.IMethod1<T>(T t1, T t2)
    {
        return "MyStruct1.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}

struct MyStruct2 : IFoo<string>, IFoo<int>
{
    string IFoo<string>.IMethod1<T>(T t1, T t2)
    {
        return "MyStruct2.IFoo<string>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
    public string IMethod1<T>(T t1, T t2)
    {
        return "MyStruct2.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}

struct MyStruct3 : IFoo<string>, IFoo<int>
{
    string IFoo<int>.IMethod1<T>(T t1, T t2)
    {
        return "MyStruct3.IFoo<int>.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
    public string IMethod1<T>(T t1, T t2)
    {
        return "MyStruct3.IMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")";
    }
}

internal class Program
{
    static string s_GMethod1;
    static string s_IFooString;
    static string s_IFooObject;
    static string s_IFooInt;

    static int s_NumErrors = 0;

    private static int Main(string[] args)
    {
        {
            s_GMethod1 = "Base.GMethod1<System.Int32>(1,2)";
            s_IFooString = "Base.IMethod1<System.Int32>(3,4)";
            s_IFooObject = "Base.IMethod1<System.Int32>(5,6)";
            s_IFooInt = "Base.IMethod1<System.Int32>(7,8)";
            TestWithClass(new Base());
            Console.WriteLine("====================");


            s_GMethod1 = "Derived.GMethod1<System.Int32>(1,2)";
            s_IFooString = "Derived.IFoo<string>.IMethod1<System.Int32>(3,4)";
            s_IFooObject = "Derived.IFoo<string>.IMethod1<System.Int32>(5,6)";
            s_IFooInt = "Base.IMethod1<System.Int32>(7,8)";
            TestWithClass(new Derived());
            Console.WriteLine("====================");


            s_GMethod1 = "Derived.GMethod1<System.Int32>(1,2)";
            s_IFooString = "Derived.IFoo<string>.IMethod1<System.Int32>(3,4)";
            s_IFooObject = "Derived.IFoo<string>.IMethod1<System.Int32>(5,6)";
            s_IFooInt = "SuperDerived.IFoo<int>.IMethod1<System.Int32>(7,8)";
            TestWithClass(new SuperDerived());
            Console.WriteLine("====================");
        }

        {
            s_GMethod1 = "GenBase<System.Byte>.GMethod1<System.Int32>(1,2)";
            s_IFooString = "GenBase<System.Byte>.IMethod1<System.Int32>(3,4)";
            s_IFooObject = "GenBase<System.Byte>.IMethod1<System.Int32>(5,6)";
            s_IFooInt = "GenBase<System.Byte>.IMethod1<System.Int32>(7,8)";
            TestWithGenClass<byte>(new GenBase<byte>());
            Console.WriteLine("====================");
            
            
            s_GMethod1 = "GenDerived<System.Byte>.GMethod1<System.Int32>(1,2)";
            s_IFooString = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(3,4)";
            s_IFooObject = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(5,6)";
            s_IFooInt = "GenBase<System.Byte>.IMethod1<System.Int32>(7,8)";
            TestWithGenClass<byte>(new GenDerived<byte>());
            Console.WriteLine("====================");
            
            
            s_GMethod1 = "GenDerived<System.String>.GMethod1<System.Int32>(1,2)";
            s_IFooString = "GenDerived<System.String>.IFoo<string>.IMethod1<System.Int32>(3,4)";
            s_IFooObject = "GenDerived<System.String>.IFoo<string>.IMethod1<System.Int32>(5,6)";
            s_IFooInt = "GenBase<System.String>.IMethod1<System.Int32>(7,8)";
            TestWithGenClass<String>(new GenDerived<String>());
            Console.WriteLine("====================");


            s_GMethod1 = "GenDerived<System.Byte>.GMethod1<System.Int32>(1,2)";
            s_IFooString = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(3,4)";
            s_IFooObject = "GenDerived<System.Byte>.IFoo<string>.IMethod1<System.Int32>(5,6)";
            s_IFooInt = "GenSuperDerived<System.Byte>.IFoo<int>.IMethod1<System.Int32>(7,8)";
            TestWithGenClass<byte>(new GenSuperDerived<byte>());
            Console.WriteLine("====================");
        }

        {
            s_IFooString = "MyStruct1.IFoo<string>.IMethod1<System.Int32>(1,2)";
            s_IFooObject = "MyStruct1.IFoo<string>.IMethod1<System.Int32>(3,4)";
            s_IFooInt = "MyStruct1.IFoo<int>.IMethod1<System.Int32>(5,6)";
            TestWithStruct(new MyStruct1(), new MyStruct1(), new MyStruct1());
            Console.WriteLine("====================");


            s_IFooString = "MyStruct2.IFoo<string>.IMethod1<System.Int32>(1,2)";
            s_IFooObject = "MyStruct2.IFoo<string>.IMethod1<System.Int32>(3,4)";
            s_IFooInt = "MyStruct2.IMethod1<System.Int32>(5,6)";
            TestWithStruct(new MyStruct2(), new MyStruct2(), new MyStruct2());
            Console.WriteLine("====================");


            s_IFooString = "MyStruct3.IMethod1<System.Int32>(1,2)";
            s_IFooObject = "MyStruct3.IMethod1<System.Int32>(3,4)";
            s_IFooInt = "MyStruct3.IFoo<int>.IMethod1<System.Int32>(5,6)";
            TestWithStruct(new MyStruct3(), new MyStruct3(), new MyStruct3());
            Console.WriteLine("====================");
        }

        if (s_NumErrors == 0)
        {
            Console.WriteLine("PASSED");
            return 100;
        }
        Console.WriteLine("FAILED");
        return s_NumErrors;
    }

    private static void TestWithStruct(IFoo<string> ifooStr, IFoo<object> ifooObj, IFoo<int> ifooInt)
    {
        var res = ifooStr.IMethod1<int>(1, 2);
        WriteLineWithVerification(res, s_IFooString);

        res = ifooObj.IMethod1<int>(3, 4);
        WriteLineWithVerification(res, s_IFooObject);

        res = ifooInt.IMethod1<int>(5, 6);
        WriteLineWithVerification(res, s_IFooInt);
    }

    private static void TestWithClass(object o)
    {
        Base b = o as Base;
        var res = b.GMethod1<int>(1, 2);
        WriteLineWithVerification(res, s_GMethod1);

        IFoo<string> ifoo1 = o as IFoo<string>;
        res = ifoo1.IMethod1<int>(3, 4);
        WriteLineWithVerification(res, s_IFooString);

        IFoo<object> ifoo2 = o as IFoo<object>;
        res = ifoo2.IMethod1<int>(5, 6);
        WriteLineWithVerification(res, s_IFooObject);

        IFoo<int> ifoo3 = o as IFoo<int>;
        res = ifoo3.IMethod1<int>(7, 8);
        WriteLineWithVerification(res, s_IFooInt);
    }

    private static void TestWithGenClass<T>(object o)
    {
        GenBase<T> b = o as GenBase<T>;
        var res = b.GMethod1<int>(1, 2);
        WriteLineWithVerification(res, s_GMethod1);

        IFoo<string> ifoo1 = o as IFoo<string>;
        res = ifoo1.IMethod1<int>(3, 4);
        WriteLineWithVerification(res, s_IFooString);

        IFoo<object> ifoo2 = o as IFoo<object>;
        res = ifoo2.IMethod1<int>(5, 6);
        WriteLineWithVerification(res, s_IFooObject);

        IFoo<int> ifoo3 = o as IFoo<int>;
        res = ifoo3.IMethod1<int>(7, 8);
        WriteLineWithVerification(res, s_IFooInt);

        res = b.VMethod1();
        Console.WriteLine(res);
    }

    private static void WriteLineWithVerification(string actual, string expected)
    {
        if (actual != expected)
        {
            Console.WriteLine("ACTUAL   : " + actual);
            Console.WriteLine("EXPECTED : " + expected);
            s_NumErrors++;
        }
        else
        {
            Console.WriteLine(actual);
        }
    }
}
