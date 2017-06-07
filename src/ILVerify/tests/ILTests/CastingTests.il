// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly Casting
{
}

.class public auto ansi beforefieldinit CastingTestsType
       extends [System.Runtime]System.Object
{
    // Array<T> tests
    .method static public hidebysig void Casting.ArrayOfTToEnumerableOfT_Valid(int32[] test) cil managed
    {
        .locals init (
            class [System.Runtime]System.Collections.Generic.IEnumerable`1<int32> V_0
        )
        ldarg.0
        stloc.0
        ret
    }

    .method static public hidebysig void Casting.ArrayOfTToEnumerable_Valid(int32[] test) cil managed
    {
        .locals init (
            class [System.Runtime]System.Collections.IEnumerable V_0
        )
        ldarg.0
        stloc.0
        ret
    }

    .method static public hidebysig void Casting.ArrayOfTToICollectionOfT_Valid(int32[] test) cil managed
    {
        .locals init (
            class [System.Runtime]System.Collections.Generic.ICollection`1<int32> V_0
        )
        ldarg.0
        stloc.0
        ret
    }

    .method static public hidebysig void Casting.ArrayOfTToICollection_Valid(int32[] test) cil managed
    {
        .locals init (
            class [System.Runtime]System.Collections.ICollection V_0
        )
        ldarg.0
        stloc.0
        ret
    }

    .method static public hidebysig void Casting.ArrayOfTToIListOfT_Valid(int32[] test) cil managed
    {
        .locals init (
            class [System.Runtime]System.Collections.Generic.IList`1<int32> V_0
        )
        ldarg.0
        stloc.0
        ret
    }

    .method static public hidebysig void Casting.ArrayOfTToIList_Valid(int32[] test) cil managed
    {
        .locals init (
            class [System.Runtime]System.Collections.IList V_0
        )
        ldarg.0
        stloc.0
        ret
    }

    .method static public hidebysig void Casting.ArrayOfTToArray_Valid(int32[] test) cil managed
    {
        .locals init (
            class [System.Runtime]System.Array V_0
        )
        ldarg.0
        stloc.0
        ret
    }
}
