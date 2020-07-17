// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#include "common.h"
#ifndef DACCESS_COMPILE
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "rhassert.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "shash.h"
#include "RWLock.h"
#include "TypeManager.h"
#include "varint.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "holder.h"
#include "Crst.h"
#include "RuntimeInstance.h"
#include "event.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "threadstore.h"
#include "threadstore.inl"
#include "stressLog.h"
#include "rhbinder.h"
#include "eetype.h"
#include "eetype.inl"

COOP_PINVOKE_HELPER(Boolean, RhpEHEnumInitFromStackFrameIterator, (
    StackFrameIterator* pFrameIter, void ** pMethodStartAddressOut, EHEnum* pEHEnum))
{
    ICodeManager * pCodeManager = pFrameIter->GetCodeManager();
    pEHEnum->m_pCodeManager = pCodeManager;

    return pCodeManager->EHEnumInit(pFrameIter->GetMethodInfo(), pMethodStartAddressOut, &pEHEnum->m_state);
}

COOP_PINVOKE_HELPER(Boolean, RhpEHEnumNext, (EHEnum* pEHEnum, EHClause* pEHClause))
{
    return pEHEnum->m_pCodeManager->EHEnumNext(&pEHEnum->m_state, pEHClause);
}

// Unmanaged helper to locate one of two classlib-provided functions that the runtime needs to 
// implement throwing of exceptions out of Rtm, and fail-fast. This may return NULL if the classlib
// found via the provided address does not have the necessary exports.
COOP_PINVOKE_HELPER(void *, RhpGetClasslibFunctionFromCodeAddress, (void * address, ClasslibFunctionId functionId))
{
    return GetRuntimeInstance()->GetClasslibFunctionFromCodeAddress(address, functionId);
}

// Unmanaged helper to locate one of two classlib-provided functions that the runtime needs to 
// implement throwing of exceptions out of Rtm, and fail-fast. This may return NULL if the classlib
// found via the provided address does not have the necessary exports.
COOP_PINVOKE_HELPER(void *, RhpGetClasslibFunctionFromEEType, (EEType * pEEType, ClasslibFunctionId functionId))
{
    return pEEType->GetTypeManagerPtr()->AsTypeManager()->GetClasslibFunction(functionId);
}

COOP_PINVOKE_HELPER(void, RhpValidateExInfoStack, ())
{
    Thread * pThisThread = ThreadStore::GetCurrentThread();
    pThisThread->ValidateExInfoStack();
}

COOP_PINVOKE_HELPER(void, RhpClearThreadDoNotTriggerGC, ())
{
    Thread * pThisThread = ThreadStore::GetCurrentThread();

    if (!pThisThread->IsDoNotTriggerGcSet())
        RhFailFast();

    pThisThread->ClearDoNotTriggerGc();
}

COOP_PINVOKE_HELPER(void, RhpSetThreadDoNotTriggerGC, ())
{
    Thread * pThisThread = ThreadStore::GetCurrentThread();

    if (pThisThread->IsDoNotTriggerGcSet())
        RhFailFast();

    pThisThread->SetDoNotTriggerGc();
}

COOP_PINVOKE_HELPER(Int32, RhGetModuleFileName, (HANDLE moduleHandle, _Out_ const TCHAR** pModuleNameOut))
{
    return PalGetModuleFileName(pModuleNameOut, moduleHandle);
}

COOP_PINVOKE_HELPER(void, RhpCopyContextFromExInfo, (void * pOSContext, Int32 cbOSContext, PAL_LIMITED_CONTEXT * pPalContext))
{
    UNREFERENCED_PARAMETER(cbOSContext);
    ASSERT(cbOSContext >= sizeof(CONTEXT));
    CONTEXT* pContext = (CONTEXT *)pOSContext;
#if defined(UNIX_AMD64_ABI)
    pContext->Rip = pPalContext->IP;
    pContext->Rsp = pPalContext->Rsp;
    pContext->Rbp = pPalContext->Rbp;
    pContext->Rdx = pPalContext->Rdx;
    pContext->Rax = pPalContext->Rax;
    pContext->Rbx = pPalContext->Rbx;
    pContext->R12 = pPalContext->R12;
    pContext->R13 = pPalContext->R13;
    pContext->R14 = pPalContext->R14;
    pContext->R15 = pPalContext->R15;
#elif defined(HOST_AMD64)
    pContext->Rip = pPalContext->IP;
    pContext->Rsp = pPalContext->Rsp;
    pContext->Rbp = pPalContext->Rbp;
    pContext->Rdi = pPalContext->Rdi;
    pContext->Rsi = pPalContext->Rsi;
    pContext->Rax = pPalContext->Rax;
    pContext->Rbx = pPalContext->Rbx;
    pContext->R12 = pPalContext->R12;
    pContext->R13 = pPalContext->R13;
    pContext->R14 = pPalContext->R14;
    pContext->R15 = pPalContext->R15;
#elif defined(HOST_X86)
    pContext->Eip = pPalContext->IP;
    pContext->Esp = pPalContext->Rsp;
    pContext->Ebp = pPalContext->Rbp;
    pContext->Edi = pPalContext->Rdi;
    pContext->Esi = pPalContext->Rsi;
    pContext->Eax = pPalContext->Rax;
    pContext->Ebx = pPalContext->Rbx;
#elif defined(HOST_ARM)
    pContext->R0  = pPalContext->R0;
    pContext->R4  = pPalContext->R4;
    pContext->R5  = pPalContext->R5;
    pContext->R6  = pPalContext->R6;
    pContext->R7  = pPalContext->R7;
    pContext->R8  = pPalContext->R8;
    pContext->R9  = pPalContext->R9;
    pContext->R10 = pPalContext->R10;
    pContext->R11 = pPalContext->R11;
    pContext->Sp  = pPalContext->SP;
    pContext->Lr  = pPalContext->LR;
    pContext->Pc  = pPalContext->IP;
#elif defined(HOST_ARM64)
    pContext->X0 = pPalContext->X0;
    pContext->X1 = pPalContext->X1;
    // TODO: Copy registers X2-X7 when we start supporting HVA's
    pContext->X19 = pPalContext->X19;
    pContext->X20 = pPalContext->X20;
    pContext->X21 = pPalContext->X21;
    pContext->X22 = pPalContext->X22;
    pContext->X23 = pPalContext->X23;
    pContext->X24 = pPalContext->X24;
    pContext->X25 = pPalContext->X25;
    pContext->X26 = pPalContext->X26;
    pContext->X27 = pPalContext->X27;
    pContext->X28 = pPalContext->X28;
    pContext->Fp = pPalContext->FP;
    pContext->Sp = pPalContext->SP;
    pContext->Lr = pPalContext->LR;
    pContext->Pc = pPalContext->IP;
#elif defined(HOST_WASM)
    // No registers, no work to do yet
#else
#error Not Implemented for this architecture -- RhpCopyContextFromExInfo
#endif
}

#if defined(HOST_AMD64) || defined(HOST_ARM) || defined(HOST_X86) || defined(HOST_ARM64)
struct DISPATCHER_CONTEXT
{
    UIntNative  ControlPc;
    // N.B. There is more here (so this struct isn't the right size), but we ignore everything else
};

#ifdef HOST_X86
struct EXCEPTION_REGISTRATION_RECORD
{
    UIntNative Next;
    UIntNative Handler;
};
#endif // HOST_X86

EXTERN_C void __cdecl RhpFailFastForPInvokeExceptionPreemp(IntNative PInvokeCallsiteReturnAddr, 
                                                           void* pExceptionRecord, void* pContextRecord);
EXTERN_C void REDHAWK_CALLCONV RhpFailFastForPInvokeExceptionCoop(IntNative PInvokeCallsiteReturnAddr, 
                                                                  void* pExceptionRecord, void* pContextRecord);
Int32 __stdcall RhpVectoredExceptionHandler(PEXCEPTION_POINTERS pExPtrs);

EXTERN_C Int32 __stdcall RhpPInvokeExceptionGuard(PEXCEPTION_RECORD       pExceptionRecord,
                                                  UIntNative              EstablisherFrame,
                                                  PCONTEXT                pContextRecord,
                                                  DISPATCHER_CONTEXT *    pDispatcherContext)
{
    UNREFERENCED_PARAMETER(EstablisherFrame);
#ifdef APP_LOCAL_RUNTIME
    UNREFERENCED_PARAMETER(pDispatcherContext);
    //
    // When running on Windows 8.1 RTM, we cannot register our vectored exception handler, because that 
    // version of MRT100.dll does not support it.  However, the binder sets this function as the personality 
    // routine for every reverse p/invoke, so we can handle hardware exceptions from managed code here.  
    //
    EXCEPTION_POINTERS pointers;
    pointers.ExceptionRecord = pExceptionRecord;
    pointers.ContextRecord = pContextRecord;

    if (RhpVectoredExceptionHandler(&pointers) == EXCEPTION_CONTINUE_EXECUTION)
        return ExceptionContinueExecution;
#endif //APP_LOCAL_RUNTIME

    Thread * pThread = ThreadStore::GetCurrentThread();

    // If the thread is currently in the "do not trigger GC" mode, we must not allocate, we must not reverse pinvoke, or
    // return from a pinvoke.  All of these things will deadlock with the GC and they all become increasingly likely as
    // exception dispatch kicks off.  So we just nip this in the bud as early as possible with a FailFast.  The most 
    // likely case where this occurs is in our GC-callouts for Jupiter lifetime management -- in that case, we have 
    // managed code that calls to native code (without pinvoking) which might have a bug that causes an AV.  
    if (pThread->IsDoNotTriggerGcSet())
        RhFailFast();

    // We promote exceptions that were not converted to managed exceptions to a FailFast.  However, we have to
    // be careful because we got here via OS SEH infrastructure and, therefore, don't know what GC mode we're
    // currently in.  As a result, since we're calling back into managed code to handle the FailFast, we must
    // correctly call either a UnmanagedCallersOnly or a RuntimeExport version of the same method.
    if (pThread->IsCurrentThreadInCooperativeMode())
    {
        // Cooperative mode -- Typically, RhpVectoredExceptionHandler will handle this because the faulting IP will be
        // in managed code.  But sometimes we AV on a bad call indirect or something similar.  In that situation, we can
        // use the dispatcher context or exception registration record to find the relevant classlib.
#ifdef HOST_X86
        IntNative classlibBreadcrumb = ((EXCEPTION_REGISTRATION_RECORD*)EstablisherFrame)->Handler;
#else
        IntNative classlibBreadcrumb = pDispatcherContext->ControlPc;
#endif
        RhpFailFastForPInvokeExceptionCoop(classlibBreadcrumb, pExceptionRecord, pContextRecord);
    }
    else
    {
        // Preemptive mode -- the classlib associated with the last pinvoke owns the fail fast behavior.
        IntNative pinvokeCallsiteReturnAddr = (IntNative)pThread->GetCurrentThreadPInvokeReturnAddress();
        RhpFailFastForPInvokeExceptionPreemp(pinvokeCallsiteReturnAddr, pExceptionRecord, pContextRecord);
    }

    return 0;
}
#else
EXTERN_C Int32 RhpPInvokeExceptionGuard()
{
    ASSERT_UNCONDITIONALLY("RhpPInvokeExceptionGuard NYI for this architecture!");
    RhFailFast();
    return 0;
}
#endif

#if defined(HOST_AMD64) || defined(HOST_ARM) || defined(HOST_X86) || defined(HOST_ARM64) || defined(HOST_WASM)
EXTERN_C REDHAWK_API void __fastcall RhpThrowHwEx();
#else
COOP_PINVOKE_HELPER(void, RhpThrowHwEx, ())
{
    ASSERT_UNCONDITIONALLY("RhpThrowHwEx NYI for this architecture!");
}
COOP_PINVOKE_HELPER(void, RhpThrowEx, ())
{
    ASSERT_UNCONDITIONALLY("RhpThrowEx NYI for this architecture!");
}
COOP_PINVOKE_HELPER(void, RhpCallCatchFunclet, ())
{
    ASSERT_UNCONDITIONALLY("RhpCallCatchFunclet NYI for this architecture!");
}
COOP_PINVOKE_HELPER(void, RhpCallFinallyFunclet, ())
{
    ASSERT_UNCONDITIONALLY("RhpCallFinallyFunclet NYI for this architecture!");
}
COOP_PINVOKE_HELPER(void, RhpCallFilterFunclet, ())
{
    ASSERT_UNCONDITIONALLY("RhpCallFilterFunclet NYI for this architecture!");
}
COOP_PINVOKE_HELPER(void, RhpRethrow, ())
{
    ASSERT_UNCONDITIONALLY("RhpRethrow NYI for this architecture!");
}

EXTERN_C void* RhpCallCatchFunclet2 = NULL;
EXTERN_C void* RhpCallFinallyFunclet2 = NULL;
EXTERN_C void* RhpCallFilterFunclet2 = NULL;
EXTERN_C void* RhpThrowEx2   = NULL;
EXTERN_C void* RhpThrowHwEx2 = NULL;
EXTERN_C void* RhpRethrow2   = NULL;
#endif

EXTERN_C void * RhpAssignRefAVLocation;
EXTERN_C void * RhpCheckedAssignRefAVLocation;
EXTERN_C void * RhpCheckedLockCmpXchgAVLocation;
EXTERN_C void * RhpCheckedXchgAVLocation;
EXTERN_C void * RhpLockCmpXchg32AVLocation;
EXTERN_C void * RhpLockCmpXchg64AVLocation;
EXTERN_C void * RhpCopyMultibyteDestAVLocation;
EXTERN_C void * RhpCopyMultibyteSrcAVLocation;
EXTERN_C void * RhpCopyMultibyteNoGCRefsDestAVLocation;
EXTERN_C void * RhpCopyMultibyteNoGCRefsSrcAVLocation;
EXTERN_C void * RhpCopyMultibyteWithWriteBarrierDestAVLocation;
EXTERN_C void * RhpCopyMultibyteWithWriteBarrierSrcAVLocation;
EXTERN_C void * RhpCopyAnyWithWriteBarrierDestAVLocation;
EXTERN_C void * RhpCopyAnyWithWriteBarrierSrcAVLocation;

static bool InWriteBarrierHelper(UIntNative faultingIP)
{
#ifndef USE_PORTABLE_HELPERS
    static UIntNative writeBarrierAVLocations[] = 
    {
        (UIntNative)&RhpAssignRefAVLocation,
        (UIntNative)&RhpCheckedAssignRefAVLocation,
        (UIntNative)&RhpCheckedLockCmpXchgAVLocation,
        (UIntNative)&RhpCheckedXchgAVLocation,
        (UIntNative)&RhpLockCmpXchg32AVLocation,
        (UIntNative)&RhpLockCmpXchg64AVLocation,
    };

    // compare the IP against the list of known possible AV locations in the write barrier helpers
    for (size_t i = 0; i < sizeof(writeBarrierAVLocations)/sizeof(writeBarrierAVLocations[0]); i++)
    {
#if defined(HOST_AMD64) || defined(HOST_X86)
        // Verify that the runtime is not linked with incremental linking enabled. Incremental linking
        // wraps every method symbol with a jump stub that breaks the following check.
        ASSERT(*(UInt8*)writeBarrierAVLocations[i] != 0xE9); // jmp XXXXXXXX
#endif

        if (writeBarrierAVLocations[i] == faultingIP)
            return true;
    }
#endif // USE_PORTABLE_HELPERS

    return false;
}

static UIntNative UnwindWriteBarrierToCaller(
#ifdef TARGET_UNIX
    PAL_LIMITED_CONTEXT * pContext
#else
    _CONTEXT * pContext
#endif
    )
{
#if defined(_DEBUG)
    UIntNative faultingIP = pContext->GetIp();
    ASSERT(InWriteBarrierHelper(faultingIP));
#endif
#if defined(HOST_AMD64) || defined(HOST_X86)
    // simulate a ret instruction
    UIntNative sp = pContext->GetSp();
    UIntNative adjustedFaultingIP = *(UIntNative *)sp;
    pContext->SetSp(sp+sizeof(UIntNative)); // pop the stack
#elif defined(HOST_ARM) || defined(HOST_ARM64)
    UIntNative adjustedFaultingIP = pContext->GetLr();
#else
    UIntNative adjustedFaultingIP = 0; // initializing to make the compiler happy
    PORTABILITY_ASSERT("UnwindWriteBarrierToCaller");
#endif
    return adjustedFaultingIP;
}

#ifdef TARGET_UNIX

Int32 __stdcall RhpHardwareExceptionHandler(UIntNative faultCode, UIntNative faultAddress,
    PAL_LIMITED_CONTEXT* palContext, UIntNative* arg0Reg, UIntNative* arg1Reg)
{
    UIntNative faultingIP = palContext->GetIp();

    ICodeManager * pCodeManager = GetRuntimeInstance()->FindCodeManagerByAddress((PTR_VOID)faultingIP);
    if ((pCodeManager != NULL) || (faultCode == STATUS_ACCESS_VIOLATION && InWriteBarrierHelper(faultingIP)))
    {
        // Make sure that the OS does not use our internal fault codes
        ASSERT(faultCode != STATUS_REDHAWK_NULL_REFERENCE && faultCode != STATUS_REDHAWK_WRITE_BARRIER_NULL_REFERENCE);

        if (faultCode == STATUS_ACCESS_VIOLATION)
        {
            if (faultAddress < NULL_AREA_SIZE)
            {
                faultCode = pCodeManager ? STATUS_REDHAWK_NULL_REFERENCE : STATUS_REDHAWK_WRITE_BARRIER_NULL_REFERENCE;
            }

            if (pCodeManager == NULL)
            {
                // we were AV-ing in a write barrier helper - unwind our way to our caller
                faultingIP = UnwindWriteBarrierToCaller(palContext);
            }
        }
        else if (faultCode == STATUS_STACK_OVERFLOW)
        {
            // Do not use ASSERT_UNCONDITIONALLY here. It will crash because of it consumes too much stack.

            PalPrintFatalError("\nProcess is terminating due to StackOverflowException.\n");
            RhFailFast();
        }

        *arg0Reg = faultCode;
        *arg1Reg = faultingIP;
        palContext->SetIp((UIntNative)&RhpThrowHwEx);

        return EXCEPTION_CONTINUE_EXECUTION;
    }

    return EXCEPTION_CONTINUE_SEARCH;
}

#else // TARGET_UNIX

Int32 __stdcall RhpVectoredExceptionHandler(PEXCEPTION_POINTERS pExPtrs)
{
    UIntNative faultingIP = pExPtrs->ContextRecord->GetIp();

    ICodeManager * pCodeManager = GetRuntimeInstance()->FindCodeManagerByAddress((PTR_VOID)faultingIP);
    UIntNative faultCode = pExPtrs->ExceptionRecord->ExceptionCode;
    if ((pCodeManager != NULL) || (faultCode == STATUS_ACCESS_VIOLATION && InWriteBarrierHelper(faultingIP)))
    {
        // Make sure that the OS does not use our internal fault codes
        ASSERT(faultCode != STATUS_REDHAWK_NULL_REFERENCE && faultCode != STATUS_REDHAWK_WRITE_BARRIER_NULL_REFERENCE);

        if (faultCode == STATUS_ACCESS_VIOLATION)
        {
            if (pExPtrs->ExceptionRecord->ExceptionInformation[1] < NULL_AREA_SIZE)
            {
                faultCode = pCodeManager ? STATUS_REDHAWK_NULL_REFERENCE : STATUS_REDHAWK_WRITE_BARRIER_NULL_REFERENCE;
            }

            if (pCodeManager == NULL)
            {
                // we were AV-ing in a write barrier helper - unwind our way to our caller
                faultingIP = UnwindWriteBarrierToCaller(pExPtrs->ContextRecord);
            }
        }
        else if (faultCode == STATUS_STACK_OVERFLOW)
        {
            // Do not use ASSERT_UNCONDITIONALLY here. It will crash because of it consumes too much stack.

            PalPrintFatalError("\nProcess is terminating due to StackOverflowException.\n");
            PalRaiseFailFastException(pExPtrs->ExceptionRecord, pExPtrs->ContextRecord, 0);
        }

        pExPtrs->ContextRecord->SetIp((UIntNative)&RhpThrowHwEx);
        pExPtrs->ContextRecord->SetArg0Reg(faultCode);
        pExPtrs->ContextRecord->SetArg1Reg(faultingIP);

        return EXCEPTION_CONTINUE_EXECUTION;
    }

    {
        static UInt8 *s_pbRuntimeModuleLower = NULL;
        static UInt8 *s_pbRuntimeModuleUpper = NULL;

        // If this is the first time through this path then calculate the upper and lower bounds of the
        // runtime module. Note we could be racing to calculate this but it doesn't matter since the results
        // should always agree.
        if ((s_pbRuntimeModuleLower == NULL) || (s_pbRuntimeModuleUpper == NULL))
        {
            // Get the module handle for this runtime. Do this by passing an address definitely within the
            // module (the address of this function) to GetModuleHandleEx with the "from address" flag.
            HANDLE hRuntimeModule = PalGetModuleHandleFromPointer(reinterpret_cast<void*>(RhpVectoredExceptionHandler));
            if (!hRuntimeModule)
            {
                ASSERT_UNCONDITIONALLY("Failed to locate our own module handle");
                RhFailFast();
            }

            PalGetModuleBounds(hRuntimeModule, &s_pbRuntimeModuleLower, &s_pbRuntimeModuleUpper);
        }

        if (((UInt8*)faultingIP >= s_pbRuntimeModuleLower) && ((UInt8*)faultingIP < s_pbRuntimeModuleUpper))
        {
            // Generally any form of hardware exception within the runtime itself is considered a fatal error.
            // Note this includes the managed code within the runtime.
            ASSERT_UNCONDITIONALLY("Hardware exception raised inside the runtime.");
            PalRaiseFailFastException(pExPtrs->ExceptionRecord, pExPtrs->ContextRecord, 0);
        }
    }

    return EXCEPTION_CONTINUE_SEARCH;
}

#endif // TARGET_UNIX

COOP_PINVOKE_HELPER(void, RhpFallbackFailFast, ())
{
    RhFailFast();
}

#endif // !DACCESS_COMPILE
