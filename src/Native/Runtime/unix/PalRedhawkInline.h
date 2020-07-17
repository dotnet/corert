// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Implementation of Redhawk PAL inline functions

#include <errno.h>

FORCEINLINE Int32 PalInterlockedIncrement(_Inout_ _Interlocked_operand_ Int32 volatile *pDst)
{
    return __sync_add_and_fetch(pDst, 1);
}

FORCEINLINE Int32 PalInterlockedDecrement(_Inout_ _Interlocked_operand_ Int32 volatile *pDst)
{
    return __sync_sub_and_fetch(pDst, 1);
}

FORCEINLINE UInt32 PalInterlockedOr(_Inout_ _Interlocked_operand_ UInt32 volatile *pDst, UInt32 iValue)
{
    return __sync_or_and_fetch(pDst, iValue);
}

FORCEINLINE UInt32 PalInterlockedAnd(_Inout_ _Interlocked_operand_ UInt32 volatile *pDst, UInt32 iValue)
{
    return __sync_and_and_fetch(pDst, iValue);
}

FORCEINLINE Int32 PalInterlockedExchange(_Inout_ _Interlocked_operand_ Int32 volatile *pDst, Int32 iValue)
{
    return __sync_swap(pDst, iValue);
}

FORCEINLINE Int64 PalInterlockedExchange64(_Inout_ _Interlocked_operand_ Int64 volatile *pDst, Int64 iValue)
{
    return __sync_swap(pDst, iValue);
}

FORCEINLINE Int32 PalInterlockedCompareExchange(_Inout_ _Interlocked_operand_ Int32 volatile *pDst, Int32 iValue, Int32 iComparand)
{
    return __sync_val_compare_and_swap(pDst, iComparand, iValue);
}

FORCEINLINE Int64 PalInterlockedCompareExchange64(_Inout_ _Interlocked_operand_ Int64 volatile *pDst, Int64 iValue, Int64 iComparand)
{
    return __sync_val_compare_and_swap(pDst, iComparand, iValue);
}

#if defined(HOST_AMD64) || defined(HOST_ARM64)
FORCEINLINE UInt8 PalInterlockedCompareExchange128(_Inout_ _Interlocked_operand_ Int64 volatile *pDst, Int64 iValueHigh, Int64 iValueLow, Int64 *pComparandAndResult)
{
    __int128_t iComparand = ((__int128_t)pComparandAndResult[1] << 64) + (UInt64)pComparandAndResult[0];
    __int128_t iResult = __sync_val_compare_and_swap((__int128_t volatile*)pDst, iComparand, ((__int128_t)iValueHigh << 64) + (UInt64)iValueLow);
    pComparandAndResult[0] = (Int64)iResult; pComparandAndResult[1] = (Int64)(iResult >> 64);
    return iComparand == iResult;
}
#endif // HOST_AMD64

#ifdef HOST_64BIT

#define PalInterlockedExchangePointer(_pDst, _pValue) \
    ((void *)PalInterlockedExchange64((Int64 volatile *)(_pDst), (Int64)(size_t)(_pValue)))

#define PalInterlockedCompareExchangePointer(_pDst, _pValue, _pComparand) \
    ((void *)PalInterlockedCompareExchange64((Int64 volatile *)(_pDst), (Int64)(size_t)(_pValue), (Int64)(size_t)(_pComparand)))

#else // HOST_64BIT

#define PalInterlockedExchangePointer(_pDst, _pValue) \
    ((void *)PalInterlockedExchange((Int32 volatile *)(_pDst), (Int32)(size_t)(_pValue)))

#define PalInterlockedCompareExchangePointer(_pDst, _pValue, _pComparand) \
    ((void *)PalInterlockedCompareExchange((Int32 volatile *)(_pDst), (Int32)(size_t)(_pValue), (Int32)(size_t)(_pComparand)))

#endif // HOST_64BIT


FORCEINLINE void PalYieldProcessor()
{
#if defined(HOST_X86) || defined(HOST_AMD64)
    __asm__ __volatile__(
        "rep\n"
        "nop"
        );
#endif
}

FORCEINLINE void PalMemoryBarrier()
{
    __sync_synchronize();
}

#define PalDebugBreak() abort()

FORCEINLINE Int32 PalGetLastError()
{
    return errno;
}

FORCEINLINE void PalSetLastError(Int32 error)
{
    errno = error;
}
