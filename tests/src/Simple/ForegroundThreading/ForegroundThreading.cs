// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    public const int Pass = 100;
    public const int Fail = -1;

    static int Main()
    {

        return ForegroundThreadsWaiter.Run();
    }
}

class ForegroundThreadsWaiter
{

    public static int Run()
    {
        new Thread(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(3));
            Environment.Exit(Program.Pass);
        }).Start();

        return Program.Fail;
    }
}
