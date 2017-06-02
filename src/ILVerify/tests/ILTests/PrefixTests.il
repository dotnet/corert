// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly Prefix 
{
}

.class public auto ansi beforefieldinit PrefixTestsType
       extends [System.Runtime]System.Object
{
    .method static public void Prefix.Readonly_Valid() cil managed 
    {
        .locals init (int32[] V_0)

        //int[] array = new int[2];
        ldc.i4.2
        newarr [System.Runtime]System.Int32
        stloc.0
        ldloc.0
        // array[1].ToString();
        ldc.i4.1
        readonly. 
        ldelema    [System.Runtime]System.Int32
        call       instance string [System.Runtime]System.Int32::ToString()
        ret
    }

    .method static public void Prefix.Readonly_Invalid_ReadOnly() cil managed 
    {
        .locals init (int32[] V_0)

        //int[] array = new int[2];
        ldc.i4.2
        newarr [System.Runtime]System.Int32
        stloc.0
        ldloc.0
        ldc.i4.1
        
        readonly. 
        ldelem    [System.Runtime]System.Int32
        ret
    }

    .method static public void Prefix.PrefixUnalignedAndVolatile_Valid() cil managed 
    {
        .locals init (int32& V_0, int32 V_1)
        // ref int x;
        // int y;
        // y = *x;
        ldloc.0
        volatile.
        unaligned. 4
        ldind.i4
        stloc.1
        ret
    }
}
