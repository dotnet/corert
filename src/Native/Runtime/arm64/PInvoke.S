// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#include <unixasmmacros.inc>
#include "AsmOffsets.inc"

.global RhpTrapThreads

// Note: these must match the defs in PInvokeTransitionFrameFlags defined in rhbinder.h
PTFF_SAVE_SP            = 0x00000400

// Bit position for the flags above, to be used with tbz / tbnz instructions
PTFF_THREAD_ABORT_BIT   = 36

// Bit position for the flags above, to be used with tbz/tbnz instructions
TSF_Attached_Bit                = 0
TSF_SuppressGcStress_Bit        = 3
TSF_DoNotTriggerGc_Bit          = 4

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// RhpWaitForSuspend -- rare path for RhpPInvoke and RhpReversePInvokeReturn
//
//
// INPUT: none
//
// TRASHES: none
// 
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
    NESTED_ENTRY RhpWaitForSuspend, _TEXT, NoHandler

        // FP and LR registers
        PROLOG_SAVE_REG_PAIR_INDEXED   fp, lr, #-0xA0            // Push down stack pointer and store FP and LR

        // Need to save argument registers x0-x7 and the return buffer register x8
        // Also save x9 which may be used for saving indirect call target
        stp         x0, x1, [sp, #0x10]
        stp         x2, x3, [sp, #0x20]
        stp         x4, x5, [sp, #0x30]
        stp         x6, x7, [sp, #0x40]
        stp         x8, x9, [sp, #0x50]

        // Save float argument registers as well since they are volatile
        stp         d0, d1, [sp, #0x60]
        stp         d2, d3, [sp, #0x70]
        stp         d4, d5, [sp, #0x80]
        stp         d6, d7, [sp, #0x90]

        bl          RhpWaitForSuspend2

        // Restore floating point registers
        ldp            d0, d1, [sp, #0x60]
        ldp            d2, d3, [sp, #0x70]
        ldp            d4, d5, [sp, #0x80]
        ldp            d6, d7, [sp, #0x90]

        // Restore the argument registers
        ldp            x0, x1, [sp, #0x10]
        ldp            x2, x3, [sp, #0x20]
        ldp            x4, x5, [sp, #0x30]
        ldp            x6, x7, [sp, #0x40]
        ldp            x8, x9, [sp, #0x50]

        // Restore FP and LR registers, and free the allocated stack block
        EPILOG_RESTORE_REG_PAIR_INDEXED   fp, lr, #0xA0
        EPILOG_RETURN

    NESTED_END RhpWaitForSuspend, _TEXT

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// RhpWaitForGCNoAbort
//
//
// INPUT: x9: transition frame
//
// TRASHES: None
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
    NESTED_ENTRY RhpWaitForGCNoAbort, _TEXT, NoHandler

        // FP and LR registers
        PROLOG_SAVE_REG_PAIR_INDEXED   fp, lr, #-0x40            // Push down stack pointer and store FP and LR

        // Save the integer return registers, as well as the floating return registers
        stp         x0, x1, [sp, #0x10]
        stp         d0, d1, [sp, #0x20]
        stp         d2, d3, [sp, #0x30]

        ldr         x0, [x9, #OFFSETOF__PInvokeTransitionFrame__m_pThread]
        ldr         w0, [x0, #OFFSETOF__Thread__m_ThreadStateFlags]
        tbnz        x0, #TSF_DoNotTriggerGc_Bit, Done

        mov         x0, x9      // passing transition frame in x0
        bl          RhpWaitForGC2

Done:
        ldp         x0, x1, [sp, #0x10]
        ldp         d0, d1, [sp, #0x20]
        ldp         d2, d3, [sp, #0x30]
        EPILOG_RESTORE_REG_PAIR_INDEXED   fp, lr, #0x40
        EPILOG_RETURN

    NESTED_END RhpWaitForGCNoAbort

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// RhpWaitForGC
//
//
// INPUT: x9: transition frame
//
// TRASHES: x0, x1, x10
// 
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
    NESTED_ENTRY RhpWaitForGC, _TEXT, NoHandler

        PROLOG_SAVE_REG_PAIR_INDEXED    fp, lr, #-0x10

        PREPARE_EXTERNAL_VAR RhpTrapThreads, x10

        ldr         w10, [x10]
        tbz         x10, #TrapThreadsFlags_TrapThreads_Bit, NoWait
        bl          RhpWaitForGCNoAbort
NoWait:
        tbz         x10, #TrapThreadsFlags_AbortInProgress_Bit, NoAbort
        ldr         x10, [x9, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        tbz         x10, #PTFF_THREAD_ABORT_BIT, NoAbort

        EPILOG_RESTORE_REG_PAIR_INDEXED fp, lr, #0x10
        mov w0, #STATUS_REDHAWK_THREAD_ABORT
        mov x1, lr          // hijack target address as exception PC
        b RhpThrowHwEx        

NoAbort:
        EPILOG_RESTORE_REG_PAIR_INDEXED fp, lr, #0x10
        EPILOG_RETURN

    NESTED_END RhpWaitForGC, _TEXT

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// RhpReversePInvoke
//
// IN:  x9: address of reverse pinvoke frame
//                  0: save slot for previous M->U transition frame
//                  8: save slot for thread pointer to avoid re-calc in epilog sequence
//
// PRESERVES: x0 - x8 -- need to preserve these because the caller assumes they are not trashed
//
// TRASHES:   x10, x11
//
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
    LEAF_ENTRY RhpReversePInvoke, _TEXT

        INLINE_GETTHREAD x10    // x10 = Thread
        str         x10, [x9, #8]    // save Thread pointer for RhpReversePInvokeReturn

        // x9 = reverse pinvoke frame
        // x10 = thread
        // x11 = scratch

        ldr         w11, [x10, #OFFSETOF__Thread__m_ThreadStateFlags]
        tbz         x11, #TSF_Attached_Bit, AttachThread

ThreadAttached:
        //
        // Check for the correct mode.  This is accessible via various odd things that we cannot completely 
        // prevent such as :
        //     1) Registering a reverse pinvoke entrypoint as a vectored exception handler
        //     2) Performing a managed delegate invoke on a reverse pinvoke delegate.
        //
        ldr         x11, [x10, #OFFSETOF__Thread__m_pTransitionFrame]
        cbz         x11, CheckBadTransition

        // Save previous TransitionFrame prior to making the mode transition so that it is always valid 
        // whenever we might attempt to hijack this thread.
        str         x11, [x9]

        str         xzr, [x10, #OFFSETOF__Thread__m_pTransitionFrame] 
        dmb         ish

        PREPARE_EXTERNAL_VAR RhpTrapThreads, x11

        ldr         w11, [x11]
        tbnz        x11, #TrapThreadsFlags_TrapThreads_Bit, TrapThread

        ret

CheckBadTransition:
        // Allow 'bad transitions' in when the TSF_DoNotTriggerGc mode is set.  This allows us to have 
        // [NativeCallable] methods that are called via the "restricted GC callouts" as well as from native,
        // which is necessary because the methods are CCW vtable methods on interfaces passed to native.
        ldr         w11, [x10, #OFFSETOF__Thread__m_ThreadStateFlags]
        tbz         x11, #TSF_DoNotTriggerGc_Bit, BadTransition

        // zero-out our 'previous transition frame' save slot
        mov         x11, #0
        str         x11, [x9]

        // nothing more to do
        ret

TrapThread:
        // put the previous frame back (sets us back to preemptive mode)
        ldr         x11, [x9]
        str         x11, [x10, #OFFSETOF__Thread__m_pTransitionFrame] 
        dmb         ish

AttachThread:
        // passing address of reverse pinvoke frame in x9
        b           RhpReversePInvokeAttachOrTrapThread

BadTransition:
        mov         x0, lr  // arg <- return address
        b           RhpReversePInvokeBadTransition

    LEAF_END RhpReversePInvoke, _TEXT

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// RhpReversePInvokeAttachOrTrapThread -- rare path for RhpPInvoke
//
//
// INPUT: x9: address of reverse pinvoke frame
//
// PRESERVES: x0-x8 -- need to preserve these because the caller assumes they are not trashed
//
// TRASHES: none
// 
//////////////////////////////////////////////////////////////////////////////////////////////////////////////
    NESTED_ENTRY RhpReversePInvokeAttachOrTrapThread, _TEXT, NoHandler

        // FP and LR registers
        PROLOG_SAVE_REG_PAIR_INDEXED   fp, lr, #-0xA0            // Push down stack pointer and store FP and LR

        // Need to save argument registers x0-x7 and the return buffer register x8 (twice for 16B alignment)
        stp         x0, x1, [sp, #0x10]
        stp         x2, x3, [sp, #0x20]
        stp         x4, x5, [sp, #0x30]
        stp         x6, x7, [sp, #0x40]
        stp         x8, x8, [sp, #0x50]

        // Save float argument registers as well since they are volatile
        stp         d0, d1, [sp, #0x60]
        stp         d2, d3, [sp, #0x70]
        stp         d4, d5, [sp, #0x80]
        stp         d6, d7, [sp, #0x90]

        mov         x0, x9         // passing reverse pinvoke frame pointer in x0
        bl          RhpReversePInvokeAttachOrTrapThread2

        // Restore floating point registers
        ldp         d0, d1, [sp, #0x60]
        ldp         d2, d3, [sp, #0x70]
        ldp         d4, d5, [sp, #0x80]
        ldp         d6, d7, [sp, #0x90]

        // Restore the argument registers
        ldp         x0, x1, [sp, #0x10]
        ldp         x2, x3, [sp, #0x20]
        ldp         x4, x5, [sp, #0x30]
        ldp         x6, x7, [sp, #0x40]
        ldr         x8, [sp, #0x50] 

        // Restore FP and LR registers, and free the allocated stack block
        EPILOG_RESTORE_REG_PAIR_INDEXED   fp, lr, #0xA0
        EPILOG_RETURN

    NESTED_END RhpReversePInvokeTrapThread

//
// RhpPInvoke
//
// IN:  X0: address of pinvoke frame
//
// This helper assumes that its callsite is as good to start the stackwalk as the actual PInvoke callsite.
// The codegenerator must treat the callsite of this helper as GC triggering and generate the GC info for it.
// Also, the codegenerator must ensure that there are no live GC references in callee saved registers.
//

NESTED_ENTRY RhpPInvoke, _TEXT, NoHandler
        str     fp, [x0, #OFFSETOF__PInvokeTransitionFrame__m_FramePointer]
        str     lr, [x0, #OFFSETOF__PInvokeTransitionFrame__m_RIP]
        mov     x9, SP
        str     x9, [x0, #OFFSETOF__PInvokeTransitionFrame__m_PreservedRegs]
        mov     x9, #PTFF_SAVE_SP
        str     x9, [x0, #OFFSETOF__PInvokeTransitionFrame__m_Flags]

        // get TLS global variable address
        // r0 = GetThread()
        INLINE_GETTHREAD x10
        str     x10, [x0, #OFFSETOF__PInvokeTransitionFrame__m_pThread]
        str     x0, [x10, #OFFSETOF__Thread__m_pTransitionFrame]

        PREPARE_EXTERNAL_VAR RhpTrapThreads, x9

        ldr     w9, [x9]
        cbnz    w9, InvokeRareTrapThread  // TrapThreadsFlags_None = 0
        ret

InvokeRareTrapThread:
        b       C_FUNC(RhpWaitForSuspend2)
NESTED_END RhpPInvoke, _TEXT


LEAF_ENTRY RhpPInvokeReturn, _TEXT
        ldr         x9, [x0, #OFFSETOF__PInvokeTransitionFrame__m_pThread]
        mov         x10, 0
        str         x10, [x9, #OFFSETOF__Thread__m_pTransitionFrame]

        PREPARE_EXTERNAL_VAR RhpTrapThreads, x9

        ldr     w9, [x9]
        cbnz    w9, 0f  // TrapThreadsFlags_None = 0
        ret
0:
        // passing transition frame pointer in x0
        b         C_FUNC(RhpWaitForGC2)
LEAF_END RhpPInvokeReturn, _TEXT

