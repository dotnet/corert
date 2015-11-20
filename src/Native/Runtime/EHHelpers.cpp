//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#ifndef DACCESS_COMPILE
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "assert.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "module.h"
#include "varint.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "holder.h"
#include "Crst.h"
#include "RWLock.h"
#include "RuntimeInstance.h"
#include "event.h"
#include "threadstore.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "stressLog.h"

// Find the module containing the given address, which is a return address from a managed function. The
// address may be to another managed function, or it may be to an unmanaged function, or it may be to a GC
// hijack. The address may also refer to an EEType if we've been called from RhpGetClasslibFunction. If it is
// a GC hijack, we will recgonize that and use the real return address, updating the address passed in.
static Module * FindModuleRespectingReturnAddressHijacks(void ** pAddress)
{
    RuntimeInstance * pRI = GetRuntimeInstance();

    // Try looking up the module assuming the address is for code first. Fall back to a read-only data looukp
    // if that fails. If we have data indicating that the data case is more common then we can reverse the
    // order of checks. Finally check read/write data: generic EETypes live there since they need to be fixed
    // up at runtime to support unification.
    Module * pModule = pRI->FindModuleByCodeAddress(*pAddress);
    if (pModule == NULL)
    {
        pModule = pRI->FindModuleByReadOnlyDataAddress(*pAddress);

        if (pModule == NULL)
            pModule = pRI->FindModuleByDataAddress(*pAddress);

        if (pModule == NULL)
        {
            // Hmmm... we didn't find a managed module for the given PC. We have a return address in unmanaged
            // code, but it could be because the thread is hijacked for GC suspension. If it is then we should
            // get the real return address and try again.
            Thread * pCurThread = ThreadStore::GetCurrentThread();
        
            if (!pCurThread->IsHijacked())
            {
                // The PC isn't in a managed module, and there is no hijack in place, so we have no EH info.
                return NULL;
            }

            // Update the PC passed in to reflect the correct return address.
            *pAddress = pCurThread->GetHijackedReturnAddress();

            pModule = pRI->FindModuleByCodeAddress(*pAddress);
        }
    }

    return pModule;
}

COOP_PINVOKE_HELPER(Boolean, RhpEHEnumInitFromStackFrameIterator, (StackFrameIterator* pFrameIter, void ** pMethodStartAddressOut, EHEnum* pEHEnum))
{
    ICodeManager * pCodeManager = pFrameIter->GetCodeManager();
    pEHEnum->m_pCodeManager = pCodeManager;

    return pCodeManager->EHEnumInit(pFrameIter->GetMethodInfo(), pMethodStartAddressOut, &pEHEnum->m_state);
}

COOP_PINVOKE_HELPER(Boolean, RhpEHEnumNext, (EHEnum* pEHEnum, EHClause* pEHClause))
{
    return pEHEnum->m_pCodeManager->EHEnumNext(&pEHEnum->m_state, pEHClause);
}

// The EH dispatch code needs to know the original return address of a method, even if it has been hijacked.
// We provide this here without modifying the hijack and the EHJump helper will honor the hijack when it
// attempts to dispatch up the stack.
COOP_PINVOKE_HELPER(void*, RhpGetUnhijackedReturnAddress, (void** ppvReturnAddressLocation))
{
    return ThreadStore::GetCurrentThread()->GetUnhijackedReturnAddress(ppvReturnAddressLocation);
}

//------------------------------------------------------------------------------------------------------------
// @TODO:EXCEPTIONS: the code below is related to throwing exceptions out of Rtm. If we did not have to throw
// out of Rtm, then we would note have to have the code below to get a classlib exception object given
// an exception id, or the special functions to back up the MDIL THROW_* instructions, or the allocation
// failure helper. If we could move to a world where we never throw out of Rtm, perhaps by moving parts
// of Rtm that do need to throw out to Bartok- or Binder-generated functions, then we could remove all of this.
//------------------------------------------------------------------------------------------------------------


// Constants used with RhpGetClasslibFunction, to indicate which classlib function
// we are interested in. 
// Note: make sure you change the def in rtm\System\Runtime\exceptionhandling.cs if you change this!
enum ClasslibFunctionId
{
    GetRuntimeException = 0,
    FailFast = 1,
    UnhandledExceptionHandler = 2,
    AppendExceptionStackFrame = 3,
};

// Unmanaged helper to locate one of two classlib-provided functions that the runtime needs to 
// implement throwing of exceptions out of Rtm, and fail-fast. This may return NULL if the classlib
// found via the provided address does not have the necessary exports.
COOP_PINVOKE_HELPER(void *, RhpGetClasslibFunction, (void * address, ClasslibFunctionId functionId))
{
    // Find the module contianing the given address, which is an address into some managed module. It could
    // be code, or it could be an EEType. No matter what, it's an address into a managed module in some non-Rtm
    // type system.
    Module * pModule = FindModuleRespectingReturnAddressHijacks(&address);
     
    // If the address isn't in a managed module then we have no classlib function.
    if (pModule == NULL)
    {
        return NULL;
    }

    // Now, find the classlib module that the first module was compiled against. This one will contain definitions
    // for the classlib functions we need here at runtime.
    Module * pClasslibModule = pModule->GetClasslibModule();
    ASSERT(pClasslibModule != NULL);

    // Lookup the method and return it. If we don't find it, we just return NULL.
    void * pMethod = NULL;
    
    if (functionId == GetRuntimeException)
    {
        pMethod = pClasslibModule->GetClasslibRuntimeExceptionHelper();
    }
    else if (functionId == AppendExceptionStackFrame)
    {
        pMethod = pClasslibModule->GetClasslibAppendExceptionStackFrameHelper();
    }
    else if (functionId == FailFast)
    {
        pMethod = pClasslibModule->GetClasslibFailFastHelper();
    }
    else if (functionId == UnhandledExceptionHandler)
    {
        pMethod = pClasslibModule->GetClasslibUnhandledExceptionHandlerHelper();
    }

    return pMethod;
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

COOP_PINVOKE_HELPER(Int32, RhGetModuleFileName, (HANDLE moduleHandle, _Out_ wchar_t** pModuleNameOut))
{
    return PalGetModuleFileName(pModuleNameOut, moduleHandle);
}

COOP_PINVOKE_HELPER(void, RhpCopyContextFromExInfo, 
                                (void * pOSContext, Int32 cbOSContext, PAL_LIMITED_CONTEXT * pPalContext))
{
    UNREFERENCED_PARAMETER(cbOSContext);
    ASSERT(cbOSContext >= sizeof(CONTEXT));
    CONTEXT* pContext = (CONTEXT *)pOSContext;
#ifdef _AMD64_
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
#elif defined(_X86_)
    pContext->Eip = pPalContext->IP;
    pContext->Esp = pPalContext->Rsp;
    pContext->Ebp = pPalContext->Rbp;
    pContext->Edi = pPalContext->Rdi;
    pContext->Esi = pPalContext->Rsi;
    pContext->Eax = pPalContext->Rax;
    pContext->Ebx = pPalContext->Rbx;
#elif defined(_ARM_)
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
#else
#error Not Implemented for this architecture -- RhpCopyContextFromExInfo
#endif
}


#if defined(_AMD64_) || defined(_ARM_) || defined(_X86_)
struct DISPATCHER_CONTEXT
{
    UIntNative  ControlPc;
    // N.B. There is more here (so this struct isn't the right size), but we ignore everything else
};

EXTERN_C void REDHAWK_CALLCONV RhpFailFastForPInvokeExceptionPreemp(IntNative PInvokeCallsiteReturnAddr, 
                                                                    void* pExceptionRecord, void* pContextRecord);
EXTERN_C void REDHAWK_CALLCONV RhpFailFastForPInvokeExceptionCoop(IntNative PInvokeCallsiteReturnAddr, 
                                                                  void* pExceptionRecord, void* pContextRecord);
Int32 __stdcall RhpVectoredExceptionHandler(PEXCEPTION_POINTERS pExPtrs);

EXTERN_C Int32 __stdcall RhpPInvokeExceptionGuard(PEXCEPTION_RECORD       pExceptionRecord,
                                        UIntNative              /*MemoryStackFp*/,
                                        PCONTEXT                pContextRecord,
                                        DISPATCHER_CONTEXT *    pDispatcherContext)
{
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
    // correctly call either a NativeCallable or a RuntimeExport version of the same method.
    if (pThread->IsCurrentThreadInCooperativeMode())
        RhpFailFastForPInvokeExceptionCoop((IntNative)pDispatcherContext->ControlPc, pExceptionRecord, pContextRecord);
    else
        RhpFailFastForPInvokeExceptionPreemp((IntNative)pDispatcherContext->ControlPc, pExceptionRecord, pContextRecord);

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

#if defined(_AMD64_) || defined(_ARM_) || defined(_X86_)
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
EXTERN_C void * RhpCopyMultibyteDestAVLocation;
EXTERN_C void * RhpCopyMultibyteSrcAVLocation;
EXTERN_C void * RhpCopyMultibyteNoGCRefsDestAVLocation;
EXTERN_C void * RhpCopyMultibyteNoGCRefsSrcAVLocation;

static bool InWriteBarrierHelper(UIntNative faultingIP)
{
#ifndef USE_PORTABLE_HELPERS
    static UIntNative writeBarrierAVLocations[] = 
    {
        (UIntNative)&RhpAssignRefAVLocation,
        (UIntNative)&RhpCheckedAssignRefAVLocation,
        (UIntNative)&RhpCheckedLockCmpXchgAVLocation,
        (UIntNative)&RhpCheckedXchgAVLocation,
        (UIntNative)&RhpCopyMultibyteDestAVLocation,
        (UIntNative)&RhpCopyMultibyteSrcAVLocation,
        (UIntNative)&RhpCopyMultibyteNoGCRefsDestAVLocation,
        (UIntNative)&RhpCopyMultibyteNoGCRefsSrcAVLocation,
    };

    // compare the IP against the list of known possible AV locations in the write barrier helpers
    for (size_t i = 0; i < sizeof(writeBarrierAVLocations)/sizeof(writeBarrierAVLocations[0]); i++)
    {
        if (writeBarrierAVLocations[i] == faultingIP)
            return true;
    }
#endif // USE_PORTABLE_HELPERS

    return false;
}

static UIntNative UnwindWriteBarrierToCaller(_CONTEXT * pContext)
{
#if defined(_DEBUG)
    UIntNative faultingIP = pContext->GetIP();
    ASSERT(InWriteBarrierHelper(faultingIP));
#endif
#if defined(_AMD64_) || defined(_X86_)
    // simulate a ret instruction
    UIntNative sp = pContext->GetSP();      // get the stack pointer
    UIntNative adjustedFaultingIP = *(UIntNative *)sp - 5;   // call instruction will be 6 bytes - act as if start of call instruction + 1 were the faulting IP
    pContext->SetSP(sp+sizeof(UIntNative)); // pop the stack
#elif defined(_ARM_)
    UIntNative adjustedFaultingIP = pContext->GetLR() - 2;   // bl instruction will be 4 bytes - act as if start of call instruction + 2 were the faulting IP
#else
#error "Unknown Architecture"
#endif
    return adjustedFaultingIP;
}

Int32 __stdcall RhpVectoredExceptionHandler(PEXCEPTION_POINTERS pExPtrs)
{
    UIntNative faultingIP = pExPtrs->ContextRecord->GetIP();

    ICodeManager * pCodeManager = GetRuntimeInstance()->FindCodeManagerByAddress((PTR_VOID)faultingIP);
    UIntNative faultCode = pExPtrs->ExceptionRecord->ExceptionCode;
    if ((pCodeManager != NULL) || (faultCode == STATUS_ACCESS_VIOLATION && InWriteBarrierHelper(faultingIP)))
    {
        if (faultCode == STATUS_ACCESS_VIOLATION)
        {
            if (pExPtrs->ExceptionRecord->ExceptionInformation[1] < NULL_AREA_SIZE)
                faultCode = STATUS_REDHAWK_NULL_REFERENCE;
            if (pCodeManager == NULL)
            {
                // we were AV-ing in a write barrier helper - unwind our way to our caller
                faultingIP = UnwindWriteBarrierToCaller(pExPtrs->ContextRecord);
            }
        }
        else if (faultCode == STATUS_STACK_OVERFLOW)
        {
            ASSERT_UNCONDITIONALLY("managed stack overflow");
            RhFailFast2(pExPtrs->ExceptionRecord, pExPtrs->ContextRecord);
        }

        pExPtrs->ContextRecord->SetIP((UIntNative)&RhpThrowHwEx);
        pExPtrs->ContextRecord->SetArg0Reg(faultCode);
        pExPtrs->ContextRecord->SetArg1Reg(faultingIP);

        return EXCEPTION_CONTINUE_EXECUTION;
    }
    else
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
            HANDLE hRuntimeModule = PalGetModuleHandleFromPointer(RhpVectoredExceptionHandler);
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
            RhFailFast2(pExPtrs->ExceptionRecord, pExPtrs->ContextRecord);
        }

        return EXCEPTION_CONTINUE_SEARCH;
    }
}


#endif // !DACCESS_COMPILE
