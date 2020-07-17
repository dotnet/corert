// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
FORCEINLINE Int32 PalInterlockedCompareExchange(_Inout_ _Interlocked_operand_ Int32 volatile *pDst, Int32 iValue, Int32 iComparand)
{
    return _InterlockedCompareExchange((long volatile *)pDst, iValue, iComparand);
}

EXTERN_C Int64 _InterlockedCompareExchange64(Int64 volatile *, Int64, Int64);
#pragma intrinsic(_InterlockedCompareExchange64)
FORCEINLINE Int64 PalInterlockedCompareExchange64(_Inout_ _Interlocked_operand_ Int64 volatile *pDst, Int64 iValue, Int64 iComparand)
{
    return _InterlockedCompareExchange64(pDst, iValue, iComparand);
}

#if defined(HOST_AMD64) || defined(HOST_ARM64)
EXTERN_C UInt8 _InterlockedCompareExchange128(Int64 volatile *, Int64, Int64, Int64 *);
#pragma intrinsic(_InterlockedCompareExchange128)
FORCEINLINE UInt8 PalInterlockedCompareExchange128(_Inout_ _Interlocked_operand_ Int64 volatile *pDst, Int64 iValueHigh, Int64 iValueLow, Int64 *pComparandAndResult)
{
    return _InterlockedCompareExchange128(pDst, iValueHigh, iValueLow, pComparandAndResult);
}
#endif // HOST_AMD64

#ifdef HOST_64BIT

EXTERN_C void * _InterlockedExchangePointer(void * volatile *, void *);
#pragma intrinsic(_InterlockedExchangePointer)
FORCEINLINE void * PalInterlockedExchangePointer(_Inout_ _Interlocked_operand_ void * volatile *pDst, _In_ void *pValue)
{
    return _InterlockedExchangePointer((void * volatile *)pDst, pValue);
}

EXTERN_C void * _InterlockedCompareExchangePointer(void * volatile *, void *, void *);
#pragma intrinsic(_InterlockedCompareExchangePointer)
FORCEINLINE void * PalInterlockedCompareExchangePointer(_Inout_ _Interlocked_operand_ void * volatile *pDst, _In_ void *pValue, _In_ void *pComparand)
{
    return _InterlockedCompareExchangePointer((void * volatile *)pDst, pValue, pComparand);
}

#else // HOST_64BIT

#define PalInterlockedExchangePointer(_pDst, _pValue) \
    ((void *)_InterlockedExchange((long volatile *)(_pDst), (long)(size_t)(_pValue)))

#define PalInterlockedCompareExchangePointer(_pDst, _pValue, _pComparand) \
    ((void *)_InterlockedCompareExchange((long volatile *)(_pDst), (long)(size_t)(_pValue), (long)(size_t)(_pComparand)))

#endif // HOST_64BIT

EXTERN_C __declspec(dllimport) unsigned long __stdcall GetLastError();
FORCEINLINE int PalGetLastError()
{
    return (int)GetLastError();
}

EXTERN_C __declspec(dllimport) void  __stdcall SetLastError(unsigned long error);
FORCEINLINE void PalSetLastError(int error)
{
    SetLastError((unsigned long)error);
}

#if defined(HOST_X86)

EXTERN_C void _mm_pause();
#pragma intrinsic(_mm_pause)
#define PalYieldProcessor() _mm_pause()

FORCEINLINE void PalMemoryBarrier()
{
    long Barrier;
    _InterlockedOr(&Barrier, 0);
}

#elif defined(HOST_AMD64)

EXTERN_C void _mm_pause();
#pragma intrinsic(_mm_pause)
#define PalYieldProcessor() _mm_pause()

EXTERN_C void __faststorefence();
#pragma intrinsic(__faststorefence)
#define PalMemoryBarrier() __faststorefence()


#elif defined(HOST_ARM)

EXTERN_C void __yield(void);
#pragma intrinsic(__yield)
EXTERN_C void __dmb(unsigned int _Type);
#pragma intrinsic(__dmb)
FORCEINLINE void PalYieldProcessor()
{
    __dmb(0xA /* _ARM_BARRIER_ISHST */);
    __yield();
}

#define PalMemoryBarrier() __dmb(0xF /* _ARM_BARRIER_SY */)

#elif defined(HOST_ARM64)

EXTERN_C void __yield(void);
#pragma intrinsic(__yield)
EXTERN_C void __dmb(unsigned int _Type);
#pragma intrinsic(__dmb)
FORCEINLINE void PalYieldProcessor()
{
    __dmb(0xA /* _ARM64_BARRIER_ISHST */);
    __yield();
}

#define PalMemoryBarrier() __dmb(0xF /* _ARM64_BARRIER_SY */)

#else
#error Unsupported architecture
#endif

#define PalDebugBreak() __debugbreak()
