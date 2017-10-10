// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static class Program
{
    private static unsafe void Main(string[] args)
    {
        Add(1, 2);
        Shift();
        UnsignedShift();

        string s = "Hello from C#!";
        PrintString(s, 14);
    }

    private static unsafe void PrintString(string s, int length)
    {
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

    private static int Add(int a, int b)
    {
        return a + b;
    }

    private static int Shift(int a, int b)
    {
        int left = a << b;
        int right = a >> b;
        return 0;
    }

    private static int UnsignedShift(uint a, int b)
    {
        return a >> b;
    }

    [DllImport("*")]
    private static unsafe extern int printf(byte* str, byte* unused);
}

public struct TwoByteStr
{
    public byte first;
    public byte second;
}

