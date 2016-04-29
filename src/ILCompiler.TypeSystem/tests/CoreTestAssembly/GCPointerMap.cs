// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

#pragma warning disable 169 // Field 'blah' is never used

namespace GCPointerMap
{
    [StructLayout(LayoutKind.Sequential)]
    class ClassWithArrayFields
    {
        int[] a1;
        string[] a2;
    }

    [StructLayout(LayoutKind.Sequential)]
    class ClassWithStringField
    {
        static string dummy;
        int i;
        string s;
        bool z;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MixedStruct
    {
        public int X;
        public object Y;
        public int Z;
        public byte U;
        public object V;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct StructWithSameGCLayoutAsMixedStruct
    {
        MixedStruct s;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DoubleMixedStructLayout
    {
        StructWithSameGCLayoutAsMixedStruct X;
        MixedStruct Y;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct ExplicitlyFarPointer
    {
        [FieldOffset(0)]
        object X;

        [FieldOffset(32 * 8)]
        object Y;

        [FieldOffset(40 * 8)]
        object Z;

        [FieldOffset(56 * 8)]
        MixedStruct W;
    }

    class MixedStaticClass
    {
        object dummy1;
        static object o;
        static int dummy2;
        const string dummy3 = "Hello";
        static StructWithSameGCLayoutAsMixedStruct m1;
        static MixedStruct m2;
    }

    class MixedThreadStaticClass
    {
        object dummy1;
        static object dummy2;

        [ThreadStatic]
        static int i;

        [ThreadStatic]
        static StructWithSameGCLayoutAsMixedStruct m1;

        [ThreadStatic]
        static MixedStruct m2;

        [ThreadStatic]
        static object o;

        [ThreadStatic]
        static short s;
    }
}
