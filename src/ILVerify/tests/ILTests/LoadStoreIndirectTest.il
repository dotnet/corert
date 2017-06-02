// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly LoadStoreIndirect
{
}

.class public auto ansi beforefieldinit LoadStoreIndirectTestsType
       extends [System.Runtime]System.Object
{
    .method static public void StoreIndirect.AssignToInt_Valid(int32&) cil managed
    {
        // arg = 0;
        ldarg.0
        ldc.i4.0
        stind.i4
        ret
    }

    .method static public void StoreIndirect.AssignToRefBase_Valid(object&) cil managed
    {
        .locals init (string V_0)

        // ref object a;
        // string b;
        // a = b;

        ldarg.0
        ldloc.0
        stind.ref
        ret
    }

    .method static public void StoreIndirect.AssignToRefString_Invalid(string&) cil managed
    {
        .locals init (object V_0)

        // ref string x;
        // object y;
        // x = y;

        ldarg.0
        ldloc.0
        stind.ref
        ret
    }

    .method static public void  StoreObject_Valid() cil managed
    {
        .locals init (object V_0)

        ldloca.s   V_0
        ldstr      "Hello"
        stobj string
        ret
    }

    .method static public void  StoreObject_Invalid() cil managed
    {
        .maxstack  2
        .locals init (object V_0)

        ldloca.s   V_0
        ldstr      "Hello"
        stobj  [mscorlib]System.IO.Stream
        ret
    }
}
