// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace StaticLibrary
{
    public class ClassLibrary
    {
        [UnmanagedCallersOnly(EntryPoint = "Add", CallingConvention = CallingConvention.StdCall)]
        public static int Add(int a, int b)
        {
            return a + b;
        }

        [UnmanagedCallersOnly(EntryPoint = "Subtract", CallingConvention = CallingConvention.StdCall)]
        public static int Subtract(int a, int b)
        {
            return a - b;
        }

        [UnmanagedCallersOnly(EntryPoint = "Not", CallingConvention = CallingConvention.StdCall)]
        public static bool Not(bool b)
        {
            return !b;
        }
    }
}
