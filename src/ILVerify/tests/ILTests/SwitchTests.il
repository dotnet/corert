// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly SwitchTests
{
}

.class public auto ansi beforefieldinit SwitchTestsType
       extends [System.Runtime]System.Object
{
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

    .method static public hidebysig void Switch.IntoTry_Invalid_BranchIntoTry() cil managed
    {
        .maxstack 1
        .locals init (
            [0] int32
        )

        IL_0000: ldc.i4.s  10
        IL_0002: stloc.0

        IL_0003: ldloc.0
        IL_0004: switch    (IL_0046, IL_002B)

        IL_0029: br.s      IL_0046

        .try
        {
            IL_002B: nop
            IL_0035: br.s      IL_0041

            IL_0041: leave.s   IL_0046
        }
        catch [System.Runtime]System.Object
        {
            IL_0043: nop
            IL_0044: leave.s   IL_0046
        }

        IL_0046: ret
    }

    .method static public hidebysig void Switch.OutOfTry_Invalid_BranchOutOfTry() cil managed
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
            IL_0004: switch    (IL_0046, IL_002B)

            IL_0029: br.s      IL_0041

            IL_002B: nop
            IL_0035: br.s      IL_0041

            IL_0041: leave.s   IL_0046
        }
        catch [System.Runtime]System.Object
        {
            IL_0043: nop
            IL_0044: leave.s   IL_0046
        }

        IL_0046: ret
    }

    .method static public hidebysig void Switch.NestedOutOfTry_Invalid_BranchOutOfTry() cil managed
    {
        .maxstack 1
        .locals init (
            [0] int32
        )

        IL_0000: ldc.i4.s  10
        IL_0002: stloc.0
        .try
        {
            .try
            {
                IL_0003: ldloc.0
                IL_0004: switch    (IL_0014, IL_0007, IL_0023)
                
                IL_0006: br.s      IL_0014

                IL_0007: nop
                IL_0008: br.s      IL_0014

                IL_0014: leave.s   IL_0023
            }
            catch [System.Runtime]System.Object
            {
                IL_0016: pop
                IL_0021: leave.s   IL_0023
            }

            IL_0023: leave.s   IL_0032
        }
        catch [System.Runtime]System.Object
        {
            IL_0025: pop
            IL_0030: leave.s   IL_0032
        }

        IL_0032: ret
    }

    .method static public hidebysig void Switch.NestedIntoTry_Invalid_BranchIntoTry() cil managed
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
            IL_0004: switch    (IL_0023, IL_0007, IL_0009)

            IL_0006: br.s      IL_0023

            IL_0007: nop
            IL_0008: br.s      IL_0023

            .try
            {
                IL_0009: nop
                IL_0014: leave.s   IL_0023
            }
            catch [System.Runtime]System.Object
            {
                IL_0016: pop
                IL_0021: leave.s   IL_0023
            }

            IL_0023: leave.s   IL_0032
        }
        catch [System.Runtime]System.Object
        {
            IL_0025: pop
            IL_0030: leave.s   IL_0032
        }

        IL_0032: ret
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
