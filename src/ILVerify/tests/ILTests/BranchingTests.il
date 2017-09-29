// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly Branching
{
}

// Custom Comparer class implementing IEqualityComparer for testing
.class private auto ansi beforefieldinit CustomComparer
    extends [System.Runtime]System.Object
    implements [System.Runtime]System.Collections.IEqualityComparer
{
    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        ldarg.0
        call      instance void [System.Runtime]System.Object::.ctor()
        ret
    }

    .method public final hidebysig newslot virtual 
        instance bool Equals (
            object x,
            object y
        ) cil managed 
    {
        ldc.i4.0
        ret
    }

    .method public final hidebysig newslot virtual 
        instance int32 GetHashCode (
            object obj
        ) cil managed 
    {
        ldarg.1
        callvirt  instance int32 [System.Runtime]System.Object::GetHashCode()
        ret
    }
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

    .method static public hidebysig void Branching.NullConditional_Invalid_StackUnexpected() cil managed
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

    .method static public hidebysig void Branching.UnequalStackMerge_Valid(class [System.Runtime]System.Collections.IEqualityComparer equalityComparer,
        class CustomComparer customComparer) cil managed
    {
        .maxstack 1
        .locals init(
            [0] class [System.Runtime]System.Collections.IEqualityComparer
        )

        ldc.i4.0
        brfalse.s   lbl_cComp

        ldarg.0
        br.s        lbl_store

    lbl_cComp:
        ldarg.1

    lbl_store:
        stloc.0
        ret
    }

    .method static public hidebysig void Branching.UnequalStackMergeConstrained_Valid<([System.Runtime]System.Collections.IEqualityComparer) T>
        (class [System.Runtime]System.Collections.IEqualityComparer equalityComparer, !!T customComparer) cil managed
    {
        .maxstack 1
        .locals init(
            [0] class [System.Runtime]System.Collections.IEqualityComparer
        )

        ldc.i4.0
        brfalse.s   lbl_cComp

        ldarg.0
        br.s        lbl_store

    lbl_cComp:
        ldarg.1
        box     !!T

    lbl_store:
        stloc.0
        ret
    }

    .method static public hidebysig void Branching.UnequalStackMergeArrays_Valid(class CustomComparer[] comparers,
        class [System.Runtime]System.Collections.IEqualityComparer[] customComparer) cil managed
    {
        .maxstack 1
        .locals init(
            [0] class [System.Runtime]System.Collections.IEqualityComparer[]
        )

        ldarg.0
        brfalse.s   lbl_cComp

        ldarg.0
        br.s        lbl_store

    lbl_cComp:
        ldarg.1

    lbl_store:
        stloc.0
        ret
    }

    .method static public hidebysig void Branching.UnequalStackMergeArrays_Invalid_StackUnexpected(class CustomComparer[] comparers,
        class [System.Runtime]System.Collections.IEqualityComparer[] customComparer) cil managed
    {
        .maxstack 1
        .locals init(
            [0] class CustomComparer[]
        )

        ldarg.0
        brfalse.s   lbl_cComp

        ldarg.0
        br.s        lbl_store

    lbl_cComp:
        ldarg.1

    lbl_store:
        stloc.0
        ret
    }

    .method static public hidebysig void Branching.UnequalStackMergeGeneric_Invalid_PathStackUnexpected<T>(!!T generic,
        object obj) cil managed
    {
        .maxstack 1
        .locals init(
            [0] object obj
        )

        ldarg.1
        brfalse.s   lbl_gen

        ldarg.1
        br.s        lbl_store

    lbl_gen:
        ldarg.0

    lbl_store:
        stloc.0
        ret
    }

    .method static public hidebysig void Branching.UnequalStackMergeGenericArrayConstrain_Valid<(int32[]) T>(!!T generic,
        int32[] arr) cil managed
    {
        .maxstack 1
        .locals init(
            [0] int32[] dest
        )

        ldarg.1
        brfalse.s   lbl_arr

        ldarg.0
        box     !!T
        br.s        lbl_store

    lbl_arr:
        ldarg.1

    lbl_store:
        stloc.0
        ret
    }

    .method static public hidebysig void Branching.InsideTry_Valid() cil managed
    {
        .maxstack 2

        .try
        {
            ldc.i4.s  10
            ldc.i4.s  5
            bne.un.s  lbl_true

            nop
            br.s      lbl_leave

            lbl_true: nop

            lbl_leave: leave.s   lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.OutOfTry_Invalid_BranchOutOfTry() cil managed
    {
        .maxstack 2

        .try
        {
            ldc.i4.s  10
            ldc.i4.s  5
            bne.un.s  lbl_true

            nop
            br.s      lbl_ret

            lbl_true: nop

            lbl_leave: leave.s   lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.IntoTryStart_Valid() cil managed
    {
        .maxstack 2

        ldc.i4.s  10
        ldc.i4.s  5
        bne.un.s  lbl_true

        nop
        br.s      lbl_ret

        .try
        {
            lbl_true: nop

            leave.s   lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.IntoTryMiddle_Invalid_BranchIntoTry() cil managed
    {
        .maxstack 2

        ldc.i4.s  10
        ldc.i4.s  5
        bne.un.s  lbl_true

        nop
        br.s      lbl_ret

        .try
        {
            nop
            lbl_true: nop

            leave.s   lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.IntoNestedTryStart_Valid() cil managed
    {
        .maxstack 2

        .try
        {
            ldc.i4.s  10
            ldc.i4.s  5
            bne.un.s  lbl_true

            nop
            br.s      lbl_leave
            .try
            {
                lbl_true: nop

                leave.s   lbl_leave
            }
            catch [System.Runtime]System.Object
            {
                pop
                leave.s   lbl_leave
            }

            lbl_leave: leave.s lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.IntoDoubleNestedTryStart_Invalid_BranchIntoTry() cil managed
    {
        .try
        {
            nop
            br    Inner

            .try
            {
                nop

                .try
                {
                    Inner:
                    leave.s    A
                }
                catch [System.Runtime]System.Object 
                {
                    pop
                    leave.s    A
                }
                A:

                leave.s    B
            }
            catch [System.Runtime]System.Object 
            {
                pop
                leave.s    B
            }
            B:

            leave.s    C
        }
        catch [System.Runtime]System.Object 
        {
            pop
            leave.s    C
        }
        C:

        ret
    }

    .method static public hidebysig void Branching.IntoDoubleNestedTryStartStart_Valid() cil managed
    {
        .try
        {
            nop
            br    Inner

            .try
            {
                .try
                {
                    Inner:
                    leave.s    A
                }
                catch [System.Runtime]System.Object 
                {
                    pop
                    leave.s    A
                }
                A:

                leave.s    B
            }
            catch [System.Runtime]System.Object 
            {
                pop
                leave.s    B
            }
            B:

            leave.s    C
        }
        catch [System.Runtime]System.Object 
        {
            pop
            leave.s    C
        }
        C:

        ret
    }

    .method static public hidebysig void Branching.IntoDoubleNestedTryStartFromOutside_Invalid_BranchIntoTry() cil managed
    {
        br    Inner

        .try
        {
            nop

            .try
            {
                Inner:
                leave.s    A
            }
            catch [System.Runtime]System.Object 
            {
                pop
                leave.s    A
            }
            A:

            leave.s    B
        }
        catch [System.Runtime]System.Object 
        {
            pop
            leave.s    B
        }
        B:

        ret
    }

    .method static public hidebysig void Branching.IntoTryStartWithPredecessor_Valid() cil managed
    {
        .try
        {
            nop
            br    Inner

            .try
            {
                leave.s    A
            }
            catch [System.Runtime]System.Object 
            {
                pop
                leave.s    A
            }

            .try
            {
                Inner:
                leave.s    A
            }
            catch [System.Runtime]System.Object 
            {
                pop
                leave.s    A
            }

            A:

            leave.s    C
        }
        catch [System.Runtime]System.Object 
        {
            pop
            leave.s    C
        }
        C:

        ret
    }

    .method static public hidebysig void Branching.IntoNestedTryMiddle_Invalid_BranchIntoTry() cil managed
    {
        .maxstack 2

        .try
        {
            ldc.i4.s  10
            ldc.i4.s  5
            bne.un.s  lbl_true

            nop
            br.s      lbl_leave
            .try
            {
                nop
                lbl_true: nop

                leave.s   lbl_leave
            }
            catch [System.Runtime]System.Object
            {
                pop
                leave.s   lbl_leave
            }

            lbl_leave: leave.s   lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.FromTryIntoCatch_Invalid_BranchOutOfTry.BranchIntoHandler() cil managed
    {
        .maxstack 2

        .try
        {
            ldc.i4.s  10
            ldc.i4.s  5
            bne.un.s  lbl_true

            nop
            leave.s   lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            lbl_true: nop
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.FromTryIntoFilter_Invalid_BranchOutOfTry.BranchIntoFilter() cil managed
    {
        .try
        {
            br.s  lbl_filter
        }
        filter
        {
            lbl_filter: pop
            ldc.i4.0
            endfilter
        } 
        { 
            pop
            leave.s lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.FromTryIntoFinally_Invalid_BranchOutOfTry.BranchIntoHandler() cil managed
    {
        .maxstack 2

        .try
        {
            ldc.i4.s  10
            ldc.i4.s  5
            bne.un.s  lbl_finally

            nop
            leave.s   lbl_ret
        }
        finally
        {
            lbl_finally: nop
            endfinally
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.FromTryIntoOtherTryStart_Invalid_BranchOutOfTry() cil managed
    {
        .maxstack 2

        .try
        {
            ldc.i4.s  10
            ldc.i4.s  5
            bne.un.s  lbl_try2

            nop
            leave.s   lbl_try
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_try
        }

        lbl_try: nop
        .try
        {
            lbl_try2: nop
            leave.s   lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }

    .method static public hidebysig void Branching.OutOfTrySameStart_Invalid_BranchOutOfTry() cil managed
    {
        .try
        {
            .try
            {
                br    A

                leave.s    A
            }
            catch [System.Runtime]System.Object 
            {
                pop
                leave.s    A
            }
            A:

            leave.s    B
        }
        catch [System.Runtime]System.Object 
        {
            pop
            leave.s    B
        }
        B:

        ret
    }

    .method static public hidebysig void Branching.FromTryIntoOtherTryMiddle_Invalid_BranchOutOfTry() cil managed
    {
        .maxstack 2

        .try
        {
            ldc.i4.s  10
            ldc.i4.s  5
            bne.un.s  lbl_try2

            nop
            leave.s   lbl_try
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_try
        }

        lbl_try: nop
        .try
        {
            nop
            lbl_try2: leave.s   lbl_ret
        }
        catch [System.Runtime]System.Object
        {
            pop
            leave.s   lbl_ret
        }

        lbl_ret: ret
    }
}
