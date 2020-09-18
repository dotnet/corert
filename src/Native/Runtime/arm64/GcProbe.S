// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <unixasmmacros.inc>
#include "AsmOffsets.inc"

    .global      g_fGcStressStarted

PROBE_SAVE_FLAGS_EVERYTHING = (DEFAULT_FRAME_SAVE_FLAGS | PTFF_SAVE_ALL_SCRATCH)

    // Build a map of symbols representing offsets into the transition frame (see PInvokeTransitionFrame in
    // rhbinder.h) and keep these two in sync.

    //            field OFFSETOF__PInvokeTransitionFrame__m_PreservedRegs
    //            field 10 * 8 ; x19..x28
    //m_CallersSP field 8      ; SP at routine entry
    //            field 19 * 8 ; x0..x18
    //            field 8      ; lr
    //m_SavedNZCV field 8      ; Saved condition flags
    //            field 4 * 8  ; d0..d3
    //PROBE_FRAME_SIZE field 0

    m_CallersSP = OFFSETOF__PInvokeTransitionFrame__m_PreservedRegs + 10 * 8
    PROBE_FRAME_SIZE = OFFSETOF__PInvokeTransitionFrame__m_PreservedRegs + (10+1+19+1+1+4) * 8

    // Support for setting up a transition frame when performing a GC probe. In many respects this is very
    // similar to the logic in PUSH_COOP_PINVOKE_FRAME in AsmMacros.h. In most cases setting up the
    // transition frame comprises the entirety of the caller's prolog (and initial non-prolog code) and
    // similarly for the epilog. Those cases can be dealt with using PROLOG_PROBE_FRAME and EPILOG_PROBE_FRAME
    // defined below. For the special cases where additional work has to be done in the prolog we also provide
    // the lower level macros ALLOC_PROBE_FRAME, FREE_PROBE_FRAME and INIT_PROBE_FRAME that allow more control
    // to be asserted.
    //
    // Note that we currently employ a significant simplification of frame setup: we always allocate a
    // maximally-sized PInvokeTransitionFrame and save all of the registers. Depending on the caller this can
    // lead to up to 20 additional register saves (x0-x18, lr) or 160 bytes of stack space. I have done no
    // analysis to see whether any of the worst cases occur on performance sensitive paths and whether the
    // additional saves will show any measurable degradation.

    // Perform the parts of setting up a probe frame that can occur during the prolog (and indeed this macro
    // can only be called from within the prolog).
    .macro ALLOC_PROBE_FRAME extraStackSpace, saveFPRegisters

        // First create PInvokeTransitionFrame      
        PROLOG_SAVE_REG_PAIR_INDEXED fp, lr, -(PROBE_FRAME_SIZE + \extraStackSpace)      // Push down stack pointer and store FP and LR

        // Slot at [sp, #0x10] is reserved for Thread *
        // Slot at [sp, #0x18] is reserved for bitmask of saved registers

        // Save callee saved registers
        PROLOG_SAVE_REG_PAIR   x19, x20, 0x20
        PROLOG_SAVE_REG_PAIR   x21, x22, 0x30
        PROLOG_SAVE_REG_PAIR   x23, x24, 0x40
        PROLOG_SAVE_REG_PAIR   x25, x26, 0x50
        PROLOG_SAVE_REG_PAIR   x27, x28, 0x60

        // Slot at [sp, #0x70] is reserved for caller sp

        // Save the scratch registers 
        str         x0,       [sp, #0x78]
        stp         x1, x2,   [sp, #0x80]
        stp         x3, x4,   [sp, #0x90]
        stp         x5, x6,   [sp, #0xA0]
        stp         x7, x8,   [sp, #0xB0]
        stp         x9, x10,  [sp, #0xC0]
        stp         x11, x12, [sp, #0xD0]
        stp         x13, x14, [sp, #0xE0]
        stp         x15, x16, [sp, #0xF0]
        stp         x17, x18, [sp, #0x100]
        str         lr,       [sp, #0x110]

        // Slot at [sp, #0x118] is reserved for NZCV

        // Save the floating return registers
        .if \saveFPRegisters
            stp         d0, d1,   [sp, #0x120]
            stp         d2, d3,   [sp, #0x130]
        .endif

    .endm

    // Undo the effects of an ALLOC_PROBE_FRAME. This may only be called within an epilog. Note that all
    // registers are restored (apart for sp and pc), even volatiles.
    .macro FREE_PROBE_FRAME extraStackSpace, restoreFPRegisters

        // Restore the scratch registers 
        ldr          x0,       [sp, #0x78]
        ldp          x1, x2,   [sp, #0x80]
        ldp          x3, x4,   [sp, #0x90]
        ldp          x5, x6,   [sp, #0xA0]
        ldp          x7, x8,   [sp, #0xB0]
        ldp          x9, x10,  [sp, #0xC0]
        ldp          x11, x12, [sp, #0xD0]
        ldp          x13, x14, [sp, #0xE0]
        ldp          x15, x16, [sp, #0xF0]
        ldp          x17, x18, [sp, #0x100]
        ldr          lr,       [sp, #0x110]

        // Restore the floating return registers
        .if \restoreFPRegisters
            ldp          d0, d1,   [sp, #0x120]
            ldp          d2, d3,   [sp, #0x130]
        .endif

        // Restore callee saved registers
        EPILOG_RESTORE_REG_PAIR x19, x20, 0x20
        EPILOG_RESTORE_REG_PAIR x21, x22, 0x30
        EPILOG_RESTORE_REG_PAIR x23, x24, 0x40
        EPILOG_RESTORE_REG_PAIR x25, x26, 0x50
        EPILOG_RESTORE_REG_PAIR x27, x28, 0x60

        EPILOG_RESTORE_REG_PAIR_INDEXED fp, lr, (PROBE_FRAME_SIZE + \extraStackSpace)
    .endm

    // Complete the setup of a probe frame allocated with ALLOC_PROBE_FRAME with the initialization that can
    // occur only outside the prolog (includes linking the frame to the current Thread). This macro assumes SP
    // is invariant outside of the prolog.
    //
    //  threadReg     : register containing the Thread* (this will be preserved)
    //  trashReg      : register that can be trashed by this macro
    //  savedRegsMask : value to initialize m_Flags field with
    //  gcFlags       : value of gcref / gcbyref flags for saved registers
    //  frameSize     : total size of the method's stack frame (including probe frame size)
    //  maskConstant  : 1 if savedRegsMask is a constant value
    .macro INIT_PROBE_FRAME threadReg, trashReg, savedRegsMask, gcFlags, frameSize, maskConstant
         str         \threadReg, [sp, #OFFSETOF__PInvokeTransitionFrame__m_pThread]            // Thread *

        .if \maskConstant == 1
            movz    \trashReg, (\savedRegsMask & 0xFFFF)
            movk    \trashReg, (\savedRegsMask >> 16) & 0xFFFF, lsl 16
            movk    \trashReg, (\gcFlags) & 0xFFFF, lsl 32
            movk    \trashReg, (\gcFlags >> 16) & 0xFFFF, lsl 48
            str     \trashReg, [sp, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        .else
            str     \savedRegsMask, [sp, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        .endif

        add         \trashReg, sp, #\frameSize
        str         \trashReg, [sp, #m_CallersSP]
    .endm

    .macro PROLOG_PROBE_FRAME threadReg, trashReg, savedRegsMask, gcFlags, getThread = 0, maskConstant = 0

        // Define the method prolog, allocating enough stack space for the PInvokeTransitionFrame and saving
        // incoming register values into it.
        ALLOC_PROBE_FRAME 0, 1

        .if \getThread == 1
#if FEATURE_EMULATED_TLS
            bl RhpGetThread

            .ifnc \threadReg, x0
                mov \threadReg, x0
            .endif
#else
            INLINE_GETTHREAD \threadReg
#endif
        .endif

        // Perform the rest of the PInvokeTransitionFrame initialization.
        INIT_PROBE_FRAME \threadReg, \trashReg, \savedRegsMask, \gcFlags, PROBE_FRAME_SIZE, \maskConstant
        mov         \trashReg, sp
        str         \trashReg, [\threadReg, #OFFSETOF__Thread__m_pHackPInvokeTunnel]
    .endm

    // Simple macro to use when PROLOG_PROBE_FRAME was used to set up and initialize the prolog and
    // PInvokeTransitionFrame. This will define the epilog including a return via the restored LR.
    .macro EPILOG_PROBE_FRAME

        FREE_PROBE_FRAME 0, 1
        EPILOG_RETURN
    .endm

// In order to avoid trashing VFP registers across the loop hijack we must save all user registers, so that 
// registers used by the loop being hijacked will not be affected. Unlike ARM32 where neon registers (NQ0, ..., NQ15) 
// are fully covered by the floating point registers D0 ... D31, we have 32 neon registers Q0, ... Q31 on ARM64 
// which are not fully covered by the register D0 ... D31. Therefore we must explicitly save all Q registers.
EXTRA_SAVE_SIZE = (32*16)

    .macro ALLOC_LOOP_HIJACK_FRAME

        PROLOG_STACK_ALLOC EXTRA_SAVE_SIZE

        \\ Save all neon registers
        stp         q0, q1,   [sp]
        stp         q2, q3,   [sp, #0x20]
        stp         q4, q5,   [sp, #0x40]
        stp         q6, q7,   [sp, #0x60]
        stp         q8, q9,   [sp, #0x80]
        stp         q10, q11, [sp, #0xA0]
        stp         q12, q13, [sp, #0xC0]
        stp         q14, q15, [sp, #0xE0]
        stp         q16, q17, [sp, #0x100]
        stp         q18, q19, [sp, #0x120]
        stp         q20, q21, [sp, #0x140]
        stp         q22, q23, [sp, #0x160]
        stp         q24, q25, [sp, #0x180]
        stp         q26, q27, [sp, #0x1A0]
        stp         q28, q29, [sp, #0x1C0]
        stp         q30, q31, [sp, #0x1E0]
        
        ALLOC_PROBE_FRAME 0, 0
    .endm

    .macro FREE_LOOP_HIJACK_FRAME

        FREE_PROBE_FRAME 0, 0

        \\ restore all neon registers 
        ldp         q0, q1,   [sp]
        ldp         q2, q3,   [sp, #0x20]
        ldp         q4, q5,   [sp, #0x40]
        ldp         q6, q7,   [sp, #0x60]
        ldp         q8, q9,   [sp, #0x80]
        ldp         q10, q11, [sp, #0xA0]
        ldp         q12, q13, [sp, #0xC0]
        ldp         q14, q15, [sp, #0xE0]
        ldp         q16, q17, [sp, #0x100]
        ldp         q18, q19, [sp, #0x120]
        ldp         q20, q21, [sp, #0x140]
        ldp         q22, q23, [sp, #0x160]
        ldp         q24, q25, [sp, #0x180]
        ldp         q26, q27, [sp, #0x1A0]
        ldp         q28, q29, [sp, #0x1C0]
        ldp         q30, q31, [sp, #0x1E0]

        EPILOG_STACK_FREE EXTRA_SAVE_SIZE
    .endm

//
// Macro to clear the hijack state. This is safe to do because the suspension code will not Unhijack this 
// thread if it finds it at an IP that isn't managed code.
//
// Register state on entry:
//  x2: thread pointer
//  
// Register state on exit:
//
    .macro ClearHijackState

        // TODO ASSERT OFFSETOF__Thread__m_pvHijackedReturnAddress == (OFFSETOF__Thread__m_ppvHijackedReturnAddressLocation + 8)
        // Clear m_ppvHijackedReturnAddressLocation and m_pvHijackedReturnAddress
        stp         xzr, xzr, [x2, #OFFSETOF__Thread__m_ppvHijackedReturnAddressLocation]
        // Clear m_uHijackedReturnValueFlags
        str         xzr, [x2, #OFFSETOF__Thread__m_uHijackedReturnValueFlags]
    .endm

//
// The prolog for all GC suspension hijacks (normal and stress). Fixes up the hijacked return address, and 
// clears the hijack state.
//
// Register state on entry:
//  All registers correct for return to the original return address.
//  
// Register state on exit:
//  x2: thread pointer
//  x3: trashed
//  x12: transition frame flags for the return registers x0 and x1
//
    .macro FixupHijackedCallstack

        // x2 <- GetThread()
        // TODO INLINE_GETTHREAD x2
        
        //
        // Fix the stack by restoring the original return address
        //
        // TODO ASSERT OFFSETOF__Thread__m_uHijackedReturnValueFlags == (OFFSETOF__Thread__m_pvHijackedReturnAddress + 8)
        // Load m_pvHijackedReturnAddress and m_uHijackedReturnValueFlags
        ldp         lr, x12, [x2, #OFFSETOF__Thread__m_pvHijackedReturnAddress]

        ClearHijackState
    .endm

//
// Set the Thread state and wait for a GC to complete.
//
// Register state on entry:
//  x4: thread pointer
//  
// Register state on exit:
//  x4: thread pointer
//  All other registers trashed
//

    .global RhpWaitForGCNoAbort

    .macro WaitForGCCompletion

        ldr         w2, [x4, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         w2, #TSF_SuppressGcStress__OR__TSF_DoNotTriggerGC
        bne         0f

        ldr         x9, [x4, #OFFSETOF__Thread__m_pHackPInvokeTunnel]
        bl          RhpWaitForGCNoAbort
0:
    .endm

    .macro HijackTargetFakeProlog

        // This is a fake entrypoint for the method that 'tricks' the OS into calling our personality routine.
        // The code here should never be executed, and the unwind info is bogus, but we don't mind since the
        // stack is broken by the hijack anyway until after we fix it below.
        PROLOG_SAVE_REG_PAIR_INDEXED   fp, lr, -0x10
        nop                     // We also need a nop here to simulate the implied bl instruction.  Without 
                                // this, an OS-applied -4 will back up into the method prolog and the unwind 
                                // will not be applied as desired.

    .endm

//
//
//
// GC Probe Hijack targets
//
//
    .global RhpPInvokeExceptionGuard

    // TODO add handler RhpPInvokeExceptionGuard after we figured out how to avoid the relocation in the code dection this would cause
    NESTED_ENTRY RhpGcProbeHijackWrapper, _TEXT, NoHandler
        HijackTargetFakeProlog

    LABELED_RETURN_ADDRESS RhpGcProbeHijack

        FixupHijackedCallstack
        orr         x12, x12, #DEFAULT_FRAME_SAVE_FLAGS
        b           RhpGcProbe
    NESTED_END RhpGcProbeHijackWrapper, _TEXT

#ifdef FEATURE_GC_STRESS
//
//
// GC Stress Hijack targets
//
//
    LEAF_ENTRY RhpGcStressHijack
        FixupHijackedCallstack
        orr         x12, x12, #DEFAULT_FRAME_SAVE_FLAGS
        b           RhpGcStressProbe
    LEAF_END RhpGcStressHijack
//
// Worker for our GC stress probes.  Do not call directly!!  
// Instead, go through RhpGcStressHijack{Scalar|Object|Byref}. 
// This worker performs the GC Stress work and returns to the original return address.
//
// Register state on entry:
//  x0: hijacked function return value
//  x1: hijacked function return value
//  x2: thread pointer
//  w12: register bitmask
//
// Register state on exit:
//  Scratch registers, except for x0, have been trashed
//  All other registers restored as they were when the hijack was first reached.
//
    NESTED_ENTRY RhpGcStressProbe, _TEXT, NoHandler
        PROLOG_PROBE_FRAME x2, x3, x12, 

        bl          $REDHAWKGCINTERFACE__STRESSGC

        EPILOG_PROBE_FRAME
    NESTED_END RhpGcStressProbe
#endif // FEATURE_GC_STRESS

    LEAF_ENTRY RhpGcProbe
        PREPARE_EXTERNAL_VAR RhpTrapThreads,x3
        ldr         w3, [x3]
        tbnz        x3, #TrapThreadsFlags_TrapThreads_Bit, RhpGcProbeRare
        ret
    LEAF_END RhpGcProbe

    .global RhpThrowHwEx

    NESTED_ENTRY RhpGcProbeRare, _TEXT, NoHandler
        PROLOG_PROBE_FRAME x2, x3, x12, 

        mov         x4, x2
        WaitForGCCompletion

        ldr         x2, [sp, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        tbnz        x2, #PTFF_THREAD_ABORT_BIT, 1f

        EPILOG_PROBE_FRAME

1:        
        FREE_PROBE_FRAME 0, 1
        mov w0, #STATUS_REDHAWK_THREAD_ABORT
        mov x1, lr // return address as exception PC
        b RhpThrowHwEx
    NESTED_END RhpGcProbeRare

    LEAF_ENTRY RhpGcPoll
        // @todo: I'm assuming it's not OK to trash any register here. If that's not true we can optimize the
        // push/pops out of this fast path.
        str         x0, [sp, #-0x10]!
        PREPARE_EXTERNAL_VAR_INDIRECT_W RhpTrapThreads, 0
        tbnz        x0, #TrapThreadsFlags_TrapThreads_Bit, 0f
        ldr         x0, [sp], #0x10
        ret
0:
        ldr         x0, [sp], #0x10
        b           RhpGcPollRare
    LEAF_END RhpGcPoll

    NESTED_ENTRY RhpGcPollRare, _TEXT, NoHandler
        PROLOG_PROBE_FRAME x2, x3, PROBE_SAVE_FLAGS_EVERYTHING, 0, 1, 1

        // Unhijack this thread, if necessary.
        INLINE_THREAD_UNHIJACK x2, x0, x1       // trashes x0, x1

        mov         x4, x2
        WaitForGCCompletion

        EPILOG_PROBE_FRAME
    NESTED_END RhpGcPollRare

    LEAF_ENTRY RhpGcPollStress
        //
        // loop hijacking is used instead
        //
        brk 0xf000

    LEAF_END RhpGcPollStress


#ifdef FEATURE_GC_STRESS
    NESTED_ENTRY RhpHijackForGcStress, _TEXT, NoHandler
        // This function should be called from right before epilog

        // Push FP and LR, and allocate stack to hold PAL_LIMITED_CONTEXT structure and VFP return value registers
        PROLOG_SAVE_REG_PAIR_INDEXED    fp, lr, -(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)

        //
        // Setup a PAL_LIMITED_CONTEXT that looks like what you'd get if you had suspended this thread at the
        // IP after the call to this helper.
        //
        // This is very likely overkill since the calculation of the return address should only need SP and 
        // LR, but this is test code, so I'm not too worried about efficiency.
        //
        // Setup a PAL_LIMITED_CONTEXT on the stack 
        // {
            // FP and LR already pushed.
            stp         x0, x1, [sp, #0x10]
            PROLOG_SAVE_REG_PAIR    x19, x20, #0x20
            PROLOG_SAVE_REG_PAIR    x21, x22, #0x30
            PROLOG_SAVE_REG_PAIR    x23, x24, #0x40
            PROLOG_SAVE_REG_PAIR    x25, x26, #0x50
            PROLOG_SAVE_REG_PAIR    x27, x28, #0x60
            PROLOG_SAVE_REG         lr, #0x78

        // } end PAL_LIMITED_CONTEXT

        // Save VFP return value
        stp         d0, d1, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x00)]
        stp         d2, d3, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x10)]

        // Compute and save SP at callsite.
        add         x0, sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)   // +0x20 for the pushes right before the context struct
        str         x0, [sp, #OFFSETOF__PAL_LIMITED_CONTEXT__SP]

        mov         x0, sp      // Address of PAL_LIMITED_CONTEXT
        bl          $THREAD__HIJACKFORGCSTRESS

        // Restore return value registers (saved in PAL_LIMITED_CONTEXT structure)
        ldp         x0, x1, [sp, #0x10]

        // Restore VFP return value
        ldp         d0, d1, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x00)]
        ldp         d2, d3, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x10)]

        // Epilog
        EPILOG_RESTORE_REG_PAIR     x19, x20, #0x20
        EPILOG_RESTORE_REG_PAIR     x21, x22, #0x30
        EPILOG_RESTORE_REG_PAIR     x23, x24, #0x40
        EPILOG_RESTORE_REG_PAIR     x25, x26, #0x50
        EPILOG_RESTORE_REG_PAIR     x27, x28, #0x60
        EPILOG_RESTORE_REG_PAIR_INDEXED     fp, lr, (SIZEOF__PAL_LIMITED_CONTEXT + 0x20)
        EPILOG_RETURN

    NESTED_END RhpHijackForGcStress

    NESTED_ENTRY RhpHijackForGcStressLeaf, _TEXT, NoHandler
        // This should be jumped to, right before epilog
        // x9 has the return address (we don't care about trashing scratch regs at this point)

        // Push FP and LR, and allocate stack to hold PAL_LIMITED_CONTEXT structure and VFP return value registers
        PROLOG_SAVE_REG_PAIR_INDEXED    fp, lr, -(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)

        //
        // Setup a PAL_LIMITED_CONTEXT that looks like what you'd get if you had suspended this thread at the
        // IP after the call to this helper.
        //
        // This is very likely overkill since the calculation of the return address should only need SP and 
        // LR, but this is test code, so I'm not too worried about efficiency.
        //
        // Setup a PAL_LIMITED_CONTEXT on the stack 
        // {
            // FP and LR already pushed.
            stp         x0, x1, [sp, #0x10]
            PROLOG_SAVE_REG_PAIR    x19, x20, #0x20
            PROLOG_SAVE_REG_PAIR    x21, x22, #0x30
            PROLOG_SAVE_REG_PAIR    x23, x24, #0x40
            PROLOG_SAVE_REG_PAIR    x25, x26, #0x50
            PROLOG_SAVE_REG_PAIR    x27, x28, #0x60
            // PROLOG_SAVE_REG macro doesn't let to use scratch reg:
            str         x9, [sp, #0x78]           // this is return address from RhpHijackForGcStress; lr is return address for it's caller

        // } end PAL_LIMITED_CONTEXT

        // Save VFP return value
        stp         d0, d1, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x00)]
        stp         d2, d3, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x10)]

        // Compute and save SP at callsite.
        add         x0, sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)   // +0x20 for the pushes right before the context struct
        str         x0, [sp, #OFFSETOF__PAL_LIMITED_CONTEXT__SP]

        mov         x0, sp      // Address of PAL_LIMITED_CONTEXT
        bl          $THREAD__HIJACKFORGCSTRESS

        // Restore return value registers (saved in PAL_LIMITED_CONTEXT structure)
        ldp         x0, x1, [sp, #0x10]

        // Restore VFP return value
        ldp         d0, d1, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x00)]
        ldp         d2, d3, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x10)]

        // Epilog
        EPILOG_RESTORE_REG_PAIR     x19, x20, #0x20
        EPILOG_RESTORE_REG_PAIR     x21, x22, #0x30
        EPILOG_RESTORE_REG_PAIR     x23, x24, #0x40
        EPILOG_RESTORE_REG_PAIR     x25, x26, #0x50
        EPILOG_RESTORE_REG_PAIR     x27, x28, #0x60
            ldr          x9, [sp, #0x78]
        EPILOG_RESTORE_REG_PAIR_INDEXED     fp, lr, (SIZEOF__PAL_LIMITED_CONTEXT + 0x20)
             br           x9

    NESTED_END RhpHijackForGcStressLeaf

#endif // FEATURE_GC_STRESS

#if 0 // used by the binder only
//
// The following functions are _jumped_ to when we need to transfer control from one method to another for EH 
// dispatch. These are needed to properly coordinate with the GC hijacking logic. We are essentially replacing
// the return from the throwing method with a jump to the handler in the caller, but we need to be aware of 
// any return address hijack that may be in place for GC suspension. These routines use a quick test of the 
// return address against a specific GC hijack routine, and then fixup the stack pointer to what it would be 
// after a real return from the throwing method. Then, if we are not hijacked we can simply jump to the 
// handler in the caller.
// 
// If we are hijacked, then we jump to a routine that will unhijack appropriately and wait for the GC to
// complete. There are also variants for GC stress.
//
// Note that at this point we are either hijacked or we are not, and this will not change until we return to
// managed code. It is an invariant of the system that a thread will only attempt to hijack or unhijack 
// another thread while the target thread is suspended in managed code, and this is _not_ managed code.
//
    .macro
        RTU_EH_JUMP_HELPER funcName, hijackFuncName, isStress, stressFuncName

        LEAF_ENTRY \funcName
            PREPARE_EXTERNAL_VAR \hijackFuncName,x0
            cmp         x0, lr
            beq         RhpGCProbeForEHJump

            .if \isStress
            PREPARE_EXTERNAL_VAR \stressFuncName,x0
            cmp         x0, lr
            beq         RhpGCStressProbeForEHJump
            .endif

            // We are not hijacked, so we can return to the handler.
            // We return to keep the call/return prediction balanced.
            mov         lr, x2  ; Update the return address
            ret
        LEAF_END \funcName
    .endm
// We need an instance of the helper for each possible hijack function. The binder has enough
// information to determine which one we need to use for any function.
    RTU_EH_JUMP_HELPER RhpEHJumpScalar,         RhpGcProbeHijack, 0, 0
    RTU_EH_JUMP_HELPER RhpEHJumpObject,         RhpGcProbeHijack, 0, 0
    RTU_EH_JUMP_HELPER RhpEHJumpByref,          RhpGcProbeHijack,  0, 0
#ifdef FEATURE_GC_STRESS
    RTU_EH_JUMP_HELPER RhpEHJumpScalarGCStress, RhpGcProbeHijack, 1,  RhpGcStressHijack
    RTU_EH_JUMP_HELPER RhpEHJumpObjectGCStress, RhpGcProbeHijack, 1,  RhpGcStressHijack
    RTU_EH_JUMP_HELPER RhpEHJumpByrefGCStress,  RhpGcProbeHijack,  1,  RhpGcStressHijack
#endif

//
// Macro to setup our frame and adjust the location of the EH object reference for EH jump probe funcs.
//
// Register state on entry:
//  x0: scratch
//  x1: reference to the exception object.
//  x2: handler address we want to jump to.
//  Non-volatile registers are all already correct for return to the caller.
//  The stack is as if we are just about to returned from the call
//  
// Register state on exit:
//  x0: reference to the exception object
//  x2: thread pointer
//
    .macro
        EHJumpProbeProlog

        PROLOG_NOP mov x0, x1  ; move the ex object reference into x0 so we can report it
        ALLOC_PROBE_FRAME 0x10, 1
        str         x2, [sp, #PROBE_FRAME_SIZE]

        // x2 <- GetThread()

#if FEATURE_EMULATED_TLS
        bl RhpGetThread
        mov x2, x0
#else
        INLINE_GETTHREAD x2
#endif
        
        // Recover the original return address and update the frame
        ldr         lr, [x2, #OFFSETOF__Thread__m_pvHijackedReturnAddress]
        str         lr, [sp, #OFFSETOF__PInvokeTransitionFrame__m_RIP]

        // ClearHijackState expects thread in x2
        ClearHijackState

        // TRASHES x1
        INIT_PROBE_FRAME x2, x1, #(DEFAULT_FRAME_SAVE_FLAGS + PTFF_SAVE_X0), PTFF_X0_IS_GCREF_HI, (PROBE_FRAME_SIZE + 8)
        add         x1, sp, xzr
        str         x1, [x2, #OFFSETOF__Thread__m_pHackPInvokeTunnel]
    .endm

//
// Macro to re-adjust the location of the EH object reference, cleanup the frame, and make the 
// final jump to the handler for EH jump probe funcs.
//
// Register state on entry:
//  x0: reference to the exception object
//  x1-x3: scratch
//  
// Register state on exit:
//  sp: correct for return to the caller
//  x1: reference to the exception object
//
    .macro
        EHJumpProbeEpilog

        ldr         x2, [sp, #PROBE_FRAME_SIZE]
        FREE_PROBE_FRAME 0x10, 1       // This restores exception object back into x0
        EPILOG_NOP  mov x1, x0      // Move the Exception object back into x1 where the catch handler expects it
        EPILOG_NOP  br  x2
    .endm

//
// We are hijacked for a normal GC (not GC stress), so we need to unhijack and wait for the GC to complete.
//
// Register state on entry:
//  x0: reference to the exception object.
//  x2: thread
//  Non-volatile registers are all already correct for return to the caller.
//  The stack is as if we have tail called to this function (lr points to return address).
//        
// Register state on exit:
//  x0: reference to the exception object
//
    NESTED_ENTRY RhpGCProbeForEHJump, _TEXT, NoHandler
        brk 0xf000 ;; TODO: remove after debugging/testing stub
        EHJumpProbeProlog

#ifdef _DEBUG
        //
        // If we get here, then we have been hijacked for a real GC, and our SyncState must
        // reflect that we've been requested to synchronize.

        PREPARE_EXTERNAL_VAR RhpTrapThreads, X1
        ldr         w1, [x1]
        tbnz        x1, #TrapThreadsFlags_TrapThreads_Bit, 0f

        bl          RhDebugBreak
0:
#endif // _DEBUG

        mov         x4, x2
        WaitForGCCompletion

        EHJumpProbeEpilog
    NESTED_END RhpGCProbeForEHJump

#ifdef FEATURE_GC_STRESS
//
// We are hijacked for GC Stress (not a normal GC) so we need to invoke the GC stress helper.
//
// Register state on entry:
//  x1: reference to the exception object.
//  x2: thread
//  Non-volatile registers are all already correct for return to the caller.
//  The stack is as if we have tail called to this function (lr points to return address).
//        
// Register state on exit:
//  x0: reference to the exception object
//
    NESTED_ENTRY RhpGCStressProbeForEHJump, _TEXT, NoHandler
        brk 0xf000 // TODO: remove after debugging/testing stub
        EHJumpProbeProlog

        bl          $REDHAWKGCINTERFACE__STRESSGC

        EHJumpProbeEpilog
    NESTED_END RhpGCStressProbeForEHJump
#endif // FEATURE_GC_STRESS
#endif // 0

#ifdef FEATURE_GC_STRESS
//
// INVARIANT: Don't trash the argument registers, the binder codegen depends on this.
//
    LEAF_ENTRY RhpSuppressGcStress
        INLINE_GETTHREAD x9
        add         x9, x9, #OFFSETOF__Thread__m_ThreadStateFlags
Retry:
        ldxr        w10, [x9]
        orr         w10, w10, #TSF_SuppressGcStress
        stxr        w11, w10, [x9]
        cbz         w11, Success
        b           Retry

Success:
        ret
    LEAF_END RhpSuppressGcStress
#endif // FEATURE_GC_STRESS
