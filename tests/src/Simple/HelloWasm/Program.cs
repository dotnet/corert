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

        int tempInt = 0;
        (*(&tempInt)) = 9;
    
        TwoByteStr str = new TwoByteStr() { first = 1, second = 2 };
        TwoByteStr str2 = new TwoByteStr() { first = 3, second = 4 };;
        *(&str) = str2;
        str2 = *(&str);
		
		if(tempInt == 9)
		{
			string s = "Hello from C#!";
			PrintString(s, 14);
		}
		
		var not = Not(0xFFFFFFFF) == 0x00000000;
		if(not)
		{
			PrintString("\n", 1);
			PrintString("not test: Ok.", 13);
		}
		
		var negInt = Neg(42) == -42;
		if(negInt)
		{
			PrintString("\n", 1);
			PrintString("negInt test: Ok.", 16);
		}

        var shiftLeft = ShiftLeft(1, 2) == 4;
        if(shiftLeft)
        {
            PrintString("\n", 1);
            PrintString("shiftLeft test: Ok.", 19);
        }

        var shiftRight = ShiftRight(4, 2) == 1;
        if(shiftRight)
        {
            PrintString("\n", 1);
            PrintString("shiftRight test: Ok.", 20);
        }
        var unsignedShift = UnsignedShift(0xFFFFFFFFu, 4) == 0x0FFFFFFFu;
        if(unsignedShift)
        {
            PrintString("\n", 1);
            PrintString("unsignedShift test: Ok.", 23);
        }
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

