// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <unixasmmacros.inc>
#include "AsmOffsets.inc"

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

    LEAF_ENTRY RhpGcPoll
        PREPARE_EXTERNAL_VAR_INDIRECT_W RhpTrapThreads, 0
        tbnz        x0, #TrapThreadsFlags_TrapThreads_Bit, RhpGcPollRare
        ret
    LEAF_END RhpGcPoll

    NESTED_ENTRY RhpGcPollRare, _TEXT, NoHandler
        PROLOG_PROBE_FRAME x2, x3, PROBE_SAVE_FLAGS_EVERYTHING, 0, 1, 1

        // Unhijack this thread, if necessary.
        INLINE_THREAD_UNHIJACK x2, x0, x1       // trashes x0, x1

        mov         x4, x2
        WaitForGCCompletion

        EPILOG_PROBE_FRAME
    NESTED_END RhpGcPollRare
