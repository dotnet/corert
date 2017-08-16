// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly Branching
{
}

.class public auto ansi beforefieldinit BranchingTestsType
       extends [System.Runtime]System.Object
{
    .method static public hidebysig void Branching.NullConditional_Valid() cil managed
    {
        //object o = null;
        //Type t = o != null ? o.GetType() : null;
        //Type.GetTypeCode(t);

        .maxstack 1
        .locals init (
            [0] object o
        )

        IL_0000: ldnull
        IL_0001: stloc.0
        IL_0002: ldloc.0
        IL_0003: brtrue.s  IL_0008

        IL_0005: ldnull
        IL_0006: br.s      IL_000E

        IL_0008: ldloc.0
        IL_0009: callvirt  instance class [System.Runtime]System.Type [System.Runtime]System.Object::GetType()

        IL_000E: call      valuetype [System.Runtime]System.TypeCode [System.Runtime]System.Type::GetTypeCode(class [System.Runtime]System.Type)
        IL_0013: pop
        IL_0014: ret
    }

    .method static public hidebysig void Branching.NullConditional_Invalid_StackUnexpected.PathStackUnexpected() cil managed
    {
        //object o = null;
        //Type t = o != null ? o.GetType() : o;
        //Type.GetTypeCode(t);

        .maxstack 1
        .locals init (
            [0] object o
        )

        IL_0000: ldnull
        IL_0001: stloc.0
        IL_0002: ldloc.0
        IL_0003: brtrue.s  IL_0008

        IL_0005: ldloc.0
        IL_0006: br.s      IL_000E

        IL_0008: ldloc.0
        IL_0009: callvirt  instance class [System.Runtime]System.Type [System.Runtime]System.Object::GetType()

        IL_000E: call      valuetype [System.Runtime]System.TypeCode [System.Runtime]System.Type::GetTypeCode(class [System.Runtime]System.Type)
        IL_0013: pop
        IL_0014: ret
    }

    .method static public hidebysig void Switch.Int32Value_Valid() cil managed
    {
        .maxstack 1
        .locals init (
            [0] int32
        )

        IL_0000: ldc.i4.s 10
        IL_0001: stloc.0

        IL_0003: ldloc.0
        IL_0004: switch    (IL_0041, IL_002B, IL_002B, IL_002B, IL_002B, IL_0037, IL_0037, IL_0037)

        IL_0029: br.s      IL_0041

        IL_002B: nop
        IL_0035: br.s      IL_0041

        IL_0037: nop

        IL_0041: ret
    }

    .method static public hidebysig void Switch.Int64Value_Invalid_StackUnexpected() cil managed
    {
        .maxstack 1
        .locals init (
            [0] int64
        )

        IL_0000: ldc.i8 10
        IL_0001: stloc.0

        IL_0003: ldloc.0
        IL_0004: switch    (IL_0041, IL_002B, IL_002B, IL_002B, IL_002B, IL_0037, IL_0037, IL_0037)

        IL_0029: br.s      IL_0041

        IL_002B: nop
        IL_0035: br.s      IL_0041

        IL_0037: nop

        IL_0041: ret
    }

    .method static public hidebysig void Switch.InsideTry_Valid() cil managed
    {
        .maxstack 1
        .locals init (
            [0] int32
        )

        IL_0000: ldc.i4.s  10
        IL_0002: stloc.0

        .try
        {
            IL_0003: ldloc.0
            IL_0004: switch    (IL_0041, IL_002B, IL_002B, IL_002B, IL_002B, IL_0037, IL_0037, IL_0037)

            IL_0029: br.s      IL_0041

            IL_002B: nop
            IL_0035: br.s      IL_0041

            IL_0037: nop

            IL_0041: leave.s   IL_0046
        }
        catch [System.Runtime]System.Object
        {
            IL_0043: nop
            IL_0044: leave.s   IL_0046
        }

        IL_0046: ret
    }

    .method static public hidebysig void Switch.ObjectValue_Invalid_StackUnexpected() cil managed
    {
        .maxstack 1
        .locals init (
            [0] object
        )

        IL_0000: ldnull
        IL_0001: stloc.0

        IL_0003: ldloc.0
        IL_0004: switch    (IL_0041, IL_002B, IL_002B, IL_002B, IL_002B, IL_0037, IL_0037, IL_0037)

        IL_0029: br.s      IL_0041

        IL_002B: nop
        IL_0035: br.s      IL_0041

        IL_0037: nop

        IL_0041: ret
    }
}
