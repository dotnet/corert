;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

    TEXTAREA

    SETALIAS    GetLoopIndirCells, ?GetLoopIndirCells@ModuleHeader@@QEAAPEAEXZ
    SETALIAS    g_pTheRuntimeInstance, ?g_pTheRuntimeInstance@@3PEAVRuntimeInstance@@EA
    SETALIAS    RuntimeInstance__ShouldHijackLoopForGcStress, ?ShouldHijackLoopForGcStress@RuntimeInstance@@QEAA_N_K@Z

    EXTERN      g_fGcStressStarted

    EXTERN      $g_pTheRuntimeInstance
    EXTERN      $RuntimeInstance__ShouldHijackLoopForGcStress
    EXTERN      $GetLoopIndirCells
    EXTERN      RecoverLoopHijackTarget

PROBE_SAVE_FLAGS_EVERYTHING     equ DEFAULT_FRAME_SAVE_FLAGS + PTFF_SAVE_ALL_SCRATCH

    ;; Build a map of symbols representing offsets into the transition frame (see PInvokeTransitionFrame in
    ;; rhbinder.h) and keep these two in sync.
    map 0
            field OFFSETOF__PInvokeTransitionFrame__m_PreservedRegs
            field 10 * 8 ; x19..x28
m_CallersSP field 8      ; SP at routine entry
            field 19 * 8 ; x0..x18
            field 8      ; lr
m_SavedNZCV field 8      ; Saved condition flags
            field 4 * 8  ; d0..d3
PROBE_FRAME_SIZE    field 0

    ;; Support for setting up a transition frame when performing a GC probe. In many respects this is very
    ;; similar to the logic in PUSH_COOP_PINVOKE_FRAME in AsmMacros.h. In most cases setting up the
    ;; transition frame comprises the entirety of the caller's prolog (and initial non-prolog code) and
    ;; similarly for the epilog. Those cases can be dealt with using PROLOG_PROBE_FRAME and EPILOG_PROBE_FRAME
    ;; defined below. For the special cases where additional work has to be done in the prolog we also provide
    ;; the lower level macros ALLOC_PROBE_FRAME, FREE_PROBE_FRAME and INIT_PROBE_FRAME that allow more control
    ;; to be asserted.
    ;;
    ;; Note that we currently employ a significant simplification of frame setup: we always allocate a
    ;; maximally-sized PInvokeTransitionFrame and save all of the registers. Depending on the caller this can
    ;; lead to up to 20 additional register saves (x0-x18, lr) or 160 bytes of stack space. I have done no
    ;; analysis to see whether any of the worst cases occur on performance sensitive paths and whether the
    ;; additional saves will show any measurable degradation.

    ;; Perform the parts of setting up a probe frame that can occur during the prolog (and indeed this macro
    ;; can only be called from within the prolog).
    MACRO
        ALLOC_PROBE_FRAME $extraStackSpace, $saveFPRegisters

        ;; First create PInvokeTransitionFrame      
        PROLOG_SAVE_REG_PAIR   fp, lr, #-(PROBE_FRAME_SIZE + $extraStackSpace)!      ;; Push down stack pointer and store FP and LR

        ;; Slot at [sp, #0x10] is reserved for Thread *
        ;; Slot at [sp, #0x18] is reserved for bitmask of saved registers

        ;; Save callee saved registers
        PROLOG_SAVE_REG_PAIR   x19, x20, #0x20
        PROLOG_SAVE_REG_PAIR   x21, x22, #0x30
        PROLOG_SAVE_REG_PAIR   x23, x24, #0x40
        PROLOG_SAVE_REG_PAIR   x25, x26, #0x50
        PROLOG_SAVE_REG_PAIR   x27, x28, #0x60

        ;; Slot at [sp, #0x70] is reserved for caller sp

        ;; Save the scratch registers 
        PROLOG_NOP str         x0,       [sp, #0x78]
        PROLOG_NOP stp         x1, x2,   [sp, #0x80]
        PROLOG_NOP stp         x3, x4,   [sp, #0x90]
        PROLOG_NOP stp         x5, x6,   [sp, #0xA0]
        PROLOG_NOP stp         x7, x8,   [sp, #0xB0]
        PROLOG_NOP stp         x9, x10,  [sp, #0xC0]
        PROLOG_NOP stp         x11, x12, [sp, #0xD0]
        PROLOG_NOP stp         x13, x14, [sp, #0xE0]
        PROLOG_NOP stp         x15, x16, [sp, #0xF0]
        PROLOG_NOP stp         x17, x18, [sp, #0x100]
        PROLOG_NOP str         lr,       [sp, #0x110]

        ;; Slot at [sp, #0x118] is reserved for NZCV

        ;; Save the floating return registers
        IF $saveFPRegisters
            PROLOG_NOP stp         d0, d1,   [sp, #0x120]
            PROLOG_NOP stp         d2, d3,   [sp, #0x130]
        ENDIF

    MEND

    ;; Undo the effects of an ALLOC_PROBE_FRAME. This may only be called within an epilog. Note that all
    ;; registers are restored (apart for sp and pc), even volatiles.
    MACRO
        FREE_PROBE_FRAME $extraStackSpace, $restoreFPRegisters

        ;; Restore the scratch registers 
        PROLOG_NOP ldr          x0,       [sp, #0x78]
        PROLOG_NOP ldp          x1, x2,   [sp, #0x80]
        PROLOG_NOP ldp          x3, x4,   [sp, #0x90]
        PROLOG_NOP ldp          x5, x6,   [sp, #0xA0]
        PROLOG_NOP ldp          x7, x8,   [sp, #0xB0]
        PROLOG_NOP ldp          x9, x10,  [sp, #0xC0]
        PROLOG_NOP ldp          x11, x12, [sp, #0xD0]
        PROLOG_NOP ldp          x13, x14, [sp, #0xE0]
        PROLOG_NOP ldp          x15, x16, [sp, #0xF0]
        PROLOG_NOP ldp          x17, x18, [sp, #0x100]
        PROLOG_NOP ldr          lr,       [sp, #0x110]

        ; Restore the floating return registers
        IF $restoreFPRegisters
            EPILOG_NOP ldp          d0, d1,   [sp, #0x120]
            EPILOG_NOP ldp          d2, d3,   [sp, #0x130]
        ENDIF

        ;; Restore callee saved registers
        EPILOG_RESTORE_REG_PAIR x19, x20, #0x20
        EPILOG_RESTORE_REG_PAIR x21, x22, #0x30
        EPILOG_RESTORE_REG_PAIR x23, x24, #0x40
        EPILOG_RESTORE_REG_PAIR x25, x26, #0x50
        EPILOG_RESTORE_REG_PAIR x27, x28, #0x60

        EPILOG_RESTORE_REG_PAIR fp, lr, #(PROBE_FRAME_SIZE + $extraStackSpace)!
    MEND

    ;; Complete the setup of a probe frame allocated with ALLOC_PROBE_FRAME with the initialization that can
    ;; occur only outside the prolog (includes linking the frame to the current Thread). This macro assumes SP
    ;; is invariant outside of the prolog.
    ;;
    ;;  $threadReg     : register containing the Thread* (this will be preserved)
    ;;  $trashReg      : register that can be trashed by this macro
    ;;  $savedRegsMask : value to initialize m_Flags field with (register or #constant)
    ;;  $gcFlags       : value of gcref / gcbyref flags for saved registers, used only if $savedRegsMask is constant
    ;;  $frameSize     : total size of the method's stack frame (including probe frame size)
    MACRO
        INIT_PROBE_FRAME $threadReg, $trashReg, $savedRegsMask, $gcFlags, $frameSize

        LCLS BitmaskStr
BitmaskStr SETS "$savedRegsMask"        

        str         $threadReg, [sp, #OFFSETOF__PInvokeTransitionFrame__m_pThread]            ; Thread *
        IF          BitmaskStr:LEFT:1 == "#"
            ;; The savedRegsMask is a constant, remove the leading "#" since the MOVL64 doesn't expect it
BitmaskStr  SETS BitmaskStr:RIGHT:(:LEN:BitmaskStr - 1)
            MOVL64      $trashReg, $BitmaskStr, $gcFlags
        ELSE
            ASSERT "$gcFlags" == ""
            ;; The savedRegsMask is a register
            mov         $trashReg, $savedRegsMask
        ENDIF
        str         $trashReg, [sp, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        add         $trashReg, sp, #$frameSize
        str         $trashReg, [sp, #m_CallersSP]
    MEND    

    ;; Simple macro to use when setting up the probe frame can comprise the entire prolog. Call this macro
    ;; first in the method (no further prolog instructions can be added after this).
    ;;
    ;;  $threadReg     : register containing the Thread* (this will be preserved). If defaulted (specify |) then
    ;;                   the current thread will be calculated inline into r2 ($trashReg must not equal r2 in
    ;;                   this case)
    ;;  $trashReg      : register that can be trashed by this macro
    ;;  $savedRegsMask : value to initialize m_dwFlags field with (register or #constant)
    ;;  $gcFlags       : value of gcref / gcbyref flags for saved registers, used only if $savedRegsMask is constant
    MACRO
        PROLOG_PROBE_FRAME $threadReg, $trashReg, $savedRegsMask, $gcFlags

        ; Local string tracking the name of the register in which the Thread* is kept. Defaults to the value
        ; of $threadReg.
        LCLS __PPF_ThreadReg
__PPF_ThreadReg SETS "$threadReg"

        ; Define the method prolog, allocating enough stack space for the PInvokeTransitionFrame and saving
        ; incoming register values into it.
        ALLOC_PROBE_FRAME 0, {true}

        ; If the caller didn't provide a value for $threadReg then generate code to fetch the Thread* into x2.
        ; Record that x2 holds the Thread* in our local variable.
        IF "$threadReg" == ""
            ASSERT "$trashReg" != "x2"
__PPF_ThreadReg SETS "x2"
            INLINE_GETTHREAD $__PPF_ThreadReg, $trashReg
        ENDIF

        ; Perform the rest of the PInvokeTransitionFrame initialization.
        INIT_PROBE_FRAME $__PPF_ThreadReg, $trashReg, $savedRegsMask, $gcFlags, PROBE_FRAME_SIZE
        mov         $trashReg, sp
        str         $trashReg, [$__PPF_ThreadReg, #OFFSETOF__Thread__m_pHackPInvokeTunnel]
    MEND

    ; Simple macro to use when PROLOG_PROBE_FRAME was used to set up and initialize the prolog and
    ; PInvokeTransitionFrame. This will define the epilog including a return via the restored LR.
    MACRO
        EPILOG_PROBE_FRAME

        FREE_PROBE_FRAME 0, {true}
        EPILOG_RETURN
    MEND

;; In order to avoid trashing VFP registers across the loop hijack we must save all user registers, so that 
;; registers used by the loop being hijacked will not be affected. Unlike ARM32 where neon registers (NQ0, ..., NQ15) 
;; are fully covered by the floating point registers D0 ... D31, we have 32 neon registers Q0, ... Q31 on ARM64 
;; which are not fully covered by the register D0 ... D31. Therefore we must explicitly save all Q registers.
EXTRA_SAVE_SIZE equ (32*16)

    MACRO
        ALLOC_LOOP_HIJACK_FRAME

        PROLOG_STACK_ALLOC EXTRA_SAVE_SIZE

        ;; Save all neon registers
        PROLOG_NOP stp         q0, q1,   [sp]
        PROLOG_NOP stp         q2, q3,   [sp, #0x20]
        PROLOG_NOP stp         q4, q5,   [sp, #0x40]
        PROLOG_NOP stp         q6, q7,   [sp, #0x60]
        PROLOG_NOP stp         q8, q9,   [sp, #0x80]
        PROLOG_NOP stp         q10, q11, [sp, #0xA0]
        PROLOG_NOP stp         q12, q13, [sp, #0xC0]
        PROLOG_NOP stp         q14, q15, [sp, #0xE0]
        PROLOG_NOP stp         q16, q17, [sp, #0x100]
        PROLOG_NOP stp         q18, q19, [sp, #0x120]
        PROLOG_NOP stp         q20, q21, [sp, #0x140]
        PROLOG_NOP stp         q22, q23, [sp, #0x160]
        PROLOG_NOP stp         q24, q25, [sp, #0x180]
        PROLOG_NOP stp         q26, q27, [sp, #0x1A0]
        PROLOG_NOP stp         q28, q29, [sp, #0x1C0]
        PROLOG_NOP stp         q30, q31, [sp, #0x1E0]
        
        ALLOC_PROBE_FRAME 0, {false}
    MEND

    MACRO
        FREE_LOOP_HIJACK_FRAME

        FREE_PROBE_FRAME 0, {false}

        ;; restore all neon registers 
        PROLOG_NOP ldp         q0, q1,   [sp]
        PROLOG_NOP ldp         q2, q3,   [sp, #0x20]
        PROLOG_NOP ldp         q4, q5,   [sp, #0x40]
        PROLOG_NOP ldp         q6, q7,   [sp, #0x60]
        PROLOG_NOP ldp         q8, q9,   [sp, #0x80]
        PROLOG_NOP ldp         q10, q11, [sp, #0xA0]
        PROLOG_NOP ldp         q12, q13, [sp, #0xC0]
        PROLOG_NOP ldp         q14, q15, [sp, #0xE0]
        PROLOG_NOP ldp         q16, q17, [sp, #0x100]
        PROLOG_NOP ldp         q18, q19, [sp, #0x120]
        PROLOG_NOP ldp         q20, q21, [sp, #0x140]
        PROLOG_NOP ldp         q22, q23, [sp, #0x160]
        PROLOG_NOP ldp         q24, q25, [sp, #0x180]
        PROLOG_NOP ldp         q26, q27, [sp, #0x1A0]
        PROLOG_NOP ldp         q28, q29, [sp, #0x1C0]
        PROLOG_NOP ldp         q30, q31, [sp, #0x1E0]

        EPILOG_STACK_FREE EXTRA_SAVE_SIZE
    MEND

;;
;; Macro to clear the hijack state. This is safe to do because the suspension code will not Unhijack this 
;; thread if it finds it at an IP that isn't managed code.
;;
;; Register state on entry:
;;  x2: thread pointer
;;  
;; Register state on exit:
;;
    MACRO
        ClearHijackState

        ASSERT OFFSETOF__Thread__m_pvHijackedReturnAddress == (OFFSETOF__Thread__m_ppvHijackedReturnAddressLocation + 8)
        ;; Clear m_ppvHijackedReturnAddressLocation and m_pvHijackedReturnAddress
        stp         xzr, xzr, [x2, #OFFSETOF__Thread__m_ppvHijackedReturnAddressLocation]
        ;; Clear m_uHijackedReturnValueFlags
        str         xzr, [x2, #OFFSETOF__Thread__m_uHijackedReturnValueFlags]
    MEND

;;
;; The prolog for all GC suspension hijacks (normal and stress). Fixes up the hijacked return address, and 
;; clears the hijack state.
;;
;; Register state on entry:
;;  All registers correct for return to the original return address.
;;  
;; Register state on exit:
;;  x2: thread pointer
;;  x3: trashed
;;  x12: transition frame flags for the return registers x0 and x1
;;
    MACRO
        FixupHijackedCallstack

        ;; x2 <- GetThread(), TRASHES x3
        INLINE_GETTHREAD x2, x3
        
        ;;
        ;; Fix the stack by restoring the original return address
        ;;
        ASSERT OFFSETOF__Thread__m_uHijackedReturnValueFlags == (OFFSETOF__Thread__m_pvHijackedReturnAddress + 8)
        ;; Load m_pvHijackedReturnAddress and m_uHijackedReturnValueFlags
        ldp         lr, x12, [x2, #OFFSETOF__Thread__m_pvHijackedReturnAddress]

        ClearHijackState
    MEND

;;
;; Set the Thread state and wait for a GC to complete.
;;
;; Register state on entry:
;;  x4: thread pointer
;;  
;; Register state on exit:
;;  x4: thread pointer
;;  All other registers trashed
;;

    EXTERN RhpWaitForGCNoAbort

    MACRO
        WaitForGCCompletion

        ldr         w2, [x4, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         w2, #TSF_SuppressGcStress__OR__TSF_DoNotTriggerGC
        bne         %ft0

        ldr         x9, [x4, #OFFSETOF__Thread__m_pHackPInvokeTunnel]
        bl          RhpWaitForGCNoAbort
0
    MEND

    MACRO
        HijackTargetFakeProlog

        ;; This is a fake entrypoint for the method that 'tricks' the OS into calling our personality routine.
        ;; The code here should never be executed, and the unwind info is bogus, but we don't mind since the
        ;; stack is broken by the hijack anyway until after we fix it below.
        PROLOG_SAVE_REG_PAIR   fp, lr, #-0x10!
        nop                     ; We also need a nop here to simulate the implied bl instruction.  Without 
                                ; this, an OS-applied -4 will back up into the method prolog and the unwind 
                                ; will not be applied as desired.

    MEND

;;
;;
;;
;; GC Probe Hijack targets
;;
;;
    EXTERN RhpPInvokeExceptionGuard

    NESTED_ENTRY RhpGcProbeHijackWrapper, .text, RhpPInvokeExceptionGuard
        HijackTargetFakeProlog

    LABELED_RETURN_ADDRESS RhpGcProbeHijack

        FixupHijackedCallstack
        orr         x12, x12, #DEFAULT_FRAME_SAVE_FLAGS
        b           RhpGcProbe
    NESTED_END RhpGcProbeHijackWrapper

#ifdef FEATURE_GC_STRESS
;;
;;
;; GC Stress Hijack targets
;;
;;
    LEAF_ENTRY RhpGcStressHijack
        FixupHijackedCallstack
        orr         x12, x12, #DEFAULT_FRAME_SAVE_FLAGS
        b           RhpGcStressProbe
    LEAF_END RhpGcStressHijack
;;
;; Worker for our GC stress probes.  Do not call directly!!  
;; Instead, go through RhpGcStressHijack{Scalar|Object|Byref}. 
;; This worker performs the GC Stress work and returns to the original return address.
;;
;; Register state on entry:
;;  x0: hijacked function return value
;;  x1: hijacked function return value
;;  x2: thread pointer
;;  w12: register bitmask
;;
;; Register state on exit:
;;  Scratch registers, except for x0, have been trashed
;;  All other registers restored as they were when the hijack was first reached.
;;
    NESTED_ENTRY RhpGcStressProbe
        PROLOG_PROBE_FRAME x2, x3, x12, 

        bl          $REDHAWKGCINTERFACE__STRESSGC

        EPILOG_PROBE_FRAME
    NESTED_END RhpGcStressProbe
#endif ;; FEATURE_GC_STRESS

    LEAF_ENTRY RhpGcProbe
        ldr         x3, =RhpTrapThreads
        ldr         w3, [x3]
        tbnz        x3, #TrapThreadsFlags_TrapThreads_Bit, RhpGcProbeRare
        ret
    LEAF_END RhpGcProbe

    EXTERN RhpThrowHwEx

    NESTED_ENTRY RhpGcProbeRare
        PROLOG_PROBE_FRAME x2, x3, x12, 

        mov         x4, x2
        WaitForGCCompletion

        ldr         x2, [sp, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        tbnz        x2, #PTFF_THREAD_ABORT_BIT, %F1

        EPILOG_PROBE_FRAME

1        
        FREE_PROBE_FRAME 0, {true}
        EPILOG_NOP mov w0, #STATUS_REDHAWK_THREAD_ABORT
        EPILOG_NOP mov x1, lr ;; return address as exception PC
        EPILOG_NOP b RhpThrowHwEx
    NESTED_END RhpGcProbeRare

    LEAF_ENTRY RhpGcPoll
        brk 0xf000 ;; TODO: remove after debugging/testing stub
        ; @todo: I'm assuming it's not OK to trash any register here. If that's not true we can optimize the
        ; push/pops out of this fast path.
        str         x0, [sp], #-0x10!
        ldr         x0, =RhpTrapThreads
        ldr         w0, [x0]
        tbnz        x0, #TrapThreadsFlags_TrapThreads_Bit, %F0
        ldr         x0, [sp], #0x10!
        ret
0
        ldr         x0, [sp], #0x10!
        b           RhpGcPollRare
    LEAF_END RhpGcPoll

    NESTED_ENTRY RhpGcPollRare
        brk 0xf000 ;; TODO: remove after debugging/testing stub
        PROLOG_PROBE_FRAME |, x3, #PROBE_SAVE_FLAGS_EVERYTHING, 0

        ; Unhijack this thread, if necessary.
        INLINE_THREAD_UNHIJACK x2, x0, x1       ;; trashes x0, x1

        mov         x4, x2
        WaitForGCCompletion

        EPILOG_PROBE_FRAME
    NESTED_END RhpGcPollRare

    LEAF_ENTRY RhpGcPollStress
        ;
        ; loop hijacking is used instead
        ;
        brk 0xf000

    LEAF_END RhpGcPollStress


#ifdef FEATURE_GC_STRESS
    NESTED_ENTRY RhpHijackForGcStress
        ;; This function should be called from right before epilog

        ;; Push FP and LR, and allocate stack to hold PAL_LIMITED_CONTEXT structure and VFP return value registers
        PROLOG_SAVE_REG_PAIR    fp, lr, #-(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)!

        ;;
        ;; Setup a PAL_LIMITED_CONTEXT that looks like what you'd get if you had suspended this thread at the
        ;; IP after the call to this helper.
        ;;
        ;; This is very likely overkill since the calculation of the return address should only need SP and 
        ;; LR, but this is test code, so I'm not too worried about efficiency.
        ;;
        ;; Setup a PAL_LIMITED_CONTEXT on the stack 
        ;; {
            ;; FP and LR already pushed.
            PROLOG_NOP  stp         x0, x1, [sp, #0x10]
            PROLOG_SAVE_REG_PAIR    x19, x20, #0x20
            PROLOG_SAVE_REG_PAIR    x21, x22, #0x30
            PROLOG_SAVE_REG_PAIR    x23, x24, #0x40
            PROLOG_SAVE_REG_PAIR    x25, x26, #0x50
            PROLOG_SAVE_REG_PAIR    x27, x28, #0x60
            PROLOG_SAVE_REG         lr, #0x78

        ;; } end PAL_LIMITED_CONTEXT

        ;; Save VFP return value
        stp         d0, d1, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x00)]
        stp         d2, d3, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x10)]

        ;; Compute and save SP at callsite.
        add         x0, sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)   ;; +0x20 for the pushes right before the context struct
        str         x0, [sp, #OFFSETOF__PAL_LIMITED_CONTEXT__SP]

        mov         x0, sp      ; Address of PAL_LIMITED_CONTEXT
        bl          $THREAD__HIJACKFORGCSTRESS

        ;; Restore return value registers (saved in PAL_LIMITED_CONTEXT structure)
        ldp         x0, x1, [sp, #0x10]

        ;; Restore VFP return value
        ldp         d0, d1, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x00)]
        ldp         d2, d3, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x10)]

        ;; Epilog
        EPILOG_RESTORE_REG_PAIR     x19, x20, #0x20
        EPILOG_RESTORE_REG_PAIR     x21, x22, #0x30
        EPILOG_RESTORE_REG_PAIR     x23, x24, #0x40
        EPILOG_RESTORE_REG_PAIR     x25, x26, #0x50
        EPILOG_RESTORE_REG_PAIR     x27, x28, #0x60
        EPILOG_RESTORE_REG_PAIR     fp, lr, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)!
        EPILOG_RETURN

    NESTED_END RhpHijackForGcStress

    NESTED_ENTRY RhpHijackForGcStressLeaf
        ;; This should be jumped to, right before epilog
        ;; x9 has the return address (we don't care about trashing scratch regs at this point)

        ;; Push FP and LR, and allocate stack to hold PAL_LIMITED_CONTEXT structure and VFP return value registers
        PROLOG_SAVE_REG_PAIR    fp, lr, #-(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)!

        ;;
        ;; Setup a PAL_LIMITED_CONTEXT that looks like what you'd get if you had suspended this thread at the
        ;; IP after the call to this helper.
        ;;
        ;; This is very likely overkill since the calculation of the return address should only need SP and 
        ;; LR, but this is test code, so I'm not too worried about efficiency.
        ;;
        ;; Setup a PAL_LIMITED_CONTEXT on the stack 
        ;; {
            ;; FP and LR already pushed.
            PROLOG_NOP  stp         x0, x1, [sp, #0x10]
            PROLOG_SAVE_REG_PAIR    x19, x20, #0x20
            PROLOG_SAVE_REG_PAIR    x21, x22, #0x30
            PROLOG_SAVE_REG_PAIR    x23, x24, #0x40
            PROLOG_SAVE_REG_PAIR    x25, x26, #0x50
            PROLOG_SAVE_REG_PAIR    x27, x28, #0x60
            ; PROLOG_SAVE_REG macro doesn't let to use scratch reg:
            PROLOG_NOP  str         x9, [sp, #0x78]           ; this is return address from RhpHijackForGcStress; lr is return address for it's caller

        ;; } end PAL_LIMITED_CONTEXT

        ;; Save VFP return value
        stp         d0, d1, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x00)]
        stp         d2, d3, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x10)]

        ;; Compute and save SP at callsite.
        add         x0, sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)   ;; +0x20 for the pushes right before the context struct
        str         x0, [sp, #OFFSETOF__PAL_LIMITED_CONTEXT__SP]

        mov         x0, sp      ; Address of PAL_LIMITED_CONTEXT
        bl          $THREAD__HIJACKFORGCSTRESS

        ;; Restore return value registers (saved in PAL_LIMITED_CONTEXT structure)
        ldp         x0, x1, [sp, #0x10]

        ;; Restore VFP return value
        ldp         d0, d1, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x00)]
        ldp         d2, d3, [sp, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x10)]

        ;; Epilog
        EPILOG_RESTORE_REG_PAIR     x19, x20, #0x20
        EPILOG_RESTORE_REG_PAIR     x21, x22, #0x30
        EPILOG_RESTORE_REG_PAIR     x23, x24, #0x40
        EPILOG_RESTORE_REG_PAIR     x25, x26, #0x50
        EPILOG_RESTORE_REG_PAIR     x27, x28, #0x60
        EPILOG_NOP     ldr          x9, [sp, #0x78]
        EPILOG_RESTORE_REG_PAIR     fp, lr, #(SIZEOF__PAL_LIMITED_CONTEXT + 0x20)!
        EPILOG_NOP     br           x9

    NESTED_END RhpHijackForGcStressLeaf

#endif ;; FEATURE_GC_STRESS

#if 0 // used by the binder only
;;
;; The following functions are _jumped_ to when we need to transfer control from one method to another for EH 
;; dispatch. These are needed to properly coordinate with the GC hijacking logic. We are essentially replacing
;; the return from the throwing method with a jump to the handler in the caller, but we need to be aware of 
;; any return address hijack that may be in place for GC suspension. These routines use a quick test of the 
;; return address against a specific GC hijack routine, and then fixup the stack pointer to what it would be 
;; after a real return from the throwing method. Then, if we are not hijacked we can simply jump to the 
;; handler in the caller.
;; 
;; If we are hijacked, then we jump to a routine that will unhijack appropriately and wait for the GC to
;; complete. There are also variants for GC stress.
;;
;; Note that at this point we are either hijacked or we are not, and this will not change until we return to
;; managed code. It is an invariant of the system that a thread will only attempt to hijack or unhijack 
;; another thread while the target thread is suspended in managed code, and this is _not_ managed code.
;;
    MACRO
        RTU_EH_JUMP_HELPER $funcName, $hijackFuncName, $isStress, $stressFuncName

        LEAF_ENTRY $funcName
            ldr         x0, =$hijackFuncName
            cmp         x0, lr
            beq         RhpGCProbeForEHJump

            IF $isStress
            ldr         x0, =$stressFuncName
            cmp         x0, lr
            beq         RhpGCStressProbeForEHJump
            ENDIF

            ;; We are not hijacked, so we can return to the handler.
            ;; We return to keep the call/return prediction balanced.
            mov         lr, x2  ; Update the return address
            ret
        LEAF_END $funcName
    MEND
;; We need an instance of the helper for each possible hijack function. The binder has enough
;; information to determine which one we need to use for any function.
    RTU_EH_JUMP_HELPER RhpEHJumpScalar,         RhpGcProbeHijack, {false}, 0
    RTU_EH_JUMP_HELPER RhpEHJumpObject,         RhpGcProbeHijack, {false}, 0
    RTU_EH_JUMP_HELPER RhpEHJumpByref,          RhpGcProbeHijack,  {false}, 0
#ifdef FEATURE_GC_STRESS
    RTU_EH_JUMP_HELPER RhpEHJumpScalarGCStress, RhpGcProbeHijack, {true},  RhpGcStressHijack
    RTU_EH_JUMP_HELPER RhpEHJumpObjectGCStress, RhpGcProbeHijack, {true},  RhpGcStressHijack
    RTU_EH_JUMP_HELPER RhpEHJumpByrefGCStress,  RhpGcProbeHijack,  {true},  RhpGcStressHijack
#endif

;;
;; Macro to setup our frame and adjust the location of the EH object reference for EH jump probe funcs.
;;
;; Register state on entry:
;;  x0: scratch
;;  x1: reference to the exception object.
;;  x2: handler address we want to jump to.
;;  Non-volatile registers are all already correct for return to the caller.
;;  The stack is as if we are just about to returned from the call
;;  
;; Register state on exit:
;;  x0: reference to the exception object
;;  x2: thread pointer
;;
    MACRO
        EHJumpProbeProlog

        PROLOG_NOP mov x0, x1  ; move the ex object reference into x0 so we can report it
        ALLOC_PROBE_FRAME 0x10, {true}
        str         x2, [sp, #PROBE_FRAME_SIZE]

        ;; x2 <- GetThread(), TRASHES x1
        INLINE_GETTHREAD x2, x1
        
        ;; Recover the original return address and update the frame
        ldr         lr, [x2, #OFFSETOF__Thread__m_pvHijackedReturnAddress]
        str         lr, [sp, #OFFSETOF__PInvokeTransitionFrame__m_RIP]

        ;; ClearHijackState expects thread in x2
        ClearHijackState

        ; TRASHES x1
        INIT_PROBE_FRAME x2, x1, #(DEFAULT_FRAME_SAVE_FLAGS + PTFF_SAVE_X0), PTFF_X0_IS_GCREF_HI, (PROBE_FRAME_SIZE + 8)
        add         x1, sp, xzr
        str         x1, [x2, #OFFSETOF__Thread__m_pHackPInvokeTunnel]
    MEND

;;
;; Macro to re-adjust the location of the EH object reference, cleanup the frame, and make the 
;; final jump to the handler for EH jump probe funcs.
;;
;; Register state on entry:
;;  x0: reference to the exception object
;;  x1-x3: scratch
;;  
;; Register state on exit:
;;  sp: correct for return to the caller
;;  x1: reference to the exception object
;;
    MACRO
        EHJumpProbeEpilog

        ldr         x2, [sp, #PROBE_FRAME_SIZE]
        FREE_PROBE_FRAME 0x10, {true}       ; This restores exception object back into x0
        EPILOG_NOP  mov x1, x0      ; Move the Exception object back into x1 where the catch handler expects it
        EPILOG_NOP  br  x2
    MEND

;;
;; We are hijacked for a normal GC (not GC stress), so we need to unhijack and wait for the GC to complete.
;;
;; Register state on entry:
;;  x0: reference to the exception object.
;;  x2: thread
;;  Non-volatile registers are all already correct for return to the caller.
;;  The stack is as if we have tail called to this function (lr points to return address).
;;        
;; Register state on exit:
;;  x0: reference to the exception object
;;
    NESTED_ENTRY RhpGCProbeForEHJump
        brk 0xf000 ;; TODO: remove after debugging/testing stub
        EHJumpProbeProlog

#ifdef _DEBUG
        ;;
        ;; If we get here, then we have been hijacked for a real GC, and our SyncState must
        ;; reflect that we've been requested to synchronize.

        ldr         x1, =RhpTrapThreads
        ldr         w1, [x1]
        tbnz        x1, #TrapThreadsFlags_TrapThreads_Bit, %0

        bl          RhDebugBreak
0
#endif ;; _DEBUG

        mov         x4, x2
        WaitForGCCompletion

        EHJumpProbeEpilog
    NESTED_END RhpGCProbeForEHJump

#ifdef FEATURE_GC_STRESS
;;
;; We are hijacked for GC Stress (not a normal GC) so we need to invoke the GC stress helper.
;;
;; Register state on entry:
;;  x1: reference to the exception object.
;;  x2: thread
;;  Non-volatile registers are all already correct for return to the caller.
;;  The stack is as if we have tail called to this function (lr points to return address).
;;        
;; Register state on exit:
;;  x0: reference to the exception object
;;
    NESTED_ENTRY RhpGCStressProbeForEHJump
        brk 0xf000 ;; TODO: remove after debugging/testing stub
        EHJumpProbeProlog

        bl          $REDHAWKGCINTERFACE__STRESSGC

        EHJumpProbeEpilog
    NESTED_END RhpGCStressProbeForEHJump
#endif ;; FEATURE_GC_STRESS
#endif ;; 0

#ifdef FEATURE_GC_STRESS
;;
;; INVARIANT: Don't trash the argument registers, the binder codegen depends on this.
;;
    LEAF_ENTRY RhpSuppressGcStress
        INLINE_GETTHREAD x9, x10
        add         x9, x9, #OFFSETOF__Thread__m_ThreadStateFlags
Retry
        ldxr        w10, [x9]
        orr         w10, w10, #TSF_SuppressGcStress
        stxr        w11, w10, [x9]
        cbz         w11, Success
        b           Retry

Success
        ret
    LEAF_END RhpSuppressGcStress
#endif ;; FEATURE_GC_STRESS

;; Helper called from hijacked loops
    LEAF_ENTRY RhpLoopHijack
;; we arrive here with essentially all registers containing useful content
;; TODO: update this comment after the RhpLoopHijack is implemented in the compiler

;; on the stack, we have two arguments:
;; - [sp+0] has the module header
;; - [sp+8] has the address of the indirection cell we jumped through
;;
;;
        brk 0xf000 ;; TODO: remove after debugging/testing stub
        ALLOC_LOOP_HIJACK_FRAME

        ; save condition codes
        mrs         x12, NZCV
        str         x12, [sp, #m_SavedNZCV]

        INLINE_GETTHREAD x4, x1
        INIT_PROBE_FRAME x4, x1, #PROBE_SAVE_FLAGS_EVERYTHING, 0, (PROBE_FRAME_SIZE + EXTRA_SAVE_SIZE + 8)
;;
;;      compute the index of the indirection cell
;;
        ldr         x0, [sp,#(PROBE_FRAME_SIZE + EXTRA_SAVE_SIZE + 0)]
        bl          $GetLoopIndirCells
        
        ; x0 now has address of the first loop indir cell
        ; subtract that from the address of our cell
        ; and divide by 8 to give the index of our cell
        ldr         x1, [sp,#(PROBE_FRAME_SIZE + EXTRA_SAVE_SIZE + 8)]
        sub         x1, x1, x0
        lsr         x0, x1, #3

        ; x0 now has the index
        ; recover the loop hijack target, passing the module header as an additional argument
        ldr         x1, [sp,#(PROBE_FRAME_SIZE + EXTRA_SAVE_SIZE + 0)]
        bl          RecoverLoopHijackTarget

        ; store the result as our pinvoke return address
        str         x0, [sp, #OFFSETOF__PInvokeTransitionFrame__m_RIP]

        ; also save it in the incoming parameter space for the actual return below
        str         x0, [sp,#(PROBE_FRAME_SIZE + EXTRA_SAVE_SIZE + 8)]

        ; Early out if GC stress is currently suppressed. Do this after we have computed the real address to
        ; return to but before we link the transition frame onto m_pHackPInvokeTunnel (because hitting this
        ; condition implies we're running restricted callouts during a GC itself and we could end up
        ; overwriting a co-op frame set by the code that caused the GC in the first place, e.g. a GC.Collect
        ; call).
        ldr         w1, [x4, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         w1, #TSF_SuppressGcStress__OR__TSF_DoNotTriggerGC
        bne         DoneWaitingForGc

        ; link the frame into the Thread
        add         x1, sp, xzr
        str         x1, [x4, #OFFSETOF__Thread__m_pHackPInvokeTunnel]

        ;;
        ;; Unhijack this thread, if necessary.
        ;;
        INLINE_THREAD_UNHIJACK x4, x1, x2       ;; trashes x1, x2

#ifdef FEATURE_GC_STRESS

        ldr         x1, =g_fGcStressStarted
        ldr         w1, [x1]
        cbnz        w1, NoGcStress

        mov         x1, x0
        ldr         x0, =$g_pTheRuntimeInstance
        ldr         x0, [x0]
        bl          $RuntimeInstance__ShouldHijackLoopForGcStress
        cbnz        x0, NoGcStress

        bl          $REDHAWKGCINTERFACE__STRESSGC
NoGcStress
#endif ;; FEATURE_GC_STRESS

        mov         x9, sp ; sp is address of PInvokeTransitionFrame
        bl          RhpWaitForGCNoAbort

DoneWaitingForGc
        ldr         x12, [sp, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        tbnz        x12, #PTFF_THREAD_ABORT_BIT, Abort
        ; restore condition codes
        ldr         x12, [sp, #m_SavedNZCV]
        msr         NZCV, x12

        FREE_LOOP_HIJACK_FRAME

        EPILOG_NOP ldr x1, [sp, #8]    ; hijack target address
        EPILOG_STACK_FREE 0x10 
        EPILOG_NOP br x1               ; jump to the hijack target

Abort
        FREE_LOOP_HIJACK_FRAME

        EPILOG_NOP mov w0, #STATUS_REDHAWK_THREAD_ABORT
        EPILOG_NOP ldr x1, [sp, #8]    ; hijack target address as exception PC
        EPILOG_STACK_FREE 0x10 
        EPILOG_NOP b RhpThrowHwEx
    LEAF_END RhpLoopHijack

    INLINE_GETTHREAD_CONSTANT_POOL

;; Trap to GC.
;; Set up the P/Invoke transition frame with the return address as the safe point.
;; All registers, both volatile and non-volatile, are preserved.
;; The function should be called not jumped because it's expecting the return address
    NESTED_ENTRY RhpTrapToGC, _TEXT
;;
        ;; What we want to get to: 
        ;;
        ;;   [sp +  ]   -> m_FramePointer                  -------|
        ;;   [sp + 8]   -> m_RIP                                  |
        ;;   [sp + 10]  -> m_pThread                              |
        ;;   [sp + 18]  -> m_Flags / m_dwAlignPad2                |
        ;;   [sp + 20]  -> x19 save                               |
        ;;   [sp + 28]  -> x20 save                               |
        ;;   [sp + 30]  -> x21 save                               |
        ;;   [sp + 38]  -> x22 save                               |
        ;;   [sp + 40]  -> x23 save                               |
        ;;   [sp + 48]  -> x24 save                               | PInvokeTransitionFrame
        ;;   [sp + 50]  -> x25 save                               |
        ;;   [sp + 58]  -> x26 save                               |
        ;;   [sp + 60]  -> x27 save                               |
        ;;   [sp + 68]  -> x28 save                               |
        ;;   [sp + 70]  -> sp save  ;caller sp                    |
        ;;   [sp + 78]  -> x0 save                                |
        ;;   [sp + 80]  -> x1 save                                |
        ;;   [sp + 88]  -> x2 save                                |
        ;;   [sp + 90]  -> x3 save                                |
        ;;   [sp + 98]  -> x4 save                                |
        ;;   [sp + a0]  -> x5 save                                |
        ;;   [sp + a8]  -> x6 save                                |
        ;;   [sp + b0]  -> x7 save                                |
        ;;   [sp + b8]  -> x8 save                                |
        ;;   [sp + c0]  -> x9 save                                |
        ;;   [sp + c8]  -> x10 save                               |
        ;;   [sp + d0]  -> x11 save                               |
        ;;   [sp + d8]  -> x12 save                               |
        ;;   [sp + e0]  -> x13 save                               |
        ;;   [sp + e8]  -> x14 save                               |
        ;;   [sp + f0]  -> x15 save                               |
        ;;   [sp + f8]  -> x16 save                               |
        ;;   [sp + 100]  -> x17 save                              |
        ;;   [sp + 108]  -> x18 save                              |
        ;;   [sp + 110]  -> lr save                        -------|
        ;;
        ;;   [sp + 118]  -> NZCV
        ;;
        ;;   [sp + 120]  -> not used 
        ;;   [sp + 140]  -> q0 ... q31
        ;;

        ALLOC_LOOP_HIJACK_FRAME

        ;; Slot at [sp, #0x118] is reserved for NZCV
        mrs         x1, NZCV
        str         x1, [sp, #m_SavedNZCV]

        ;; x4 <- GetThread(), TRASHES x1
        INLINE_GETTHREAD x4, x1
        INIT_PROBE_FRAME x4, x1, #PROBE_SAVE_FLAGS_EVERYTHING, 0, (PROBE_FRAME_SIZE + EXTRA_SAVE_SIZE)
        
        ; Early out if GC stress is currently suppressed. Do this after we have computed the real address to
        ; return to but before we link the transition frame onto m_pHackPInvokeTunnel (because hitting this
        ; condition implies we're running restricted callouts during a GC itself and we could end up
        ; overwriting a co-op frame set by the code that caused the GC in the first place, e.g. a GC.Collect
        ; call).
        ldr         w1, [x4, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         w1, #TSF_SuppressGcStress__OR__TSF_DoNotTriggerGC
        bne         DoNotTriggerGC

        ; link the frame into the Thread
        add         x1, sp, xzr
        str         x1, [x4, #OFFSETOF__Thread__m_pHackPInvokeTunnel]

        ;;
        ;; Unhijack this thread, if necessary.
        ;;
        INLINE_THREAD_UNHIJACK x4, x1, x2       ;; trashes x1, x2

#ifdef FEATURE_GC_STRESS

        ldr         x1, =g_fGcStressStarted
        ldr         w1, [x1]
        cbnz        w1, SkipGcStress

        mov         x1, x0
        ldr         x0, =$g_pTheRuntimeInstance
        ldr         x0, [x0]
        bl          $RuntimeInstance__ShouldHijackLoopForGcStress
        cbnz        x0, SkipGcStress

        bl          $REDHAWKGCINTERFACE__STRESSGC
SkipGcStress
#endif ;; FEATURE_GC_STRESS

        mov         x9, sp ; sp is address of PInvokeTransitionFrame
        bl          RhpWaitForGCNoAbort

DoNotTriggerGC
        ldr         x1, [sp, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        tbnz        x1, #PTFF_THREAD_ABORT_BIT, ToAbort
        
        ; restore condition codes
        ldr         x1, [sp, #m_SavedNZCV]
        msr         NZCV, x1

        FREE_LOOP_HIJACK_FRAME
        EPILOG_RETURN

ToAbort
        FREE_LOOP_HIJACK_FRAME
        EPILOG_NOP mov w0, #STATUS_REDHAWK_THREAD_ABORT
        EPILOG_NOP mov x1, lr    ; hijack target address as exception PC
        EPILOG_NOP b RhpThrowHwEx

    NESTED_END RhpTrapToGC

    INLINE_GETTHREAD_CONSTANT_POOL

    end

