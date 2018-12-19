// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "daccess.h"

#define THUMB_CODE 1

template <typename ResultType, typename SourceType>
inline ResultType DataPointerToThumbCode(SourceType pCode)
{
    return (ResultType)(((uintptr_t)pCode) | THUMB_CODE);
}

template <typename ResultType, typename SourceType>
inline ResultType ThumbCodeToDataPointer(SourceType pCode)
{
    return (ResultType)(((uintptr_t)pCode) & ~THUMB_CODE);
}

// Convert from a PCODE to the corresponding PINSTR.  On many architectures this will be the identity function;
// on ARM, this will mask off the THUMB bit.
inline TADDR PCODEToPINSTR(PCODE pc)
{
#ifdef _TARGET_ARM_
    return ThumbCodeToDataPointer<TADDR,PCODE>(pc);
#else
    return dac_cast<PCODE>(pc);
#endif
}

// Convert from a PINSTR to the corresponding PCODE.  On many architectures this will be the identity function;
// on ARM, this will raise the THUMB bit.
inline PCODE PINSTRToPCODE(TADDR addr)
{
#ifdef _TARGET_ARM_
    return DataPointerToThumbCode<PCODE,TADDR>(addr);
#else
    return dac_cast<PCODE>(addr);
#endif
}