// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

#pragma warning disable 169

namespace ContainsGCPointers
{
    struct NoPointers
    {
        int int1;
        byte byte1;
        char char1;
    }

    struct StillNoPointers
    {
        NoPointers noPointers1;
        bool bool1;
    }

    class ClassNoPointers
    {
        char char1;
    }

    struct HasPointers
    {
        string string1;
    }

    struct FieldHasPointers
    {
        HasPointers hasPointers1;
    }

    class ClassHasPointers
    {
        ClassHasPointers classHasPointers1;
    }

    class BaseClassHasPointers : ClassHasPointers
    {
    }

    public class ClassHasIntArray
    {
        int[] intArrayField;
    }

    public class ClassHasArrayOfClassType
    {
        ClassNoPointers[] classTypeArray;
    }
}

namespace Explicit
{
    [StructLayout(LayoutKind.Explicit)]
    class Class1
    {
        static int Stat;
        [FieldOffset(4)]
        bool Bar;
        [FieldOffset(10)]
        char Baz;
    }

    [StructLayout(LayoutKind.Explicit)]
    class Class2 : Class1
    {
        [FieldOffset(0)]
        int Lol;
        [FieldOffset(20)]
        byte Omg;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    class ExplicitSize : Class1
    {
        [FieldOffset(0)]
        int Lol;
        [FieldOffset(20)]
        byte Omg;
    }

    [StructLayout(LayoutKind.Explicit)]
    public class ExplicitEmptyClass
    {
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ExplicitEmptyStruct
    {
    }
}

namespace Sequential
{
    [StructLayout(LayoutKind.Sequential)]
    class Class1
    {
        int MyInt;
        bool MyBool;
        char MyChar;
        string MyString;
        byte[] MyByteArray;
        Class1 MyClass1SelfRef;
    }

    [StructLayout(LayoutKind.Sequential)]
    class Class2 : Class1
    {
        int MyInt2;
    }

    // [StructLayout(LayoutKind.Sequential)] is applied by default by the C# compiler
    struct Struct0
    {
        bool b1;
        bool b2;
        bool b3;
        int i1;
        string s1;
    }

    // [StructLayout(LayoutKind.Sequential)] is applied by default by the C# compiler
    struct Struct1
    {
        Struct0 MyStruct0;
        bool MyBool;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class ClassDoubleBool
    {
        double double1;
        bool bool1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class ClassBoolDoubleBool
    {
        bool bool1;
        double double1;
        bool bool2;
    }
}

namespace Auto
{
    class ClassWithStructs
    {
        Struct0 MyStruct0;
        bool MyBool1;
        char MyChar1;
        int MyInt;
        double MyDouble;
        long MyLong;
        byte[] MyByteArray;
        string MyString1;
        bool MyBool2;
        Struct1 MyStruct1;
        Struct2 MyStruct2;
    }

    class Class1_7BytesRemaining
    {
        bool MyBool1;
        double MyDouble;
        long MyLong;
        byte[] MyByteArray;
        string MyString1;
    }

    class Class2_3BytesRemaining : Class1_7BytesRemaining
    {
        int MyInt2;
        int MyInt3;
        string MyString2;
        bool MyBool3;
        bool MyBool4;
        char MyChar2;
    }

    class Class5 : Class1_7BytesRemaining
    {
        bool MyBool4;
        char MyChar5;
        long MyLong5;
        string MyString5;
    }

    class Class6 : Class2_3BytesRemaining // Not aligned
    {
        char MyChar6;
        int MyInt6;
        string MyString6;
    }

    struct Struct0
    {
        bool MyStruct0Bool;
    }

    struct Struct1
    {
        int MyStruct1Int;
        char MyStruct1Char;
    }

    struct Struct2
    {
        char MyStruct2Char;
    }
}

namespace IsByRefLike
{
    public ref struct ByRefLikeStruct
    {
        ByReference<object> ByRef;
    }

    public struct NotByRefLike
    {
        int X;
    }
}
