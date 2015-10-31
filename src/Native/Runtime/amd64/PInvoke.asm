;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

include asmmacros.inc

extern RhpReversePInvokeBadTransition : proc


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
NESTED_ENTRY RhpWaitForSuspend, _TEXT
        push_vol_reg    rax
        alloc_stack     60h

        ; save the arg regs in the caller's scratch space
        save_reg_postrsp        rcx, 70h
        save_reg_postrsp        rdx, 78h
        save_reg_postrsp        r8, 80h
        save_reg_postrsp        r9, 88h

        ; save the FP arg regs in our stack frame
        save_xmm128_postrsp     xmm0, (20h + 0*10h)
        save_xmm128_postrsp     xmm1, (20h + 1*10h)
        save_xmm128_postrsp     xmm2, (20h + 2*10h)
        save_xmm128_postrsp     xmm3, (20h + 3*10h)

        END_PROLOGUE

        ; passing Thread pointer in rcx
        INLINE_GETTHREAD rcx, rdx
        call        RhpPInvokeWaitEx

        movdqa      xmm0, [rsp + 20h + 0*10h]
        movdqa      xmm1, [rsp + 20h + 1*10h]
        movdqa      xmm2, [rsp + 20h + 2*10h]
        movdqa      xmm3, [rsp + 20h + 3*10h]

        mov         rcx, [rsp + 70h]
        mov         rdx, [rsp + 78h]
        mov         r8,  [rsp + 80h]
        mov         r9,  [rsp + 88h]

        add         rsp, 60h
        pop         rax
        ret

NESTED_END RhpWaitForSuspend, _TEXT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpWaitForGC -- rare path for RhpPInvokeReturn
;;
;;
;; INPUT: RCX: transition frame
;;
;; TRASHES: RCX, RDX, R8, R9, R10, R11
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
NESTED_ENTRY RhpWaitForGC, _TEXT
        push_vol_reg    rax                 ; don't trash the integer return value
        push_nonvol_reg rbx
        alloc_stack     38h
        movdqa          [rsp + 20h], xmm0   ; don't trash the FP return value
        END_PROLOGUE

        mov         rbx, rcx

RetryWaitForGC:
        ; RBX: transition frame
        mov         rcx, [rbx + OFFSETOF__PInvokeTransitionFrame__m_pThread]

        test        dword ptr [rcx + OFFSETOF__Thread__m_ThreadStateFlags], TSF_DoNotTriggerGc
        jnz         Done

        mov         [rcx + OFFSETOF__Thread__m_pTransitionFrame], rbx
        
        ; passing thread pointer in rcx
        call        RhpPInvokeReturnWaitEx
        
        mov         rcx, [rbx + OFFSETOF__PInvokeTransitionFrame__m_pThread]
        mov         qword ptr [rcx + OFFSETOF__Thread__m_pTransitionFrame], 0

        cmp         [RhpTrapThreads], 0
        jne         RetryWaitForGC

Done:
        movdqa      xmm0, [rsp + 20h]
        add         rsp, 38h
        pop         rbx
        pop         rax
        ret

NESTED_END RhpWaitForGC, _TEXT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvoke2
;;
;; INCOMING: RCX -- address of reverse pinvoke frame
;;
;; This is useful for calling with a standard calling convention for code generators that don't insert this in 
;; the prolog.
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
LEAF_ENTRY RhpReversePInvoke2, _TEXT
        mov         rax, rcx
        jmp         RhpReversePInvoke
LEAF_END RhpReversePInvoke2, _TEXT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvoke
;;
;;
;; INCOMING:  RAX -- address of reverse pinvoke frame
;;                          0: save slot for previous M->U transition frame
;;                          8: save slot for thread pointer to avoid re-calc in epilog sequence
;;
;; PRESERVES: RCX, RDX, R8, R9 -- need to preserve these because the caller assumes they aren't trashed
;;
;; TRASHES:   RAX, R10, R11
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
LEAF_ENTRY RhpReversePInvoke, _TEXT
        ;; R10 = GetThread(), TRASHES R11
        INLINE_GETTHREAD r10, r11
        mov         [rax + 8], r10          ; save thread pointer for RhpReversePInvokeReturn

        test        dword ptr [r10 + OFFSETOF__Thread__m_ThreadStateFlags], TSF_Attached
        jz          AttachThread

        ;;
        ;; Check for the correct mode.  This is accessible via various odd things that we cannot completely 
        ;; prevent such as :
        ;;     1) Registering a reverse pinvoke entrypoint as a vectored exception handler
        ;;     2) Performing a managed delegate invoke on a reverse pinvoke delegate.
        ;;
        cmp         qword ptr [r10 + OFFSETOF__Thread__m_pTransitionFrame], 0
        jne         ValidTransition

        ;; Allow 'bad transitions' in when the TSF_DoNotTriggerGc mode is set.  This allows us to have 
        ;; [NativeCallable] methods that are called via the "restricted GC callouts" as well as from native,
        ;; which is necessary because the methods are CCW vtable methods on interfaces passed to native.
        test        dword ptr [r10 + OFFSETOF__Thread__m_ThreadStateFlags], TSF_DoNotTriggerGc
        jz          BadTransition

ValidTransition:
        ; rax: reverse pinvoke frame
        ; r10: thread

        ; Save previous TransitionFrame prior to making the mode transition so that it is always valid 
        ; whenever we might attempt to hijack this thread.
        mov         r11, [r10 + OFFSETOF__Thread__m_pTransitionFrame]
        mov         [rax], r11

        mov         qword ptr [r10 + OFFSETOF__Thread__m_pTransitionFrame], 0
        cmp         [RhpTrapThreads], 0
        jne         TrapThread

        ret


AttachThread:
        mov         r11, 1
        jmp         RhpReversePInvoke_RarePath

BadTransition:
        mov         r11, 2
        jmp         RhpReversePInvoke_RarePath

TrapThread:
        mov         r11, 3
        jmp         RhpReversePInvoke_RarePath

LEAF_END RhpReversePInvoke, _TEXT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvoke_RarePath
;;
;;
;; INCOMING:  RAX -- address of reverse pinvoke frame
;;                          0: save slot for previous M->U transition frame
;;                          8: save slot for thread pointer to avoid re-calc in epilog sequence
;;            R10 -- thread pointer
;;            R11 -- reason for entering the rare path {1: attach thread, 2: bad transition, 3: trap thread}
;;
;; PRESERVES: RCX, RDX, R8, R9 -- need to preserve these because the caller assumes they aren't trashed
;;
;; TRASHES:   RAX, R10, R11
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
NESTED_ENTRY RhpReversePInvoke_RarePath, _TEXT
        push_nonvol_reg rbx
        alloc_stack     80h     ; alloc scratch area and frame

        ; save the integer arg regs
        save_reg_postrsp        rcx, (20h + 0*8)
        save_reg_postrsp        rdx, (20h + 1*8)
        save_reg_postrsp        r8,  (20h + 2*8)
        save_reg_postrsp        r9,  (20h + 3*8)

        ; save the FP arg regs
        save_xmm128_postrsp     xmm0, (20h + 4*8 + 0*10h)
        save_xmm128_postrsp     xmm1, (20h + 4*8 + 1*10h)
        save_xmm128_postrsp     xmm2, (20h + 4*8 + 2*10h)
        save_xmm128_postrsp     xmm3, (20h + 4*8 + 3*10h)

        END_PROLOGUE

        mov         rbx, rax    ; RBX <- address of previous frame save slot
        
        cmp         r11, 1
        je          AttachThread
        cmp         r11, 2
        je          BadTransition
        cmp         r11, 3
        je          TrapThread

        ; error: you called this function incorrectly
        call        PALDEBUGBREAK
        int 3

Retry:
        ; rbx: reverse pinvoke frame

        ;; R10 = GetThread()
        mov         r10, [rbx + 8]

        test        dword ptr [r10 + OFFSETOF__Thread__m_ThreadStateFlags], TSF_Attached
        jz          AttachThread

        ;;
        ;; Check for the correct mode.  This is accessible via various odd things that we cannot completely 
        ;; prevent such as :
        ;;     1) Registering a reverse pinvoke entrypoint as a vectored exception handler
        ;;     2) Performing a managed delegate invoke on a reverse pinvoke delegate.
        ;;
        cmp         qword ptr [r10 + OFFSETOF__Thread__m_pTransitionFrame], 0
        jne         ValidTransition

        ;; Allow 'bad transitions' in when the TSF_DoNotTriggerGc mode is set.  This allows us to have 
        ;; [NativeCallable] methods that are called via the "restricted GC callouts" as well as from native,
        ;; which is necessary because the methods are CCW vtable methods on interfaces passed to native.
        test        dword ptr [r10 + OFFSETOF__Thread__m_ThreadStateFlags], TSF_DoNotTriggerGc
        jz          BadTransition

        ;; RhpTrapThreads will always be set in this case, so we must skip that check.  We must be sure to 
        ;; zero-out our 'previous transition frame' state first, however.
        mov         qword ptr [rbx], 0
        jmp         AllDone

ValidTransition:
        ; rbx: previous transition frame save slot
        ; r10: thread

        ; Save previous TransitionFrame prior to making the mode transition so that it is always valid 
        ; whenever we might attempt to hijack this thread.
        mov         r11, [r10 + OFFSETOF__Thread__m_pTransitionFrame]
        mov         [rbx], r11

        mov         qword ptr [r10 + OFFSETOF__Thread__m_pTransitionFrame], 0
        cmp         [RhpTrapThreads], 0
        jne         TrapThread

AllDone:
        movdqa      xmm0, [rsp + (20h + 4*8 + 0*10h)]
        movdqa      xmm1, [rsp + (20h + 4*8 + 1*10h)]
        movdqa      xmm2, [rsp + (20h + 4*8 + 2*10h)]
        movdqa      xmm3, [rsp + (20h + 4*8 + 3*10h)]

        mov         rcx, [rsp + (20h + 0*8)]
        mov         rdx, [rsp + (20h + 1*8)]
        mov         r8,  [rsp + (20h + 2*8)]
        mov         r9,  [rsp + (20h + 3*8)]

        ;; epilog
        add         rsp, 80h
        pop         rbx                 ; restore preserved reg
        ret

AttachThread:
        ;;
        ;; Thread attach is done here to avoid taking the ThreadStore lock while in DllMain.  The lock is 
        ;; avoided for DllMain thread attach notifications, but not process attach notifications because
        ;; our managed DllMain does work during process attach, so it needs to reverse pinvoke.
        ;;
        call        THREADSTORE__ATTACHCURRENTTHREAD
        jmp         Retry

TrapThread:
        ; rbx: previous transition frame save slot
        ; r10: thread

        mov         rcx, [rbx]
        mov         [r10 + OFFSETOF__Thread__m_pTransitionFrame], rcx

        mov         rcx, r10    ; passing Thread pointer in rcx
        call        RhpPInvokeReturnWaitEx
        jmp         Retry

BadTransition:
        add         rsp, 80h
        pop         rbx                 ; restore preserved reg
        jmp         RhpReversePInvokeBadTransition

NESTED_END RhpReversePInvoke_RarePath, _TEXT


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvokeReturn
;;
;; IN:  RCX: address of reverse pinvoke frame 
;;
;; TRASHES:  RCX, RDX, R10, R11
;;
;; PRESERVES: RAX (return value)
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
LEAF_ENTRY RhpReversePInvokeReturn, _TEXT
        mov         rdx, [rcx + 8]  ; get Thread pointer
        mov         rcx, [rcx + 0]  ; get previous M->U transition frame

        mov         [rdx + OFFSETOF__Thread__m_pTransitionFrame], rcx
        cmp         [RhpTrapThreads], 0
        jne         RhpWaitForSuspend
        ret
LEAF_END RhpReversePInvokeReturn, _TEXT


END
