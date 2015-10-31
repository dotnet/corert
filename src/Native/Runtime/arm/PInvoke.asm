;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

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

        ; passing Thread pointer in r0, trashes r1
        INLINE_GETTHREAD r0, r1
        bl          RhpPInvokeWaitEx
        
        EPILOG_VPOP {d0-d7}
        EPILOG_POP  {r0-r4,pc}

        NESTED_END RhpWaitForSuspend


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

        PROLOG_PUSH {r0,r1,r4-r6,lr}  ; Even number of registers to maintain 8-byte stack alignment
        PROLOG_VPUSH {d0-d3}          ; Save float return value registers as well

        mov         r4, r2
        ldr         r5, [r4, #OFFSETOF__PInvokeTransitionFrame__m_pThread]

        ldr         r0, [r5, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         r0, #TSF_DoNotTriggerGc
        bne         Done

RetryWaitForGC
        ; r4: transition frame
        ; r5: thread

        str         r4, [r5, #OFFSETOF__Thread__m_pTransitionFrame]
        dmb

        mov         r0, r5                      ; passing Thread in r0
        bl          RhpPInvokeReturnWaitEx

        mov         r0, #0
        str         r0, [r5, #OFFSETOF__Thread__m_pTransitionFrame]
        dmb

        ldr         r0, =RhpTrapThreads
        ldr         r0, [r0]
        cmp         r0, #0
        bne         RetryWaitForGC

Done
        EPILOG_VPOP {d0-d3}
        EPILOG_POP  {r0,r1,r4-r6,pc}

        NESTED_END RhpWaitForGC

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvoke2
;;
;; INCOMING: r0 -- address of reverse pinvoke frame
;;
;; This is useful for calling with a standard calling convention for code generators that don't insert this in 
;; the prolog.
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        LEAF_ENTRY RhpReversePInvoke2

        mov         r4, r0
        b           RhpReversePInvoke

        LEAF_END RhpReversePInvoke2


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
        cbnz        r6, ValidTransition

        ;; Allow 'bad transitions' in when the TSF_DoNotTriggerGc mode is set.  This allows us to have 
        ;; [NativeCallable] methods that are called via the "restricted GC callouts" as well as from native,
        ;; which is necessary because the methods are CCW vtable methods on interfaces passed to native.
        ldr         r7, [r5, #OFFSETOF__Thread__m_ThreadStateFlags]
        tst         r7, #TSF_DoNotTriggerGc
        beq         BadTransition

        ;; zero-out our 'previous transition frame' save slot
        mov         r7, #0
        str         r7, [r4]

        ;; nothing more to do
        b           AllDone

ValidTransition

        ;; Save previous TransitionFrame prior to making the mode transition so that it is always valid 
        ;; whenever we might attempt to hijack this thread.
        str         r6, [r4]

ReverseRetry
        mov         r6, #0
        str         r6, [r5, #OFFSETOF__Thread__m_pTransitionFrame]
        dmb

        ldr         r6, =RhpTrapThreads
        ldr         r6, [r6]
        cbnz        r6, TrapThread

AllDone
        EPILOG_POP  {r5-r7,lr}
        EPILOG_RETURN

TrapThread
        ; r4 = prev save slot
        ; r5 = thread
        ; r6 = scratch
        
        ldr         r6, [r4]
        str         r6, [r5, #OFFSETOF__Thread__m_pTransitionFrame]
        dmb

        ; passing Thread pointer in r5
        bl          RhpReversePInvokeTrapThread
        b           ReverseRetry

AttachThread
        bl          RhpReversePInvokeAttachThread
        b           ThreadAttached

BadTransition

        EPILOG_POP  {r5-r7,lr}
        EPILOG_BRANCH RhpReversePInvokeBadTransition

        NESTED_END RhpReversePInvoke


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvokeTrapThread -- rare path for RhpPInvoke
;;
;;
;; INPUT: r5 = thread
;;
;; TRASHES: none
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        NESTED_ENTRY RhpReversePInvokeTrapThread

        PROLOG_PUSH {r0-r4,lr}     ; Need to save argument registers r0-r3 and lr, r4 is just for alignment
        PROLOG_VPUSH {d0-d7}       ; Save float argument registers as well since they're volatile

        mov         r0, r5         ; passing Thread pointer in r0
        bl          RhpPInvokeReturnWaitEx

        EPILOG_VPOP {d0-d7}
        EPILOG_POP  {r0-r4,pc}

        NESTED_END RhpReversePInvokeTrapThread


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; AttachCurrentThread -- rare path for RhpPInvoke
;;
;;
;; INPUT: none
;;
;; TRASHES: none
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        NESTED_ENTRY RhpReversePInvokeAttachThread

        ;;
        ;; Thread attach is done here to avoid taking the ThreadStore lock while in DllMain.  The lock is 
        ;; avoided for DllMain thread attach notifications, but not process attach notifications because
        ;; our managed DllMain does work during process attach, so it needs to reverse pinvoke.
        ;;

        PROLOG_PUSH {r0-r4,lr}     ; Need to save argument registers r0-r3 and lr, r4 is just for alignment
        PROLOG_VPUSH {d0-d7}       ; Save float argument registers as well since they're volatile

        bl          $THREADSTORE__ATTACHCURRENTTHREAD
        
        EPILOG_VPOP {d0-d7}
        EPILOG_POP  {r0-r4,pc}

        NESTED_END RhpReversePInvokeAttachThread

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
        cbnz        r3, RareTrapThread

        bx          lr

RareTrapThread
        b           RhpWaitForSuspend

        LEAF_END RhpReversePInvokeReturn


        end
