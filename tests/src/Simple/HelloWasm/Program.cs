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
        TwoCharStr strStruct = new TwoCharStr();
        strStruct.first = (byte)'H';
        strStruct.second = (byte)'\0';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)'i';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)' ';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)'f';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)'r';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)'o';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)'m';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)' ';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)'C';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)'#';
        printf((byte*)&strStruct, null);
        strStruct.first = (byte)'!';
        printf((byte*)&strStruct, null);
    }

    private static int Add(int a, int b)
    {
        return a + b;
    }

    [DllImport("*")]
    private static unsafe extern int printf(byte* str, byte* unused);
}

public struct TwoCharStr
{
    public byte first;
    public byte second;
}

