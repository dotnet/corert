// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "rhassert.h"

//
// Floating point and 64-bit integer math helpers.
//

EXTERN_C REDHAWK_API UInt64 REDHAWK_CALLCONV RhpDbl2ULng(double val)
{
    return((UInt64)val);
}

// CORERT Specific - on Project N the arguments to these helpers are inverted
#ifdef CORERT
#include <cmath>

EXTERN_C REDHAWK_API float REDHAWK_CALLCONV RhpFltRem(float dividend, float divisor)
{
    //
    // From the ECMA standard:
    //
    // If [divisor] is zero or [dividend] is infinity
    //   the result is NaN.
    // If [divisor] is infinity,
    //   the result is [dividend] (negated for -infinity***).
    //
    // ***"negated for -infinity" has been removed from the spec
    //

    if (divisor==0 || !std::isfinite(dividend))
    {
        return -nanf(0);
    }
    else if (!std::isfinite(divisor) && !std::isnan(divisor))
    {
        return dividend;
    }
    // else...
    return fmodf(dividend,divisor);
}

EXTERN_C REDHAWK_API double REDHAWK_CALLCONV RhpDblRem(double dividend, double divisor)
{
    //
    // From the ECMA standard:
    //
    // If [divisor] is zero or [dividend] is infinity
    //   the result is NaN.
    // If [divisor] is infinity,
    //   the result is [dividend] (negated for -infinity***).
    //
    // ***"negated for -infinity" has been removed from the spec
    //
    if (divisor==0 || !std::isfinite(dividend))
    {
        return -nan(0);
    }
    else if (!std::isfinite(divisor) && !std::isnan(divisor))
    {
        return dividend;
    }
    // else...
    return(fmod(dividend,divisor));
}
#endif // CORERT

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
