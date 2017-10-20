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
			PrintString(s);
		}
		
		var not = Not(0xFFFFFFFF) == 0x00000000;
		if(not)
		{
			PrintString("\n");
			PrintString("not test: Ok.");
		}
		
		var negInt = Neg(42) == -42;
		if(negInt)
		{
			PrintString("\n");
			PrintString("negInt test: Ok.");
		}

        var shiftLeft = ShiftLeft(1, 2) == 4;
        if(shiftLeft)
        {
            PrintString("\n");
            PrintString("shiftLeft test: Ok.");
        }

        var shiftRight = ShiftRight(4, 2) == 1;
        if(shiftRight)
        {
            PrintString("\n");
            PrintString("shiftRight test: Ok.");
        }
        var unsignedShift = UnsignedShift(0xFFFFFFFFu, 4) == 0x0FFFFFFFu;
        if(unsignedShift)
        {
            PrintString("\n");
            PrintString("unsignedShift test: Ok.");
        }
		
		var switchTest0 = SwitchOp(5, 5, 0);
		if(switchTest0 == 10)
		{
			PrintString("\n");
            PrintString("SwitchOp0 test: Ok.");
		}
		
		var switchTest1 = SwitchOp(5, 5, 1);
		if(switchTest1 == 25)
		{
			PrintString("\n");
            PrintString("SwitchOp1 test: Ok.");
		}
		
		var switchTestDefault = SwitchOp(5, 5, 20);
		if(switchTestDefault == 0)
		{
			PrintString("\n");
            PrintString("SwitchOpDefault test: Ok.");
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
	
	private static int SwitchOp(int a, int b, int mode)
	{
		switch(mode)
		{
			case 0:
				return a + b;
			case 1:
				return a * b;
			case 2:
				return a / b;
			case 3:
				return a - b;
			default:
				return 0;
		}
    }

    [DllImport("*")]
    private static unsafe extern int printf(byte* str, byte* unused);
}

public struct TwoByteStr
{
    public byte first;
    public byte second;
}

