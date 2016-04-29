// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

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
        int i;
        string s;
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
}
