//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "RedhawkWarnings.h"
#include "assert.h"
#include "slist.h"
#include "gcrhinterface.h"
#include "varint.h"
#include "regdisplay.h"
#include "StackFrameIterator.h"
#include "thread.h"
#include "holder.h"
#include "Crst.h"
#include "RWLock.h"
#include "static_check.h"
#include "event.h"
#include "threadstore.h"
#include "stressLog.h"

#include "module.h"
#include "RuntimeInstance.h"
#include "rhbinder.h"

// warning C4061: enumerator '{blah}' in switch of enum '{blarg}' is not explicitly handled by a case label
#pragma warning(disable:4061)

#if !defined(USE_PORTABLE_HELPERS) // these are (currently) only implemented in assembly helpers
// When we use a thunk to call out to managed code from the runtime the following label is the instruction
// immediately following the thunk's call instruction. As such it can be used to identify when such a callout
// has occured as we are walking the stack.
EXTERN_C void * ReturnFromManagedCallout2;
GVAL_IMPL_INIT(PTR_VOID, g_ReturnFromManagedCallout2Addr, &ReturnFromManagedCallout2);

#if defined(FEATURE_DYNAMIC_CODE)
EXTERN_C void * ReturnFromUniversalTransition;
GVAL_IMPL_INIT(PTR_VOID, g_ReturnFromUniversalTransitionAddr, &ReturnFromUniversalTransition);

EXTERN_C void * ReturnFromCallDescrThunk;
GVAL_IMPL_INIT(PTR_VOID, g_ReturnFromCallDescrThunkAddr, &ReturnFromCallDescrThunk);
#endif

#ifdef TARGET_X86
EXTERN_C void * RhpCallFunclet2;
GVAL_IMPL_INIT(PTR_VOID, g_RhpCallFunclet2Addr, &RhpCallFunclet2);
#endif
EXTERN_C void * RhpCallCatchFunclet2;
GVAL_IMPL_INIT(PTR_VOID, g_RhpCallCatchFunclet2Addr, &RhpCallCatchFunclet2);
EXTERN_C void * RhpCallFinallyFunclet2;
GVAL_IMPL_INIT(PTR_VOID, g_RhpCallFinallyFunclet2Addr, &RhpCallFinallyFunclet2);
EXTERN_C void * RhpCallFilterFunclet2;
GVAL_IMPL_INIT(PTR_VOID, g_RhpCallFilterFunclet2Addr, &RhpCallFilterFunclet2);
EXTERN_C void * RhpThrowEx2;
GVAL_IMPL_INIT(PTR_VOID, g_RhpThrowEx2Addr, &RhpThrowEx2);
EXTERN_C void * RhpThrowHwEx2;
GVAL_IMPL_INIT(PTR_VOID, g_RhpThrowHwEx2Addr, &RhpThrowHwEx2);
EXTERN_C void * RhpRethrow2;
GVAL_IMPL_INIT(PTR_VOID, g_RhpRethrow2Addr, &RhpRethrow2);
#endif //!defined(USE_PORTABLE_HELPERS)

// Addresses of functions in the DAC won't match their runtime counterparts so we
// assign them to globals. However it is more performant in the runtime to compare
// against immediates than to fetch the global. This macro hides the difference.
#ifdef DACCESS_COMPILE
#define EQUALS_CODE_ADDRESS(x, func_name) ((x) == g_ ## func_name ## Addr)
#else
#define EQUALS_CODE_ADDRESS(x, func_name) ((x) == &func_name)
#endif


// The managed callout thunk above stashes a transition frame pointer in its FP frame. The following constant
// is the offset from the FP at which this pointer is stored.
#define MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET (-(Int32)sizeof(UIntNative))

PTR_PInvokeTransitionFrame GetPInvokeTransitionFrame(PTR_VOID pTransitionFrame)
{
    return static_cast<PTR_PInvokeTransitionFrame>(pTransitionFrame);
}


StackFrameIterator::StackFrameIterator(Thread * pThreadToWalk, PTR_VOID pInitialTransitionFrame)
{
    STRESS_LOG0(LF_STACKWALK, LL_INFO10000, "----Init---- [ GC ]\n");
    ASSERT(!pThreadToWalk->DangerousCrossThreadIsHijacked());
    InternalInit(pThreadToWalk, GetPInvokeTransitionFrame(pInitialTransitionFrame));
}

StackFrameIterator::StackFrameIterator(Thread * pThreadToWalk, PTR_PAL_LIMITED_CONTEXT pCtx)
{
    STRESS_LOG0(LF_STACKWALK, LL_INFO10000, "----Init---- [ hijack ]\n");
    InternalInit(pThreadToWalk, pCtx, 0);
}

void StackFrameIterator::ResetNextExInfoForSP(UIntNative SP)
{
    while (m_pNextExInfo && (SP > (UIntNative)dac_cast<TADDR>(m_pNextExInfo)))
        m_pNextExInfo = m_pNextExInfo->m_pPrevExInfo;
}

void StackFrameIterator::InternalInit(Thread * pThreadToWalk, PTR_PInvokeTransitionFrame pFrame)
{
    m_pThread = pThreadToWalk;
    m_pInstance = GetRuntimeInstance();
    m_pCodeManager = NULL;
    m_pHijackedReturnValue = NULL;
    m_HijackedReturnValueKind = GCRK_Unknown;
    m_pConservativeStackRangeLowerBound = NULL;
    m_pConservativeStackRangeUpperBound = NULL;
    m_dwFlags = CollapseFunclets | RemapHardwareFaultsToSafePoint;  // options for GC stack walk
    m_pNextExInfo = pThreadToWalk->GetCurExInfo();

    if (pFrame == TOP_OF_STACK_MARKER)
    {
        m_ControlPC = 0;
        return;
    }

    memset(&m_RegDisplay, 0, sizeof(m_RegDisplay));

    // We need to walk the ExInfo chain in parallel with the stackwalk so that we know when we cross over 
    // exception throw points.  So we must find our initial point in the ExInfo chain here so that we can 
    // properly walk it in parallel.
    ResetNextExInfoForSP((UIntNative)dac_cast<TADDR>(pFrame));

    m_RegDisplay.SetIP((PCODE)pFrame->m_RIP);
    m_RegDisplay.SetAddrOfIP((PTR_PCODE)PTR_HOST_MEMBER(PInvokeTransitionFrame, pFrame, m_RIP));

    PTR_UIntNative pPreservedRegsCursor = (PTR_UIntNative)PTR_HOST_MEMBER(PInvokeTransitionFrame, pFrame, m_PreservedRegs);

#ifdef TARGET_ARM
    m_RegDisplay.pLR = (PTR_UIntNative)PTR_HOST_MEMBER(PInvokeTransitionFrame, pFrame, m_RIP);
    m_RegDisplay.pR11 = (PTR_UIntNative)PTR_HOST_MEMBER(PInvokeTransitionFrame, pFrame, m_ChainPointer);
     
    if (pFrame->m_dwFlags & PTFF_SAVE_R4)  { m_RegDisplay.pR4 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R5)  { m_RegDisplay.pR5 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R6)  { m_RegDisplay.pR6 = pPreservedRegsCursor++; }
    ASSERT(!(pFrame->m_dwFlags & PTFF_SAVE_R7)); // R7 should never contain a GC ref because we require
                                                 // a frame pointer for methods with pinvokes
    if (pFrame->m_dwFlags & PTFF_SAVE_R8)  { m_RegDisplay.pR8 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R9)  { m_RegDisplay.pR9 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R10)  { m_RegDisplay.pR10 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_SP)  { m_RegDisplay.SP  = *pPreservedRegsCursor++; }

    m_RegDisplay.pR7 = (PTR_UIntNative) PTR_HOST_MEMBER(PInvokeTransitionFrame, pFrame, m_FramePointer);

    if (pFrame->m_dwFlags & PTFF_SAVE_R0)  { m_RegDisplay.pR0 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R1)  { m_RegDisplay.pR1 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R2)  { m_RegDisplay.pR2 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R3)  { m_RegDisplay.pR3 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_LR)  { m_RegDisplay.pLR = pPreservedRegsCursor++; }

    if (pFrame->m_dwFlags & PTFF_R0_IS_GCREF)
    {
        m_pHijackedReturnValue = (PTR_RtuObjectRef) m_RegDisplay.pR0;
        m_HijackedReturnValueKind = GCRK_Object;
    }
    if (pFrame->m_dwFlags & PTFF_R0_IS_BYREF)
    {
        m_pHijackedReturnValue = (PTR_RtuObjectRef) m_RegDisplay.pR0;
        m_HijackedReturnValueKind = GCRK_Byref;
    }

    m_ControlPC       = dac_cast<PTR_VOID>(*(m_RegDisplay.pIP));
#else // TARGET_ARM
    if (pFrame->m_dwFlags & PTFF_SAVE_RBX)  { m_RegDisplay.pRbx = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_RSI)  { m_RegDisplay.pRsi = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_RDI)  { m_RegDisplay.pRdi = pPreservedRegsCursor++; }
    ASSERT(!(pFrame->m_dwFlags & PTFF_SAVE_RBP)); // RBP should never contain a GC ref because we require
                                                  // a frame pointer for methods with pinvokes
#ifdef TARGET_AMD64
    if (pFrame->m_dwFlags & PTFF_SAVE_R12)  { m_RegDisplay.pR12 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R13)  { m_RegDisplay.pR13 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R14)  { m_RegDisplay.pR14 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R15)  { m_RegDisplay.pR15 = pPreservedRegsCursor++; }
#endif // TARGET_AMD64

    m_RegDisplay.pRbp = (PTR_UIntNative) PTR_HOST_MEMBER(PInvokeTransitionFrame, pFrame, m_FramePointer);

    if (pFrame->m_dwFlags & PTFF_SAVE_RSP)  { m_RegDisplay.SP   = *pPreservedRegsCursor++; }

    if (pFrame->m_dwFlags & PTFF_SAVE_RAX)  { m_RegDisplay.pRax = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_RCX)  { m_RegDisplay.pRcx = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_RDX)  { m_RegDisplay.pRdx = pPreservedRegsCursor++; }
#ifdef TARGET_AMD64
    if (pFrame->m_dwFlags & PTFF_SAVE_R8 )  { m_RegDisplay.pR8  = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R9 )  { m_RegDisplay.pR9  = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R10)  { m_RegDisplay.pR10 = pPreservedRegsCursor++; }
    if (pFrame->m_dwFlags & PTFF_SAVE_R11)  { m_RegDisplay.pR11 = pPreservedRegsCursor++; }
#endif // TARGET_AMD64

    if (pFrame->m_dwFlags & PTFF_RAX_IS_GCREF)
    {
        m_pHijackedReturnValue = (PTR_RtuObjectRef) m_RegDisplay.pRax;
        m_HijackedReturnValueKind = GCRK_Object;
    }
    if (pFrame->m_dwFlags & PTFF_RAX_IS_BYREF)
    {
        m_pHijackedReturnValue = (PTR_RtuObjectRef) m_RegDisplay.pRax;
        m_HijackedReturnValueKind = GCRK_Byref;
    }

    m_ControlPC       = dac_cast<PTR_VOID>(*(m_RegDisplay.pIP));
#endif // TARGET_ARM

    // @TODO: currently, we always save all registers -- how do we handle the onese we don't save once we 
    //        start only saving those that weren't already saved?

    // If our control PC indicates that we're in one of the thunks we use to make managed callouts from the
    // runtime we need to adjust the frame state to that of the managed method that previously called into the
    // runtime (i.e. skip the intervening unmanaged frames).
    HandleManagedCalloutThunk();

    STRESS_LOG1(LF_STACKWALK, LL_INFO10000, "   %p\n", m_ControlPC);
}

#ifndef DACCESS_COMPILE

void StackFrameIterator::InternalInitForEH(Thread * pThreadToWalk, PAL_LIMITED_CONTEXT * pCtx)
{
    STRESS_LOG0(LF_STACKWALK, LL_INFO10000, "----Init---- [ EH ]\n");
    StackFrameIterator::InternalInit(pThreadToWalk, pCtx, ApplyReturnAddressAdjustment);

    STRESS_LOG1(LF_STACKWALK, LL_INFO10000, "   %p\n", m_ControlPC);
}

void StackFrameIterator::InternalInitForStackTrace()
{
    STRESS_LOG0(LF_STACKWALK, LL_INFO10000, "----Init---- [ StackTrace ]\n");
    Thread * pThreadToWalk = ThreadStore::GetCurrentThread();
    PTR_VOID pFrame = pThreadToWalk->GetTransitionFrameForStackTrace();
    InternalInit(pThreadToWalk, GetPInvokeTransitionFrame(pFrame));
}

#endif //!DACCESS_COMPILE

void StackFrameIterator::InternalInit(Thread * pThreadToWalk, PTR_PAL_LIMITED_CONTEXT pCtx, UInt32 dwFlags)
{
    ASSERT((dwFlags & MethodStateCalculated) == 0);

    m_pThread = pThreadToWalk;
    m_pInstance = GetRuntimeInstance(); 
    m_ControlPC = 0;
    m_pCodeManager = NULL;
    m_pHijackedReturnValue = NULL;
    m_HijackedReturnValueKind = GCRK_Unknown;
    m_pConservativeStackRangeLowerBound = NULL;
    m_pConservativeStackRangeUpperBound = NULL;
    m_dwFlags = dwFlags;
    m_pNextExInfo = pThreadToWalk->GetCurExInfo();

    // We need to walk the ExInfo chain in parallel with the stackwalk so that we know when we cross over 
    // exception throw points.  So we must find our initial point in the ExInfo chain here so that we can 
    // properly walk it in parallel.
    ResetNextExInfoForSP(pCtx->GetSp());

    PTR_VOID ControlPC = dac_cast<PTR_VOID>(pCtx->GetIp());
    if (dwFlags & ApplyReturnAddressAdjustment)
        ControlPC = AdjustReturnAddressBackward(ControlPC);

    // If our control PC indicates that we're in one of the thunks we use to make managed callouts from the
    // runtime we need to adjust the frame state to that of the managed method that previously called into the
    // runtime (i.e. skip the intervening unmanaged frames).
    HandleManagedCalloutThunk(ControlPC, pCtx->GetFp());

    // This codepath is used by the hijack stackwalk and we can get arbitrary ControlPCs from there.  If this
    // context has a non-managed control PC, then we're done.
    if (!m_pInstance->FindCodeManagerByAddress(ControlPC))
        return;

    //
    // control state
    //
    m_ControlPC       = ControlPC;
    m_RegDisplay.SP   = pCtx->GetSp();
    m_RegDisplay.IP   = pCtx->GetIp();
    m_RegDisplay.pIP  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, IP);

#ifdef TARGET_ARM
    //
    // preserved regs
    //
    m_RegDisplay.pR4  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R4);
    m_RegDisplay.pR5  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R5);
    m_RegDisplay.pR6  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R6);
    m_RegDisplay.pR7  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R7);
    m_RegDisplay.pR8  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R8);
    m_RegDisplay.pR9  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R9);
    m_RegDisplay.pR10 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R10);
    m_RegDisplay.pR11 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R11);
    m_RegDisplay.pLR  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, LR);

    //
    // preserved vfp regs
    //
    for (Int32 i = 0; i < 16 - 8; i++)
    {
        m_RegDisplay.D[i] = pCtx->D[i];
    }
    //
    // scratch regs
    //
    m_RegDisplay.pR0  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R0);
#else // TARGET_ARM
    //
    // preserved regs
    //
    m_RegDisplay.pRbp = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, Rbp);
    m_RegDisplay.pRsi = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, Rsi);
    m_RegDisplay.pRdi = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, Rdi);
    m_RegDisplay.pRbx = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, Rbx);
#ifdef TARGET_AMD64
    m_RegDisplay.pR12 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R12);
    m_RegDisplay.pR13 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R13);
    m_RegDisplay.pR14 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R14);
    m_RegDisplay.pR15 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, R15);
    //
    // preserved xmm regs
    //
    memcpy(m_RegDisplay.Xmm, &pCtx->Xmm6, sizeof(m_RegDisplay.Xmm));
#endif // TARGET_AMD64

    //
    // scratch regs
    //
    m_RegDisplay.pRax = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pCtx, Rax);
    m_RegDisplay.pRcx = NULL;
    m_RegDisplay.pRdx = NULL;
#ifdef TARGET_AMD64
    m_RegDisplay.pR8  = NULL;
    m_RegDisplay.pR9  = NULL;
    m_RegDisplay.pR10 = NULL;
    m_RegDisplay.pR11 = NULL;
#endif // TARGET_AMD64
#endif // TARGET_ARM
}

PTR_VOID StackFrameIterator::HandleExCollide(PTR_ExInfo pExInfo, PTR_VOID collapsingTargetFrame)
{
    STRESS_LOG3(LF_STACKWALK, LL_INFO10000, "   [ ex collide ] kind = %d, pass = %d, idxCurClause = %d\n", 
                pExInfo->m_kind, pExInfo->m_passNumber, pExInfo->m_idxCurClause);

    UInt32 curFlags = m_dwFlags;

    // If we aren't invoking a funclet (i.e. idxCurClause == -1), and we're doing a GC stackwalk, we don't 
    // want the 2nd-pass collided behavior because that behavior assumes that the previous frame was a 
    // funclet, which isn't the case when taking a GC at some points in the EH dispatch code.  So we treat it
    // as if the 2nd pass hasn't actually started yet.
    if ((pExInfo->m_passNumber == 1) || 
        (pExInfo->m_idxCurClause == 0xFFFFFFFF)) 
    {
        ASSERT_MSG(!(curFlags & ApplyReturnAddressAdjustment), 
            "did not expect to collide with a 1st-pass ExInfo during a EH stackwalk");
        InternalInit(m_pThread, pExInfo->m_pExContext, curFlags);
        m_pNextExInfo = pExInfo->m_pPrevExInfo;
        CalculateCurrentMethodState();
        ASSERT(IsValid());

        if ((pExInfo->m_kind == EK_HardwareFault) && (curFlags & RemapHardwareFaultsToSafePoint))
            GetCodeManager()->RemapHardwareFaultToGCSafePoint(&m_methodInfo, &m_codeOffset);
    }
    else
    {
        //
        // Copy our state from the previous StackFrameIterator
        //
        this->UpdateFromExceptionDispatch((PTR_StackFrameIterator)&pExInfo->m_frameIter);

        // Sync our 'current' ExInfo with the updated state (we may have skipped other dispatches)
        ResetNextExInfoForSP(m_RegDisplay.GetSP());

        if ((m_dwFlags & ApplyReturnAddressAdjustment) && (curFlags & ApplyReturnAddressAdjustment))
        {
            // Counteract our pre-adjusted m_ControlPC, since the caller of this routine will apply the 
            // adjustment again once we return.
            m_ControlPC = AdjustReturnAddressForward(m_ControlPC);
        }
        m_dwFlags = curFlags;
        if ((m_ControlPC != 0) &&           // the dispatch in ExInfo could have gone unhandled
            (m_dwFlags & CollapseFunclets))
        {
            CalculateCurrentMethodState();
            ASSERT(IsValid());
            if (GetCodeManager()->IsFunclet(&m_methodInfo))
            {
                // We just unwound out of a funclet, now we need to keep unwinding until we find the 'main 
                // body' associated with this funclet and then unwind out of that.
                collapsingTargetFrame = m_FramePointer;
            }
            else 
            {
                // We found the main body, now unwind out of that and we're done.

                // In the case where the caller *was* the main body, we didn't need to set 
                // collapsingTargetFrame, so it is zero in that case.  
                ASSERT(!collapsingTargetFrame || (collapsingTargetFrame == m_FramePointer));
                NextInternal();
                collapsingTargetFrame = 0;
            }
        }
    }
    return collapsingTargetFrame;
}

void StackFrameIterator::UpdateFromExceptionDispatch(PTR_StackFrameIterator pSourceIterator)
{
    PreservedRegPtrs thisFuncletPtrs = this->m_funcletPtrs;

    // Blast over 'this' with everything from the 'source'.  
    *this = *pSourceIterator;

    // Then, put back the pointers to the funclet's preserved registers (since those are the correct values
    // until the funclet completes, at which point the values will be copied back to the ExInfo's REGDISPLAY).

#ifdef TARGET_ARM
    m_RegDisplay.pR4  = thisFuncletPtrs.pR4 ;
    m_RegDisplay.pR5  = thisFuncletPtrs.pR5 ;
    m_RegDisplay.pR6  = thisFuncletPtrs.pR6 ;
    m_RegDisplay.pR7  = thisFuncletPtrs.pR7 ;
    m_RegDisplay.pR8  = thisFuncletPtrs.pR8 ;
    m_RegDisplay.pR9  = thisFuncletPtrs.pR9 ;
    m_RegDisplay.pR10 = thisFuncletPtrs.pR10;
    m_RegDisplay.pR11 = thisFuncletPtrs.pR11;
#else
    // Save the preserved regs portion of the REGDISPLAY across the unwind through the C# EH dispatch code.
    m_RegDisplay.pRbp = thisFuncletPtrs.pRbp;
    m_RegDisplay.pRdi = thisFuncletPtrs.pRdi;
    m_RegDisplay.pRsi = thisFuncletPtrs.pRsi;
    m_RegDisplay.pRbx = thisFuncletPtrs.pRbx;
#ifdef TARGET_AMD64
    m_RegDisplay.pR12 = thisFuncletPtrs.pR12;
    m_RegDisplay.pR13 = thisFuncletPtrs.pR13;
    m_RegDisplay.pR14 = thisFuncletPtrs.pR14;
    m_RegDisplay.pR15 = thisFuncletPtrs.pR15;
#endif // TARGET_AMD64
#endif // TARGET_ARM
}


// The invoke of a funclet is a bit special and requires an assembly thunk, but we don't want to break the
// stackwalk due to this.  So this routine will unwind through the assembly thunks used to invoke funclets.
// It's also used to disambiguate exceptionally- and non-exceptionally-invoked funclets.
bool StackFrameIterator::HandleFuncletInvokeThunk()
{
#if defined(USE_PORTABLE_HELPERS) // @TODO: Currently no funclet invoke defined in a portable way
    return false;
#else // defined(USE_PORTABLE_HELPERS)

    ASSERT((m_dwFlags & MethodStateCalculated) == 0);

    if (
#ifdef TARGET_X86
        !EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallFunclet2)
#else
        !EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallCatchFunclet2) &&
        !EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallFinallyFunclet2) &&
        !EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallFilterFunclet2)
#endif
        )
    {
        return false;
    }

    PTR_UIntNative SP;

#ifdef TARGET_X86
    // First, unwind RhpCallFunclet
    SP = (PTR_UIntNative)(m_RegDisplay.SP + 0x4);   // skip the saved assembly-routine-EBP
    m_RegDisplay.SetAddrOfIP(SP);
    m_RegDisplay.SetIP(*SP++);
    m_RegDisplay.SetSP((UIntNative)dac_cast<TADDR>(SP));
    m_ControlPC = dac_cast<PTR_VOID>(*(m_RegDisplay.pIP));

    ASSERT(
        EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallCatchFunclet2) ||
        EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallFinallyFunclet2) ||
        EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallFilterFunclet2)
        );
#endif

#ifdef TARGET_AMD64
    // Save the preserved regs portion of the REGDISPLAY across the unwind through the C# EH dispatch code.
    m_funcletPtrs.pRbp = m_RegDisplay.pRbp;
    m_funcletPtrs.pRdi = m_RegDisplay.pRdi;
    m_funcletPtrs.pRsi = m_RegDisplay.pRsi;
    m_funcletPtrs.pRbx = m_RegDisplay.pRbx;
    m_funcletPtrs.pR12 = m_RegDisplay.pR12;
    m_funcletPtrs.pR13 = m_RegDisplay.pR13;
    m_funcletPtrs.pR14 = m_RegDisplay.pR14;
    m_funcletPtrs.pR15 = m_RegDisplay.pR15;

    SP = (PTR_UIntNative)(m_RegDisplay.SP + 0x28);

    m_RegDisplay.pRbp = SP++;
    m_RegDisplay.pRdi = SP++;
    m_RegDisplay.pRsi = SP++;
    m_RegDisplay.pRbx = SP++;
    m_RegDisplay.pR12 = SP++;
    m_RegDisplay.pR13 = SP++;
    m_RegDisplay.pR14 = SP++;
    m_RegDisplay.pR15 = SP++;

    // RhpCallCatchFunclet puts a couple of extra things on the stack that aren't put there by the other two  
    // thunks, but we don't need to know what they are here, so we just skip them.
    if (EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallCatchFunclet2))
        SP += 2;
#elif defined(TARGET_X86)
    // Save the preserved regs portion of the REGDISPLAY across the unwind through the C# EH dispatch code.
    m_funcletPtrs.pRbp = m_RegDisplay.pRbp;
    m_funcletPtrs.pRdi = m_RegDisplay.pRdi;
    m_funcletPtrs.pRsi = m_RegDisplay.pRsi;
    m_funcletPtrs.pRbx = m_RegDisplay.pRbx;

    SP = (PTR_UIntNative)(m_RegDisplay.SP + 0x4);

    m_RegDisplay.pRdi = SP++;
    m_RegDisplay.pRsi = SP++;
    m_RegDisplay.pRbx = SP++;
    m_RegDisplay.pRbp = SP++;

#elif defined(TARGET_ARM)
    // RhpCallCatchFunclet puts a couple of extra things on the stack that aren't put there by the other two
    // thunks, but we don't need to know what they are here, so we just skip them.
    UIntNative uOffsetToR4 = EQUALS_CODE_ADDRESS(m_ControlPC, RhpCallCatchFunclet2) ? 0xC : 0x4;

    // Save the preserved regs portion of the REGDISPLAY across the unwind through the C# EH dispatch code.
    m_funcletPtrs.pR4  = m_RegDisplay.pR4;
    m_funcletPtrs.pR5  = m_RegDisplay.pR5;
    m_funcletPtrs.pR6  = m_RegDisplay.pR6;
    m_funcletPtrs.pR7  = m_RegDisplay.pR7;
    m_funcletPtrs.pR8  = m_RegDisplay.pR8;
    m_funcletPtrs.pR9  = m_RegDisplay.pR9;
    m_funcletPtrs.pR10 = m_RegDisplay.pR10;
    m_funcletPtrs.pR11 = m_RegDisplay.pR11;

    SP = (PTR_UIntNative)(m_RegDisplay.SP + uOffsetToR4);

    m_RegDisplay.pR4  = SP++;
    m_RegDisplay.pR5  = SP++;
    m_RegDisplay.pR6  = SP++;
    m_RegDisplay.pR7  = SP++;
    m_RegDisplay.pR8  = SP++;
    m_RegDisplay.pR9  = SP++;
    m_RegDisplay.pR10 = SP++;
    m_RegDisplay.pR11 = SP++;
#else
    SP = (PTR_UIntNative)(m_RegDisplay.SP);
    ASSERT_UNCONDITIONALLY("NYI for this arch");
#endif
    m_RegDisplay.SetAddrOfIP((PTR_PCODE)SP);
    m_RegDisplay.SetIP(*SP++);
    m_RegDisplay.SetSP((UIntNative)dac_cast<TADDR>(SP));
    m_ControlPC = dac_cast<PTR_VOID>(*(m_RegDisplay.pIP));

    // We expect to be called by the runtime's C# EH implementation, and since this function's notion of how 
    // to unwind through the stub is brittle relative to the stub itself, we want to check as soon as we can.
    ASSERT(m_pInstance->FindCodeManagerByAddress(m_ControlPC) && "unwind from funclet invoke stub failed");

    return true;
#endif // defined(USE_PORTABLE_HELPERS)
}

#ifdef _AMD64_
#define STACK_ALIGN_SIZE 16
#elif defined(_ARM_)
#define STACK_ALIGN_SIZE 8
#elif defined(_X86_)
#define STACK_ALIGN_SIZE 4
#endif

#ifdef TARGET_AMD64
struct CALL_DESCR_CONTEXT
{
    UIntNative  Rbp;
    UIntNative  Rsi;
    UIntNative  Rbx;
    UIntNative  IP;
};
#elif defined(TARGET_ARM)
struct CALL_DESCR_CONTEXT
{
    UIntNative  R4;
    UIntNative  R5;
    UIntNative  R7;
    UIntNative  IP;
};
#elif defined(TARGET_X86)
struct CALL_DESCR_CONTEXT
{
    UIntNative  Rbx;
    UIntNative  Rbp;
    UIntNative  IP;
};
#else
#error NYI - For this arch
#endif

typedef DPTR(CALL_DESCR_CONTEXT) PTR_CALL_DESCR_CONTEXT;

bool StackFrameIterator::HandleCallDescrThunk()
{
    ASSERT((m_dwFlags & MethodStateCalculated) == 0);

#if defined(USE_PORTABLE_HELPERS) // Corresponding helper code is only defined in assembly code
    return false;
#else // defined(USE_PORTABLE_HELPERS)
    if (true
#if defined(FEATURE_DYNAMIC_CODE)
        && !EQUALS_CODE_ADDRESS(m_ControlPC, ReturnFromCallDescrThunk)
#endif
        )
    {
        return false;
    }
    
    UIntNative newSP;
#ifdef TARGET_AMD64
    // RBP points to the SP that we want to capture. (This arrangement allows for
    // the arguments from this function to be loaded into memory with an adjustment
    // to SP, like an alloca
    newSP = *(PTR_UIntNative)m_RegDisplay.pRbp;

    PTR_CALL_DESCR_CONTEXT pContext = (PTR_CALL_DESCR_CONTEXT)newSP;

    m_RegDisplay.pRbp = PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, Rbp);
    m_RegDisplay.pRsi = PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, Rsi);
    m_RegDisplay.pRbx = PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, Rbx);

    // And adjust SP to be the state that it should be in just after returning from
    // the CallDescrFunction
    newSP += sizeof(CALL_DESCR_CONTEXT);
#elif defined(TARGET_ARM)
    // R7 points to the SP that we want to capture. (This arrangement allows for
    // the arguments from this function to be loaded into memory with an adjustment
    // to SP, like an alloca
    newSP = *(PTR_UIntNative)m_RegDisplay.pR7;
    PTR_CALL_DESCR_CONTEXT pContext = (PTR_CALL_DESCR_CONTEXT)newSP;

    m_RegDisplay.pR4 = PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, R4);
    m_RegDisplay.pR5 = PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, R5);
    m_RegDisplay.pR7 = PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, R7);

    // And adjust SP to be the state that it should be in just after returning from
    // the CallDescrFunction
    newSP += sizeof(CALL_DESCR_CONTEXT);
#elif defined(TARGET_X86)
    // RBP points to the SP that we want to capture. (This arrangement allows for
    // the arguments from this function to be loaded into memory with an adjustment
    // to SP, like an alloca
    newSP = *(PTR_UIntNative)m_RegDisplay.pRbp;

    PTR_CALL_DESCR_CONTEXT pContext = (PTR_CALL_DESCR_CONTEXT)(newSP - offsetof(CALL_DESCR_CONTEXT, Rbp));

    m_RegDisplay.pRbp = PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, Rbp);
    m_RegDisplay.pRbx = PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, Rbx);

    // And adjust SP to be the state that it should be in just after returning from
    // the CallDescrFunction
    newSP += sizeof(CALL_DESCR_CONTEXT) - offsetof(CALL_DESCR_CONTEXT, Rbp);
#else
    ASSERT_UNCONDITIONALLY("NYI for this arch");
#endif

    m_RegDisplay.SetAddrOfIP(PTR_TO_MEMBER(CALL_DESCR_CONTEXT, pContext, IP));
    m_RegDisplay.SetIP(pContext->IP);
    m_RegDisplay.SetSP(newSP);
    m_ControlPC = dac_cast<PTR_VOID>(pContext->IP);

    // We expect the call site to be in managed code, and since this function's notion of how to unwind 
    // through the stub is brittle relative to the stub itself, we want to check as soon as we can.
    ASSERT(m_pInstance->FindCodeManagerByAddress(m_ControlPC) && "unwind from CallDescrThunkStub failed");

    return true;
#endif // defined(USE_PORTABLE_HELPERS)
}

bool StackFrameIterator::HandleThrowSiteThunk()
{
    ASSERT((m_dwFlags & MethodStateCalculated) == 0);

#if defined(USE_PORTABLE_HELPERS) // @TODO: no portable version of throw helpers
    return false;
#else // defined(USE_PORTABLE_HELPERS)
    if (!EQUALS_CODE_ADDRESS(m_ControlPC, RhpThrowEx2) && 
        !EQUALS_CODE_ADDRESS(m_ControlPC, RhpThrowHwEx2) &&
        !EQUALS_CODE_ADDRESS(m_ControlPC, RhpRethrow2))
    {
        return false;
    }

    const UIntNative STACKSIZEOF_ExInfo = ((sizeof(ExInfo) + (STACK_ALIGN_SIZE-1)) & ~(STACK_ALIGN_SIZE-1));
#ifdef TARGET_AMD64
    const UIntNative SIZEOF_OutgoingScratch = 0x20;
#else
    const UIntNative SIZEOF_OutgoingScratch = 0;
#endif

    PTR_PAL_LIMITED_CONTEXT pContext = (PTR_PAL_LIMITED_CONTEXT)
                                        (m_RegDisplay.SP + SIZEOF_OutgoingScratch + STACKSIZEOF_ExInfo);

#ifdef TARGET_AMD64
    m_RegDisplay.pRbp = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, Rbp);
    m_RegDisplay.pRdi = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, Rdi);
    m_RegDisplay.pRsi = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, Rsi);
    m_RegDisplay.pRbx = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, Rbx);
    m_RegDisplay.pR12 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R12);
    m_RegDisplay.pR13 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R13);
    m_RegDisplay.pR14 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R14);
    m_RegDisplay.pR15 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R15);
#elif defined(TARGET_ARM)
    m_RegDisplay.pR4  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R4);
    m_RegDisplay.pR5  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R5);
    m_RegDisplay.pR6  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R6);
    m_RegDisplay.pR7  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R7);
    m_RegDisplay.pR8  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R8);
    m_RegDisplay.pR9  = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R9);
    m_RegDisplay.pR10 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R10);
    m_RegDisplay.pR11 = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, R11);
#elif defined(TARGET_X86)
    m_RegDisplay.pRbp = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, Rbp);
    m_RegDisplay.pRdi = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, Rdi);
    m_RegDisplay.pRsi = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, Rsi);
    m_RegDisplay.pRbx = PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, Rbx);
#else
    ASSERT_UNCONDITIONALLY("NYI for this arch");
#endif

    m_RegDisplay.SetAddrOfIP(PTR_TO_MEMBER(PAL_LIMITED_CONTEXT, pContext, IP));
    m_RegDisplay.SetIP(pContext->IP);
    m_RegDisplay.SetSP(pContext->GetSp());
    m_ControlPC = dac_cast<PTR_VOID>(pContext->IP);

    // We expect the throw site to be in managed code, and since this function's notion of how to unwind 
    // through the stub is brittle relative to the stub itself, we want to check as soon as we can.
    ASSERT(m_pInstance->FindCodeManagerByAddress(m_ControlPC) && "unwind from throw site stub failed");

    return true;
#endif // defined(USE_PORTABLE_HELPERS)
}

// If our control PC indicates that we're in one of the thunks we use to make managed callouts from the
// runtime we need to adjust the frame state to that of the managed method that previously called into the
// runtime (i.e. skip the intervening unmanaged frames). Returns true if such a sequence of unmanaged frames
// was skipped.

bool StackFrameIterator::HandleManagedCalloutThunk()
{
    return HandleManagedCalloutThunk(m_ControlPC, m_RegDisplay.GetFP());
}


bool StackFrameIterator::HandleManagedCalloutThunk(PTR_VOID controlPC, UIntNative framePointer)
{
#if defined(USE_PORTABLE_HELPERS) // @TODO: no portable version of managed callout defined
    return false;
#else // defined(USE_PORTABLE_HELPERS)
    if (EQUALS_CODE_ADDRESS(controlPC,ReturnFromManagedCallout2)

#if defined(FEATURE_DYNAMIC_CODE)
     || EQUALS_CODE_ADDRESS(controlPC, ReturnFromUniversalTransition)
#endif

        )
    {
        // We're in a special thunk we use to call into managed code from unmanaged code in the runtime. This
        // thunk sets up an FP frame with a pointer to a PInvokeTransitionFrame erected by the managed method
        // which called into the runtime in the first place (actually a stub called by that managed method).
        // Thus we can unwind from one managed method to the previous one, skipping all the unmanaged frames
        // in the middle.
        //
        // On all architectures this transition frame pointer is pushed at a well-known offset from FP.
        PTR_VOID pEntryToRuntimeFrame = *(PTR_PTR_VOID)(framePointer +
                                                     MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET);
        InternalInit(m_pThread, GetPInvokeTransitionFrame(pEntryToRuntimeFrame));
        ASSERT(m_pInstance->FindCodeManagerByAddress(m_ControlPC));

        // Additionally the initial managed method (the one that called into the runtime) may have pushed some
        // arguments containing GC references on the stack. Since the managed callout initiated by the runtime
        // has an unrelated signature, there's nobody reporting any of these references to the GC. To avoid
        // having to store signature information for what might be potentially a lot of methods (we use this
        // mechanism for certain edge cases in interface invoke) we conservatively report a range of the stack
        // that might contain GC references. Such references will be in either the outgoing stack argument
        // slots of the calling method or in argument registers spilled to the stack in the prolog of the stub
        // they use to call into the runtime.
        //
        // The lower bound of this range we define as the transition frame itself. We just computed this
        // address and it's guaranteed to be lower than (but quite close to) that of any spilled argument
        // register (see comments in the various versions of RhpInterfaceDispatchSlow). The upper bound we
        // can't quite compute just yet. Because the managed method may not have an FP frame it's difficult to
        // put a bound on the location of its outgoing argument area. Instead we'll wait until the next frame
        // and use the caller's SP at the point of the call into this method.
        ASSERT(m_pConservativeStackRangeLowerBound == NULL);
        ASSERT(m_pConservativeStackRangeUpperBound == NULL);
        m_pConservativeStackRangeLowerBound = (PTR_RtuObjectRef)pEntryToRuntimeFrame;

        return true;
    }
#if defined(FEATURE_DYNAMIC_CODE)
    else if (EQUALS_CODE_ADDRESS(controlPC, ReturnFromCallDescrThunk))
    {
        HandleCallDescrThunk();
        ASSERT(m_pInstance->FindCodeManagerByAddress(m_ControlPC));

        // RhCallDescrWorker is called from library code (called from RuntimeAugments.CallDescrWorker), not user code
        // It does not need conservative reporting.
        // CallDescrWorker takes a fixed set of simple and known arguments (not arbitrary, like the arguments 
        // to the universal thunk) and, therefore, does not need conservative scanning

        ASSERT(m_pConservativeStackRangeLowerBound == NULL);
        ASSERT(m_pConservativeStackRangeUpperBound == NULL);

        return true;
    }
#endif

    return false;
#endif // defined(USE_PORTABLE_HELPERS)
}

bool StackFrameIterator::IsValid()
{
    return (m_ControlPC != 0);
}

#ifdef DACCESS_COMPILE
#define FAILFAST_OR_DAC_FAIL(x) if(!(x)) { DacError(E_FAIL); }
#else
#define FAILFAST_OR_DAC_FAIL(x) if(!(x)) { ASSERT_UNCONDITIONALLY(#x); RhFailFast(); }
#endif

void StackFrameIterator::Next()
{
    NextInternal();
    STRESS_LOG1(LF_STACKWALK, LL_INFO10000, "   %p\n", m_ControlPC);
}

void StackFrameIterator::NextInternal()
{
    PTR_VOID collapsingTargetFrame = NULL;
KeepUnwinding:
    m_dwFlags &= ~(ExCollide|MethodStateCalculated|UnwoundReversePInvoke);
    ASSERT(IsValid());

    m_pHijackedReturnValue = NULL;
    m_HijackedReturnValueKind = GCRK_Unknown;

#ifdef _DEBUG
    m_ControlPC = dac_cast<PTR_VOID>((void*)666);
#endif // _DEBUG

    bool fJustComputedConservativeLowerStackBound = false;

    // If we published a stack range to report to the GC conservatively in the last frame enumeration clear it
    // now to make way for building another one if required.
    if ((m_pConservativeStackRangeLowerBound != NULL) && (m_pConservativeStackRangeUpperBound != NULL))
    {
        m_pConservativeStackRangeLowerBound = NULL;
        m_pConservativeStackRangeUpperBound = NULL;
    }

#if defined(_DEBUG) && !defined(DACCESS_COMPILE)
    UIntNative DEBUG_preUnwindSP = m_RegDisplay.GetSP();
#endif

    PTR_VOID pPreviousTransitionFrame;
    FAILFAST_OR_DAC_FAIL(GetCodeManager()->UnwindStackFrame(&m_methodInfo, m_codeOffset, &m_RegDisplay, &pPreviousTransitionFrame));

    if (pPreviousTransitionFrame != NULL)
    {
        if (pPreviousTransitionFrame == TOP_OF_STACK_MARKER)
        {
            m_ControlPC = 0;
        }
        else
        {
            InternalInit(m_pThread, GetPInvokeTransitionFrame(pPreviousTransitionFrame));
            ASSERT(m_pInstance->FindCodeManagerByAddress(m_ControlPC));
        }
        m_dwFlags |= UnwoundReversePInvoke;
    }
    else
    {
        // if the thread is safe to walk, it better not have a hijack in place.
        ASSERT((ThreadStore::GetCurrentThread() == m_pThread) || !m_pThread->DangerousCrossThreadIsHijacked());

        m_ControlPC = dac_cast<PTR_VOID>(*(m_RegDisplay.GetAddrOfIP()));

        //
        // BEWARE: these side-effect the current m_RegDisplay and m_ControlPC
        //
        HandleCallDescrThunk();
        bool atThrowSiteThunk = HandleThrowSiteThunk();
        bool isExceptionallyInvokedFunclet = HandleFuncletInvokeThunk();
        ASSERT(!isExceptionallyInvokedFunclet || GetCodeManager()->IsFunclet(&m_methodInfo));

        UIntNative postUnwindSP = m_RegDisplay.SP;

        bool exCollide = (m_dwFlags & CollapseFunclets) 
                                ? m_pNextExInfo && (postUnwindSP > ((UIntNative)dac_cast<TADDR>(m_pNextExInfo)))
                                : isExceptionallyInvokedFunclet;

        // If our control PC indicates that we're in one of the thunks we use to make managed callouts from 
        // the runtime we need to adjust the frame state to that of the managed method that previously called 
        // into the runtime (i.e. skip the intervening unmanaged frames).
        if (HandleManagedCalloutThunk())
        {
            // Set this flag so we don't immediately try to compute the upper bound from this frame in the
            // code below.
            fJustComputedConservativeLowerStackBound = true;
        }
        else if (exCollide)
        {
            // OK, so we just hit (collided with) an exception throw point.  We continue by consulting the 
            // ExInfo.

            // Double-check that we collide only at boundaries where we would have walked off into unmanaged
            // code frames.  In the GC stackwalk, this means walking all the way off the end of the managed
            // exception dispatch code to the throw site.  In the EH stackwalk, this means hitting the special 
            // funclet invoke ASM thunks.
            ASSERT(atThrowSiteThunk || isExceptionallyInvokedFunclet);
            atThrowSiteThunk;   // reference the variable so that retail builds don't see warnings-as-errors.

            // Double-check that when we are 'collapsing' funclets, we always see the same frame pointer.  If
            // we don't, then we will be missing frames we should be reporting.
            ASSERT(!collapsingTargetFrame || collapsingTargetFrame == m_FramePointer);

            // Double-check that the ExInfo that is being consulted is at or below the 'current' stack pointer
            ASSERT(DEBUG_preUnwindSP <= (UIntNative)m_pNextExInfo);

            collapsingTargetFrame = HandleExCollide(m_pNextExInfo, collapsingTargetFrame);
            if (collapsingTargetFrame != 0)
            {
                STRESS_LOG1(LF_STACKWALK, LL_INFO10000, "[ KeepUnwinding, target FP = %p ]\n", collapsingTargetFrame);
                goto KeepUnwinding;
            }

            m_dwFlags |= ExCollide;
        }
        else
        {
            ASSERT(m_pInstance->FindCodeManagerByAddress(m_ControlPC));
        }
        
        if (m_dwFlags & ApplyReturnAddressAdjustment)
            m_ControlPC = AdjustReturnAddressBackward(m_ControlPC);
    }

    if ((m_pConservativeStackRangeLowerBound != NULL) && !fJustComputedConservativeLowerStackBound)
    {
        // See comment above where we set m_pConservativeStackRangeLowerBound. In the previous frame
        // we started computing a stack range to report to the GC conservatively. Now we've unwound we
        // can use the current value of SP as the upper bound. Setting this value will cause
        // HasStackRangeToReportConservatively() to return true, which will cause our caller to call
        // GetStackRangeToReportConservatively() to retrieve the range values.
        //
        // The only case where we can't do this is when we fell off the end of the stack (m_ControlPC == 0).
        // This happens only after a reverse p/invoke method (since that's the only way we could have gotten
        // into managed code to begin with). Luckily those cases require an FP frame so we can compute the
        // upper bound from that. The odd case here is ARM where the FP register can end up pointing into the
        // middle of the outgoing argument area of the frame. In this case we'll use the OS frame pointer
        // (r11) which acts very much like ebp/rbp on the other architectures.
        ASSERT(m_pConservativeStackRangeUpperBound == NULL);

        if (m_ControlPC != 0)
        {
            m_pConservativeStackRangeUpperBound = (PTR_RtuObjectRef)m_RegDisplay.GetSP();
        }
        else
        {
#ifdef TARGET_ARM
            m_pConservativeStackRangeUpperBound = (PTR_RtuObjectRef)*m_RegDisplay.pR11;
#else
            m_pConservativeStackRangeUpperBound = (PTR_RtuObjectRef)m_RegDisplay.GetFP();
#endif
        }
    }
}

REGDISPLAY * StackFrameIterator::GetRegisterSet()
{
    ASSERT(IsValid());
    return &m_RegDisplay;
}

UInt32 StackFrameIterator::GetCodeOffset()
{
    ASSERT(IsValid());
    return m_codeOffset;
}

ICodeManager * StackFrameIterator::GetCodeManager()
{
    ASSERT(IsValid());
    return m_pCodeManager;
}

MethodInfo * StackFrameIterator::GetMethodInfo()
{
    ASSERT(IsValid());
    return &m_methodInfo;
}

#ifdef DACCESS_COMPILE
#define FAILFAST_OR_DAC_RETURN_FALSE(x) if(!(x)) return false;
#else
#define FAILFAST_OR_DAC_RETURN_FALSE(x) if(!(x)) { ASSERT_UNCONDITIONALLY(#x); RhFailFast(); }
#endif

void StackFrameIterator::CalculateCurrentMethodState()
{
    if (m_dwFlags & MethodStateCalculated)
        return;

    // Assume that the caller is likely to be in the same module
    if (m_pCodeManager == NULL || !m_pCodeManager->FindMethodInfo(m_ControlPC, &m_methodInfo, &m_codeOffset))
    {
        m_pCodeManager = m_pInstance->FindCodeManagerByAddress(m_ControlPC);
        FAILFAST_OR_DAC_FAIL(m_pCodeManager);

        FAILFAST_OR_DAC_FAIL(m_pCodeManager->FindMethodInfo(m_ControlPC, &m_methodInfo, &m_codeOffset));
    }

    m_FramePointer = GetCodeManager()->GetFramePointer(&m_methodInfo, &m_RegDisplay);

    m_dwFlags |= MethodStateCalculated;
}

bool StackFrameIterator::GetHijackedReturnValueLocation(PTR_RtuObjectRef * pLocation, GCRefKind * pKind)
{
    if (GCRK_Unknown == m_HijackedReturnValueKind)
        return false;

    ASSERT((GCRK_Object == m_HijackedReturnValueKind) || (GCRK_Byref == m_HijackedReturnValueKind));

    *pLocation = m_pHijackedReturnValue;
    *pKind = m_HijackedReturnValueKind;
    return true;
}

bool StackFrameIterator::IsValidReturnAddress(PTR_VOID pvAddress)
{
#if !defined(USE_PORTABLE_HELPERS) // @TODO: no portable version of these helpers defined
    // These are return addresses into functions that call into managed (non-funclet) code, so we might see
    // them as hijacked return addresses.

    if (EQUALS_CODE_ADDRESS(pvAddress, ReturnFromManagedCallout2))
        return true;

#if defined(FEATURE_DYNAMIC_CODE)
    if (EQUALS_CODE_ADDRESS(pvAddress, ReturnFromUniversalTransition) ||
        EQUALS_CODE_ADDRESS(pvAddress, ReturnFromCallDescrThunk))
    {
        return true;
    }
#endif

    if (EQUALS_CODE_ADDRESS(pvAddress, RhpThrowEx2) ||
        EQUALS_CODE_ADDRESS(pvAddress, RhpThrowHwEx2) ||
        EQUALS_CODE_ADDRESS(pvAddress, RhpRethrow2))
    {
        return true;
    }
#endif // !defined(USE_PORTABLE_HELPERS)

    return (NULL != GetRuntimeInstance()->FindCodeManagerByAddress(pvAddress));
}

// Support for conservatively reporting GC references in a stack range. This is used when managed methods with
// an unknown signature potentially including GC references call into the runtime and we need to let a GC
// proceed (typically because we call out into managed code again). Instead of storing signature metadata for
// every possible managed method that might make such a call we identify a small range of the stack that might
// contain outgoing arguments. We then report every pointer that looks like it might refer to the GC heap as a
// fixed interior reference.
//
// We discover the lower and upper bounds of this region over the processing of two frames: the lower bound
// first as we discover the transition frame of the method that entered the runtime (typically as a result or
// enumerating from the managed method that the runtime subsequently called out to) and the upper bound as we
// unwind that method back to its caller. We could do it in one frame if we could guarantee that the call into
// the runtime originated from a managed method with a frame pointer, but we can't make that guarantee (the
// current usage of this mechanism involves methods that simply make an interface call, on the slow path where
// we might have to make a managed callout on the ICastable interface). Thus we need to wait for one more
// unwind to use the caller's SP as a conservative estimate of the upper bound.

bool StackFrameIterator::HasStackRangeToReportConservatively()
{
    // When there's no range to report both the lower and upper bounds will be NULL. When we start to build
    // the range the lower bound will become non-NULL first, followed by the upper bound on the next frame, at
    // which point we have a range to report.
    return m_pConservativeStackRangeUpperBound != NULL;
}

void StackFrameIterator::GetStackRangeToReportConservatively(PTR_RtuObjectRef * ppLowerBound, PTR_RtuObjectRef * ppUpperBound)
{
    ASSERT(HasStackRangeToReportConservatively());
    *ppLowerBound = m_pConservativeStackRangeLowerBound;
    *ppUpperBound = m_pConservativeStackRangeUpperBound;
}

// helpers to ApplyReturnAddressAdjustment
// The adjustment is made by EH to ensure that the ControlPC of a callsite stays within the containing try region.
// We adjust by the minimum instruction size on the target-architecture (1-byte on x86 and AMD64, 2-bytes on ARM)
PTR_VOID StackFrameIterator::AdjustReturnAddressForward(PTR_VOID controlPC)
{
#ifdef TARGET_ARM
    return (PTR_VOID)(((PTR_UInt8)controlPC) + 2);
#else
    return (PTR_VOID)(((PTR_UInt8)controlPC) + 1);
#endif
}
PTR_VOID StackFrameIterator::AdjustReturnAddressBackward(PTR_VOID controlPC)
{
#ifdef TARGET_ARM
    return (PTR_VOID)(((PTR_UInt8)controlPC) - 2);
#else
    return (PTR_VOID)(((PTR_UInt8)controlPC) - 1);
#endif
}

#ifndef DACCESS_COMPILE

COOP_PINVOKE_HELPER(Boolean, RhpSfiInit, (StackFrameIterator* pThis, PAL_LIMITED_CONTEXT* pStackwalkCtx))
{
    Thread * pCurThread = ThreadStore::GetCurrentThread();

    // The stackwalker is intolerant to hijacked threads, as it is largely expecting to be called from C++
    // where the hijack state of the thread is invariant.  Because we've exposed the iterator out to C#, we 
    // need to unhijack every time we callback into C++ because the thread could have been hijacked during our
    // time exectuing C#.
    pCurThread->Unhijack();

    // Passing NULL is a special-case to request a standard managed stack trace for the current thread.
    if (pStackwalkCtx == NULL)
        pThis->InternalInitForStackTrace();
    else
        pThis->InternalInitForEH(pCurThread, pStackwalkCtx);

    bool isValid = pThis->IsValid();
    if (isValid)
        pThis->CalculateCurrentMethodState();
    return isValid ? Boolean_true : Boolean_false;
}

COOP_PINVOKE_HELPER(Boolean, RhpSfiNext, (StackFrameIterator* pThis, UInt32* puExCollideClauseIdx, Boolean* pfUnwoundReversePInvoke))
{
    // The stackwalker is intolerant to hijacked threads, as it is largely expecting to be called from C++
    // where the hijack state of the thread is invariant.  Because we've exposed the iterator out to C#, we 
    // need to unhijack every time we callback into C++ because the thread could have been hijacked during our
    // time exectuing C#.
    ThreadStore::GetCurrentThread()->Unhijack();

    const UInt32 MaxTryRegionIdx = 0xFFFFFFFF;

    ExInfo * pCurExInfo = pThis->m_pNextExInfo;
    pThis->Next();
    bool isValid = pThis->IsValid();
    if (isValid)
        pThis->CalculateCurrentMethodState();

    if (pThis->m_dwFlags & StackFrameIterator::ExCollide)
    {
        ASSERT(pCurExInfo->m_idxCurClause != MaxTryRegionIdx);
        *puExCollideClauseIdx = pCurExInfo->m_idxCurClause;
        pCurExInfo->m_kind = (ExKind)(pCurExInfo->m_kind | EK_SuperscededFlag);
    }
    else
    {
        *puExCollideClauseIdx = MaxTryRegionIdx;
    }

    *pfUnwoundReversePInvoke = (pThis->m_dwFlags & StackFrameIterator::UnwoundReversePInvoke) 
                                    ? Boolean_true 
                                    : Boolean_false;
    return isValid;
}

#endif // !DACCESS_COMPILE
