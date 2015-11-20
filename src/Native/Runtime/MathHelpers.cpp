//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "assert.h"

//
// Floating point and 64-bit integer math helpers.
//

EXTERN_C REDHAWK_API Int32 REDHAWK_CALLCONV RhpDbl2IntOvf(double val, Boolean* pShouldThrow)
{
    const double two31 = 2147483648.0;
    *pShouldThrow = Boolean_false;

        // Note that this expression also works properly for val = NaN case
    if (val > -two31 - 1 && val < two31)
        return((int)val);

    *pShouldThrow = Boolean_true;
    return 0;
}

EXTERN_C REDHAWK_API Int64 REDHAWK_CALLCONV RhpDbl2LngOvf(double val, Boolean* pShouldThrow)
{
    const double two63  = 2147483648.0 * 4294967296.0;
    *pShouldThrow = Boolean_false;

        // Note that this expression also works properly for val = NaN case
        // We need to compare with the very next double to two63. 0x402 is epsilon to get us there.
    if (val > -two63 - 0x402 && val < two63)
        return((Int64)val);

    *pShouldThrow = Boolean_true;
    return 0;
}

EXTERN_C REDHAWK_API UInt64 REDHAWK_CALLCONV RhpDbl2ULngOvf(double val, Boolean* pShouldThrow)
{
    const double two64  = 2.0* 2147483648.0 * 4294967296.0;
    *pShouldThrow = Boolean_false;

    // Note that this expression also works properly for val = NaN case
    if (val < two64)
        return((Int64)val);

    *pShouldThrow = Boolean_true;
    return 0;
}

EXTERN_C REDHAWK_API Int32 REDHAWK_CALLCONV RhpFlt2IntOvf(float val, Boolean* pShouldThrow)
{
    const double two31 = 2147483648.0;
    *pShouldThrow = Boolean_false;

        // Note that this expression also works properly for val = NaN case
    if (val > -two31 - 1 && val < two31)
        return((int)val);

    *pShouldThrow = Boolean_true;
    return 0;
}

EXTERN_C REDHAWK_API Int64 REDHAWK_CALLCONV RhpFlt2LngOvf(float val, Boolean* pShouldThrow)
{
    const double two63  = 2147483648.0 * 4294967296.0;
    *pShouldThrow = Boolean_false;

        // Note that this expression also works properly for val = NaN case
        // We need to compare with the very next double to two63. 0x402 is epsilon to get us there.
    if (val > -two63 - 0x402 && val < two63)
        return((Int64)val);

    *pShouldThrow = Boolean_true;
    return 0;
}

EXTERN_C REDHAWK_API UInt64 REDHAWK_CALLCONV RhpDbl2ULng(double val)
{
    return((UInt64)val);
}

#ifdef _ARM_
EXTERN_C REDHAWK_API Int32 REDHAWK_CALLCONV RhpIDiv(Int32 i, Int32 j)
{
    return i / j;
}

EXTERN_C REDHAWK_API Int32 REDHAWK_CALLCONV RhpIMod(Int32 i, Int32 j)
{
    return i % j;
}

EXTERN_C REDHAWK_API UInt32 REDHAWK_CALLCONV RhpUDiv(UInt32 i, UInt32 j)
{
    return i / j;
}

EXTERN_C REDHAWK_API UInt32 REDHAWK_CALLCONV RhpUMod(UInt32 i, UInt32 j)
{
    return i % j;
}

EXTERN_C REDHAWK_API Int64 REDHAWK_CALLCONV RhpLMul(Int64 i, Int64 j)
{
    return i * j;
}

EXTERN_C REDHAWK_API Int64 REDHAWK_CALLCONV RhpLDiv(Int64 i, Int64 j)
{
    return i / j;
}

EXTERN_C REDHAWK_API Int64 REDHAWK_CALLCONV RhpLMod(Int64 i, Int64 j)
{
    return i % j;
}

EXTERN_C REDHAWK_API UInt64 REDHAWK_CALLCONV RhpULMul(UInt64 i, UInt64 j)
{
    return i * j;
}

EXTERN_C REDHAWK_API UInt64 REDHAWK_CALLCONV RhpULDiv(UInt64 i, UInt64 j)
{
    return i / j;
}

EXTERN_C REDHAWK_API UInt64 REDHAWK_CALLCONV RhpULMod(UInt64 i, UInt64 j)
{
    return i % j;
}

EXTERN_C REDHAWK_API Int64 REDHAWK_CALLCONV RhpDbl2Lng(double val)
{
    return (Int64)val;
}

EXTERN_C REDHAWK_API double REDHAWK_CALLCONV RhpLng2Dbl(Int64 val)
{
    return (double)val;
}

EXTERN_C REDHAWK_API double REDHAWK_CALLCONV RhpULng2Dbl(UInt64 val)
{
    return (double)val;
}

#endif // _ARM_

// int/uint divide and remainder for ARM
//
// All the rest are X86 and ARM only
//
#if defined(_X86_) || defined(_ARM_)
//
// helper macro to multiply two 32-bit uints
//
#define Mul32x32To64(a, b)  ((UInt64)((UInt32)(a)) * (UInt64)((UInt32)(b)))

//
// helper macro to get high 32-bit of 64-bit int
//
#define Hi32Bits(a)         ((UInt32)((UInt64)(a) >> 32))

EXTERN_C REDHAWK_API Int64 REDHAWK_CALLCONV RhpLMulOvf(Int64 i, Int64 j, Boolean* pShouldThrow)
{
    Int64 ret;
    *pShouldThrow = Boolean_false;

        // Remember the sign of the result
    Int32 sign = Hi32Bits(i) ^ Hi32Bits(j);

        // Convert to unsigned multiplication
    if (i < 0) i = -i;
    if (j < 0) j = -j;

        // Get the upper 32 bits of the numbers
    UInt32 val1High = Hi32Bits(i);
    UInt32 val2High = Hi32Bits(j);

    UInt64 valMid;

    if (val1High == 0) {
        // Compute the 'middle' bits of the long multiplication
        valMid = Mul32x32To64(val2High, i);
    }
    else {
        if (val2High != 0)
            goto ThrowExcep;
        // Compute the 'middle' bits of the long multiplication
        valMid = Mul32x32To64(val1High, j);
    }

        // See if any bits after bit 32 are set
    if (Hi32Bits(valMid) != 0)
        goto ThrowExcep;

    ret = Mul32x32To64(i, j) + (((UInt64)(UInt32)valMid) << 32);

    // check for overflow
    if (Hi32Bits(ret) < (UInt32)valMid)
        goto ThrowExcep;

    if (sign >= 0) {
        // have we spilled into the sign bit?
        if (ret < 0)
            goto ThrowExcep;
    }
    else {
        ret = -ret;
        // have we spilled into the sign bit?
        if (ret > 0)
            goto ThrowExcep;
    }
    return ret;

ThrowExcep:
    *pShouldThrow = Boolean_true;
    return 0;
}
EXTERN_C REDHAWK_API UInt64 REDHAWK_CALLCONV RhpULMulOvf(UInt64 i, UInt64 j, Boolean* pShouldThrow)
{
    Int64 ret;
    *pShouldThrow = Boolean_false;

        // Get the upper 32 bits of the numbers
    Int32 val1High = Hi32Bits(i);
    Int32 val2High = Hi32Bits(j);

    Int64 valMid;

    if (val1High == 0) {
        if (val2High == 0)
            return Mul32x32To64(i, j);
        // Compute the 'middle' bits of the long multiplication
        valMid = Mul32x32To64(val2High, i);
    }
    else {
        if (val2High != 0)
            goto ThrowExcep;
        // Compute the 'middle' bits of the long multiplication
        valMid = Mul32x32To64(val1High, j);
    }

        // See if any bits after bit 32 are set
    if (Hi32Bits(valMid) != 0)
        goto ThrowExcep;

    ret = Mul32x32To64(i, j) + (((UInt64)valMid) << 32);

    // check for overflow
    if (Hi32Bits(ret) < (UInt32)valMid)
        goto ThrowExcep;
    return ret;
    
ThrowExcep:
    *pShouldThrow = Boolean_true;
    return 0;
}

#endif // defined(_X86_) || defined(_ARM_)
