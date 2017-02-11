// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

class Program
{
    public const int Pass = 100;
    public const int Fail = -1;

    static int Main()
    {
        SimpleReadWriteThreadStaticTest.Run(42, "SimpleReadWriteThreadStatic");
        // TODO: After issue https://github.com/dotnet/corert/issues/2695 is fixed, move FinalizeTest to run at the end
        if (FinalizeTest.Run() != Pass)
            return Fail;
        ThreadStaticsTestWithTasks.Run();

        return Pass;
    }
}

class FinalizeTest
{
    public static bool visited = false;
    public class Dummy
    {
        ~Dummy()
        {
            Console.WriteLine("In Finalize() of Dummy");
            FinalizeTest.visited = true;
        }
    }

    public static int Run()
    {
        int iterationCount = 0;
        while (!visited && iterationCount++ < 1000000)
        {
           GC.KeepAlive(new Dummy());
           GC.Collect();
        }

        if (visited)
        {
            Console.WriteLine("Test for Finalize() & WaitForPendingFinalizers() passed!");
            return Program.Pass;
        }
        else
        {
            Console.WriteLine("Test for Finalize() & WaitForPendingFinalizers() failed!");
            return Program.Fail;
        }
    }
}

class SimpleReadWriteThreadStaticTest
{
    public static void Run(int intValue, string stringValue)
    {
        NonGenericReadWriteThreadStaticsTest(intValue, "NonGeneric" + stringValue);
        GenericReadWriteThreadStaticsTest(intValue + 1, "Generic" + stringValue);
    }

    class NonGenericType
    {
        [ThreadStatic]
        public static int IntValue;

        [ThreadStatic]
        public static string StringValue;
    }

    class GenericType<T, V>
    {
        [ThreadStatic]
        public static T ValueT;

        [ThreadStatic]
        public static V ValueV;
    }

    static void NonGenericReadWriteThreadStaticsTest(int intValue, string stringValue)
    {
        NonGenericType.IntValue = intValue;
        NonGenericType.StringValue = stringValue;

        if (NonGenericType.IntValue != intValue)
        {
            throw new Exception("SimpleReadWriteThreadStaticsTest: wrong integer value: " + NonGenericType.IntValue.ToString());
        }

        if (NonGenericType.StringValue != stringValue)
        {
            throw new Exception("SimpleReadWriteThreadStaticsTest: wrong string value: " + NonGenericType.StringValue);
        }
    }

    static void GenericReadWriteThreadStaticsTest(int intValue, string stringValue)
    {
        GenericType<int, string>.ValueT = intValue;
        GenericType<int, string>.ValueV = stringValue;

        if (GenericType<int, string>.ValueT != intValue)
        {
            throw new Exception("GenericReadWriteThreadStaticsTest1a: wrong integer value: " + GenericType<int, string>.ValueT.ToString());
        }

        if (GenericType<int, string>.ValueV != stringValue)
        {
            throw new Exception("GenericReadWriteThreadStaticsTest1b: wrong string value: " + GenericType<int, string>.ValueV);
        }

        intValue++;
        GenericType<int, int>.ValueT = intValue;
        GenericType<int, int>.ValueV = intValue + 1;

        if (GenericType<int, int>.ValueT != intValue)
        {
            throw new Exception("GenericReadWriteThreadStaticsTest2a: wrong integer value: " + GenericType<int, string>.ValueT.ToString());
        }

        if (GenericType<int, int>.ValueV != (intValue + 1))
        {
            throw new Exception("GenericReadWriteThreadStaticsTest2b: wrong integer value: " + GenericType<int, string>.ValueV.ToString());
        }

        GenericType<string, string>.ValueT = stringValue + "a";
        GenericType<string, string>.ValueV = stringValue + "b";

        if (GenericType<string, string>.ValueT != (stringValue + "a"))
        {
            throw new Exception("GenericReadWriteThreadStaticsTest3a: wrong string value: " + GenericType<string, string>.ValueT);
        }

        if (GenericType<string, string>.ValueV != (stringValue + "b"))
        {
            throw new Exception("GenericReadWriteThreadStaticsTest3b: wrong string value: " + GenericType<string, string>.ValueV);
        }
    }
}

class ThreadStaticsTestWithTasks
{
    static object lockObject = new object();
    const int TotalTaskCount = 32;

    public static void Run()
    {
        Task[] tasks = new Task[TotalTaskCount];
        for (int i = 0; i < tasks.Length; ++i)
        {
            tasks[i] = Task.Factory.StartNew((param) =>
            {
                int index = (int)param;
                int intTestValue = index * 10;
                string stringTestValue = "ThreadStaticsTestWithTasks" + index;

                // Try to run the on every other task
                if ((index % 2) == 0)
                {
                    lock (lockObject)
                    {
                        SimpleReadWriteThreadStaticTest.Run(intTestValue, stringTestValue);
                    }
                }
                else
                {
                    SimpleReadWriteThreadStaticTest.Run(intTestValue, stringTestValue);
                }
            }, i);
        }
        for (int i = 0; i < tasks.Length; ++i)
        {
            tasks[i].Wait();
        }
    }
}
