//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

// Implementation of Redhawk PAL inline functions

EXTERN_C long __cdecl _InterlockedIncrement(long volatile *);
#pragma intrinsic(_InterlockedIncrement)
FORCEINLINE Int32 PalInterlockedIncrement(_Inout_ _Interlocked_operand_ Int32 volatile *pDst)
{
    return _InterlockedIncrement((long volatile *)pDst);
}

EXTERN_C long __cdecl _InterlockedDecrement(long volatile *);
#pragma intrinsic(_InterlockedDecrement)
FORCEINLINE Int32 PalInterlockedDecrement(_Inout_ _Interlocked_operand_ Int32 volatile *pDst)
{
    return _InterlockedDecrement((long volatile *)pDst);
}

EXTERN_C long _InterlockedOr(long volatile *, long);
#pragma intrinsic(_InterlockedOr)
FORCEINLINE UInt32 PalInterlockedOr(_Inout_ _Interlocked_operand_ UInt32 volatile *pDst, UInt32 iValue)
{
    return _InterlockedOr((long volatile *)pDst, iValue);
}

EXTERN_C long _InterlockedAnd(long volatile *, long);
#pragma intrinsic(_InterlockedAnd)
FORCEINLINE UInt32 PalInterlockedAnd(_Inout_ _Interlocked_operand_ UInt32 volatile *pDst, UInt32 iValue)
{
    return _InterlockedAnd((long volatile *)pDst, iValue);
}

EXTERN_C long __PN__MACHINECALL_CDECL_OR_DEFAULT _InterlockedExchange(long volatile *, long);
#pragma intrinsic(_InterlockedExchange)
FORCEINLINE Int32 PalInterlockedExchange(_Inout_ _Interlocked_operand_ Int32 volatile *pDst, Int32 iValue)
{
    return _InterlockedExchange((long volatile *)pDst, iValue);
}

EXTERN_C long __PN__MACHINECALL_CDECL_OR_DEFAULT _InterlockedCompareExchange(long volatile *, long, long);
#pragma intrinsic(_InterlockedCompareExchange)
FORCEINLINE Int32 PalInterlockedCompareExchange(_Inout_ _Interlocked_operand_ Int32 volatile *pDst, Int32 iValue, Int32 iComperand)
{
    return _InterlockedCompareExchange((long volatile *)pDst, iValue, iComperand);
}

EXTERN_C Int64 _InterlockedCompareExchange64(Int64 volatile *, Int64, Int64);
#pragma intrinsic(_InterlockedCompareExchange64)
FORCEINLINE Int64 PalInterlockedCompareExchange64(_Inout_ _Interlocked_operand_ Int64 volatile *pDst, Int64 iValue, Int64 iComperand)
{
    return _InterlockedCompareExchange64(pDst, iValue, iComperand);
}

#if defined(_AMD64_)
EXTERN_C UInt8 _InterlockedCompareExchange128(Int64 volatile *, Int64, Int64, Int64 *);
#pragma intrinsic(_InterlockedCompareExchange128)
FORCEINLINE UInt8 PalInterlockedCompareExchange128(_Inout_ _Interlocked_operand_ Int64 volatile *pDst, Int64 iValueHigh, Int64 iValueLow, Int64 *pComperand)
{
    return _InterlockedCompareExchange128(pDst, iValueHigh, iValueLow, pComperand);
}
#endif // _AMD64_

#ifdef BIT64

EXTERN_C void * _InterlockedExchangePointer(void * volatile *, void *);
#pragma intrinsic(_InterlockedExchangePointer)
FORCEINLINE void * PalInterlockedExchangePointer(_Inout_ _Interlocked_operand_ void * volatile *pDst, _In_ void *pValue)
{
    return _InterlockedExchangePointer((void * volatile *)pDst, pValue);
}

EXTERN_C void * _InterlockedCompareExchangePointer(void * volatile *, void *, void *);
#pragma intrinsic(_InterlockedCompareExchangePointer)
FORCEINLINE void * PalInterlockedCompareExchangePointer(_Inout_ _Interlocked_operand_ void * volatile *pDst, _In_ void *pValue, _In_ void *pComperand)
{
    return _InterlockedCompareExchangePointer((void * volatile *)pDst, pValue, pComperand);
}

#else // BIT64

#define PalInterlockedExchangePointer(_pDst, _pValue) \
    ((void *)_InterlockedExchange((long volatile *)(_pDst), (long)(size_t)(_pValue)))

#define PalInterlockedCompareExchangePointer(_pDst, _pValue, _pComperand) \
    ((void *)_InterlockedCompareExchange((long volatile *)(_pDst), (long)(size_t)(_pValue), (long)(size_t)(_pComperand)))

#endif // BIT64

