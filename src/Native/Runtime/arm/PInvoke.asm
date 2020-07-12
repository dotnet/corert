;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

#include "AsmMacros.h"

        TEXTAREA

        IMPORT RhpReversePInvokeBadTransition

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpWaitForSuspend -- rare path for RhpPInvoke and RhpReversePInvokeReturn
;;
;;
;; INPUT: none
;;
;; TRASHES: none
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        NESTED_ENTRY RhpWaitForSuspend

        PROLOG_PUSH {r0-r4,lr}     ; Need to save argument registers r0-r3 and lr, r4 is just for alignment
        PROLOG_VPUSH {d0-d7}       ; Save float argument registers as well since they're volatile

        bl          RhpWaitForSuspend2
        
        EPILOG_VPOP {d0-d7}
        EPILOG_POP  {r0-r4,pc}

        NESTED_END RhpWaitForSuspend


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpWaitForGCNoAbort
;;
;;
;; INPUT: r2: transition frame
;;
;; OUTPUT: 
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        NESTED_ENTRY RhpWaitForGCNoAbort

        PROLOG_PUSH {r0-r6,lr}  ; Even number of registers to maintain 8-byte stack alignment
        PROLOG_VPUSH {d0-d3}    ; Save float return value registers as well

        ldr         r5, [r2, #OFFSETOF__PInvokeTransitionFrame__m_pThread]

        ldr         r0, [r5, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         r0, #TSF_DoNotTriggerGc
        bne         Done

        mov         r0, r2      ; passing transition frame in r0
        bl          RhpWaitForGC2

Done
        EPILOG_VPOP {d0-d3}
        EPILOG_POP  {r0-r6,pc}

        NESTED_END RhpWaitForGCNoAbort

        EXTERN RhpThrowHwEx

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpWaitForGC
;;
;;
;; INPUT: r2: transition frame
;;
;; OUTPUT: 
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        NESTED_ENTRY RhpWaitForGC
        PROLOG_PUSH  {r0,lr}

        ldr         r0, =RhpTrapThreads
        ldr         r0, [r0]
        tst         r0, #TrapThreadsFlags_TrapThreads
        beq         NoWait
        bl          RhpWaitForGCNoAbort
NoWait
        tst         r0, #TrapThreadsFlags_AbortInProgress
        beq         NoAbort
        ldr         r0, [r2, #OFFSETOF__PInvokeTransitionFrame__m_Flags]
        tst         r0, #PTFF_THREAD_ABORT
        beq         NoAbort
        EPILOG_POP  {r0,r1}         ; hijack target address as exception PC
        EPILOG_NOP  mov r0, #STATUS_REDHAWK_THREAD_ABORT
        EPILOG_BRANCH RhpThrowHwEx        
NoAbort
        EPILOG_POP  {r0,pc}
        NESTED_END RhpWaitForGC

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvoke
;;
;; IN:  r4: address of reverse pinvoke frame
;;                  0: save slot for previous M->U transition frame
;;                  4: save slot for thread pointer to avoid re-calc in epilog sequence
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        NESTED_ENTRY RhpReversePInvoke

        PROLOG_PUSH {r5-r7,lr}  ; Even number of registers to maintain 8-byte stack alignment

        INLINE_GETTHREAD r5, r6     ; r5 = Thread, r6 trashed
        str         r5, [r4, #4]    ; save Thread pointer for RhpReversePInvokeReturn

        ; r4 = prev save slot
        ; r5 = thread
        ; r6 = scratch

        ldr         r6, [r5, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         r6, #TSF_Attached
        beq         AttachThread

ThreadAttached
        ;;
        ;; Check for the correct mode.  This is accessible via various odd things that we cannot completely 
        ;; prevent such as :
        ;;     1) Registering a reverse pinvoke entrypoint as a vectored exception handler
        ;;     2) Performing a managed delegate invoke on a reverse pinvoke delegate.
        ;;
        ldr         r6, [r5, #OFFSETOF__Thread__m_pTransitionFrame]
        cbz         r6, CheckBadTransition

        ;; Save previous TransitionFrame prior to making the mode transition so that it is always valid 
        ;; whenever we might attempt to hijack this thread.
        str         r6, [r4]

        mov         r6, #0
        str         r6, [r5, #OFFSETOF__Thread__m_pTransitionFrame]
        dmb

        ldr         r6, =RhpTrapThreads
        ldr         r6, [r6]
        tst         r6, #TrapThreadsFlags_TrapThreads
        bne         TrapThread

AllDone
        EPILOG_POP  {r5-r7,lr}
        EPILOG_RETURN


CheckBadTransition
        ;; Allow 'bad transitions' in when the TSF_DoNotTriggerGc mode is set.  This allows us to have 
        ;; [UnmanagedCallersOnly] methods that are called via the "restricted GC callouts" as well as from native,
        ;; which is necessary because the methods are CCW vtable methods on interfaces passed to native.
        ldr         r7, [r5, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         r7, #TSF_DoNotTriggerGc
        beq         BadTransition

        ;; zero-out our 'previous transition frame' save slot
        mov         r7, #0
        str         r7, [r4]

        ;; nothing more to do
        b           AllDone

TrapThread
        ;; put the previous frame back (sets us back to preemptive mode)
        ldr         r6, [r4]
        str         r6, [r5, #OFFSETOF__Thread__m_pTransitionFrame]
        dmb

AttachThread
        ; passing address of reverse pinvoke frame in r4
        EPILOG_POP  {r5-r7,lr}
        EPILOG_BRANCH RhpReversePInvokeAttachOrTrapThread

BadTransition
        EPILOG_POP  {r5-r7,lr}
        EPILOG_NOP  mov r0, lr  ; arg <- return address
        EPILOG_BRANCH RhpReversePInvokeBadTransition

        NESTED_END RhpReversePInvoke

        INLINE_GETTHREAD_CONSTANT_POOL


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvokeAttachOrTrapThread -- rare path for RhpPInvoke
;;
;;
;; INPUT: r4: address of reverse pinvoke frame
;;
;; TRASHES: none
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        NESTED_ENTRY RhpReversePInvokeAttachOrTrapThread

        PROLOG_PUSH {r0-r4,lr}     ; Need to save argument registers r0-r3 and lr, r4 is just for alignment
        PROLOG_VPUSH {d0-d7}       ; Save float argument registers as well since they're volatile

        mov         r0, r4         ; passing reverse pinvoke frame pointer in r0
        bl          RhpReversePInvokeAttachOrTrapThread2

        EPILOG_VPOP {d0-d7}
        EPILOG_POP  {r0-r4,pc}

        NESTED_END RhpReversePInvokeTrapThread


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvokeReturn
;;
;; IN:  r3: address of reverse pinvoke frame
;;                  0: save slot for previous M->U transition frame
;;                  4: save slot for thread pointer to avoid re-calc in epilog sequence
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        LEAF_ENTRY RhpReversePInvokeReturn

        ldr         r2, [r3, #4]    ; get Thread pointer
        ldr         r3, [r3, #0]    ; get previous M->U transition frame

        str         r3, [r2, #OFFSETOF__Thread__m_pTransitionFrame]
        dmb

        ldr         r3, =RhpTrapThreads
        ldr         r3, [r3]
        tst         r3, #TrapThreadsFlags_TrapThreads
        bne         RareTrapThread

        bx          lr

RareTrapThread
        b           RhpWaitForSuspend

        LEAF_END RhpReversePInvokeReturn


        end
