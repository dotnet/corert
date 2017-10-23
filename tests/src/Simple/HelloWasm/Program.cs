// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal static class Program
{
    private static int staticInt;
    private static unsafe void Main(string[] args)
    {
        Add(1, 2);

        int tempInt = 0;
        (*(&tempInt)) = 9;

        TwoByteStr str = new TwoByteStr() { first = 1, second = 2 };
        TwoByteStr str2 = new TwoByteStr() { first = 3, second = 4 }; ;
        *(&str) = str2;
        str2 = *(&str);

        if (tempInt == 9)
        {
            string s = "Hello from C#!";
            PrintString(s);
        }

        staticInt = 5;
        if (staticInt == 5)
        {
            PrintString("\n");
            PrintString("static int field test: Ok.");
        }

        if (str.second == 4)
        {
            PrintString("\n");
            PrintString("value type int field test: Ok.");
        }

        var not = Not(0xFFFFFFFF) == 0x00000000;
        if (not)
        {
            PrintString("\n");
            PrintString("not test: Ok.");
        }

        var negInt = Neg(42) == -42;
        if (negInt)
        {
            PrintString("\n");
            PrintString("negInt test: Ok.");
        }

        var shiftLeft = ShiftLeft(1, 2) == 4;
        if (shiftLeft)
        {
            PrintString("\n");
            PrintString("shiftLeft test: Ok.");
        }

        var shiftRight = ShiftRight(4, 2) == 1;
        if (shiftRight)
        {
            PrintString("\n");
            PrintString("shiftRight test: Ok.");
        }
        var unsignedShift = UnsignedShift(0xFFFFFFFFu, 4) == 0x0FFFFFFFu;
        if (unsignedShift)
        {
            PrintString("\n");
            PrintString("unsignedShift test: Ok.");
        }
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

    private static int Add(int a, int b)
    {
        return a + b;
    }

    private static uint Not(uint a)
    {
        return ~a;
    }

    private static int Neg(int a)
    {
        return -a;
    }

    private static int ShiftLeft(int a, int b)
    {
        return a << b;
    }

    private static int ShiftRight(int a, int b)
    {
        return a >> b;
    }

    private static uint UnsignedShift(uint a, int b)
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

