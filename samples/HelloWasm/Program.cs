// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Reflection;

internal static class Program
{

    public struct TwoByteStr
    {
        public byte first;
        public byte second;
    }

    private static unsafe int Main(string[] args)
    {
        PrintLine("Hello Wasm World");

        return 100;
    }

    private static unsafe void PrintString(string s)
    {
        int length = s.Length;
        fixed (char* curChar = s)
        {
            for (int i = 0; i < length; i++)
            {
                TwoByteStr curCharStr = new TwoByteStr();
                curCharStr.first = (byte)(*(curChar + i));
                printf((byte*)&curCharStr, null);
            }
        }
    }

    public static void PrintLine(string s)
    {
        PrintString(s);
        PrintString("\n");
    }

    [DllImport("*")]
    private static unsafe extern int printf(byte* str, byte* unused);

}
