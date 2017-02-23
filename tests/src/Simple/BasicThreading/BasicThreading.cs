// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    public const int Pass = 100;
    public const int Fail = -1;

    private static bool s_verbose = false;

    public static void WriteVerbose(string message)
    {
        if (s_verbose)
        {
            Console.WriteLine(message);
        }
    }

    static int Main()
    {
        SimpleReadWriteThreadStaticTest.Run(42, "SimpleReadWriteThreadStatic");
        // TODO: After issue https://github.com/dotnet/corert/issues/2695 is fixed, move FinalizeTest to run at the end
        if (FinalizeTest.Run() != Pass)
            return Fail;
        ThreadStaticsTestWithTasks.Run();

        if (ThreadTest.Run() != Pass)
            return Fail;

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
            Program.WriteVerbose("In Finalize() of Dummy");
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

class ThreadTest
{
    private static int s_passed;
    private static int s_failed;

    private static void Expect(bool condition, string message)
    {
        if (condition)
        {
            Interlocked.Increment(ref s_passed);
        }
        else
        {
            Interlocked.Increment(ref s_failed);
            Console.WriteLine("ERROR: " + message);
        }
    }

    private static void ExpectException<T>(Action action, string message)
    {
        Exception ex = null;
        try
        {
            action();
        }
        catch (Exception e)
        {
            ex = e;
        }
        Expect(ex is T, message);
    }

    public static int Run()
    {
        // Test thread creation
        // Case 1: new Thread(ThreadStart).Start()
        var t1 = new Thread(() => { Program.WriteVerbose("Thread #1"); Expect(true, null); });
        t1.Start();

        // Case 2: new Thread(ThreadStart).Start(parameter)
        var t2 = new Thread(() => { Program.WriteVerbose("Thread #2"); Expect(false, "This thread must not be started"); });
        // InvalidOperationException: The thread was created with a ThreadStart delegate that does not accept a parameter.
        ExpectException<InvalidOperationException>(() => t2.Start(null), "Expected InvalidOperationException");

        // Case 3: new Thread(ParameterizedThreadStart).Start()
        var t3 = new Thread(obj => { Program.WriteVerbose("Thread #3"); Expect(obj == null, "Expected obj == null"); });
        t3.Start();

        // Case 4: new Thread(ParameterizedThreadStart).Start(parameter)
        var t4 = new Thread(obj => { Program.WriteVerbose("Thread #4"); Expect((int)obj == 42, "Expected (int)obj == 42"); });
        t4.Start(42);

        // Test ManagedThreadId, ThreadState, and IsBackground properties
        int t5_id = 0;
        var t5_event = new AutoResetEvent(false);
        Thread t5 = null;
        t5 = new Thread(() => {
            Program.WriteVerbose("Thread #5");
            Expect(object.ReferenceEquals(Thread.CurrentThread, t5), "Expected CurrentTread == t5");
            Expect(Thread.CurrentThread.ManagedThreadId == t5_id, "Expected CurrentTread.ManagedThreadId == t5_id");
            Expect(Environment.CurrentManagedThreadId == t5_id, "Expected Environment.CurrentManagedThreadId == t5_id");
            t5_event.WaitOne();
        });

        Expect(t5.ThreadState == ThreadState.Unstarted, "Expected t5.ThreadState == ThreadState.Unstarted");
        t5_id = t5.ManagedThreadId;
        t5.Start();
        ExpectException<ThreadStateException>(() => t5.Start(), "Expected ThreadStateException");
        Expect(t5.ThreadState == ThreadState.Running || t5.ThreadState == ThreadState.WaitSleepJoin,
            "Expected t5.ThreadState is either ThreadState.Running or ThreadState.WaitSleepJoin");
        Expect(!t5.IsBackground, "Expected t5.IsBackground == false");
        t5_event.Set();
        t5.Join();
        Expect(t5.ThreadState == ThreadState.Stopped, "Expected t5.ThreadState == ThreadState.Stopped");
        ExpectException<ThreadStateException>(() => Console.WriteLine(t5.IsBackground), "Expected ThreadStateException");

        Task.Factory.StartNew(() => Expect(Thread.CurrentThread.IsBackground, "Expected IsBackground == true")).Wait();
        Expect(!Thread.CurrentThread.IsBackground, "Expected CurrentThread.IsBackground == false");

        // The IsThreadPoolThread property is not present in the contract version we compile against at present
        //Expect(!t5.IsThreadPoolThread, "Expected t5.IsThreadPoolThread == false");
        //Task.Factory.StartNew(() => Expect(Thread.CurrentThread.IsThreadPoolThread, "Expected IsThreadPoolThread == true")).Wait();
        //Expect(!Thread.CurrentThread.IsThreadPoolThread, "Expected CurrentThread.IsThreadPoolThread == false");

        // Join all created threads
        foreach (Thread t in new Thread[] { t1, t2, t3, t4, t5 })
        {
            if (t != t2)
            {
                t.Join();
            }
            else
            {
                ExpectException<ThreadStateException>(() => t.Join(), "Expected ThreadStateException");
            }
        }

        Expect(!Thread.CurrentThread.Join(1), "CurrentThread.Join(1) must return false");

        const int expectedPassed = 17;
        Expect(s_passed == expectedPassed, string.Format("Expected s_passed == {0}, not {1}", expectedPassed, s_passed));
        return (s_failed == 0) ? Program.Pass : Program.Fail;
    }
}
