;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

        .586
        .model  flat
        option  casemap:none
        .code


include AsmMacros.inc

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
_RhpWaitForSuspend proc public
        push        ebp
        mov         ebp, esp
        push        eax
        push        ecx
        push        edx

        ; passing Thread pointer in ecx, trashes eax
        INLINE_GETTHREAD ecx, eax
        call        RhpPInvokeWaitEx
        
        pop         edx
        pop         ecx
        pop         eax
        pop         ebp
        ret
_RhpWaitForSuspend endp


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpWaitForGC
;;
;;
;; INPUT: ECX: transition frame
;;
;; OUTPUT: 
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
_RhpWaitForGC proc public
        push        ebp
        mov         ebp, esp
        push        eax
        push        edx
        push        ebx
        push        esi

        mov         ebx, ecx
        mov         esi, [ebx + OFFSETOF__PInvokeTransitionFrame__m_pThread]

        test        dword ptr [esi + OFFSETOF__Thread__m_ThreadStateFlags], TSF_DoNotTriggerGc
        jnz         Done

RetryWaitForGC:
        ; EBX: transition frame
        ; ESI: thread

        mov         [esi + OFFSETOF__Thread__m_pTransitionFrame], ebx

        mov         ecx, esi                        ; passing Thread pointer in ecx
        call        @RhpPInvokeReturnWaitEx@4

        mov         dword ptr [esi + OFFSETOF__Thread__m_pTransitionFrame], 0

        cmp         [RhpTrapThreads], 0
        jne         RetryWaitForGC

Done:
        pop         esi
        pop         ebx
        pop         edx
        pop         eax
        pop         ebp
        ret
_RhpWaitForGC endp

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvoke2
;;
;; INCOMING: ECX -- address of reverse pinvoke frame
;;
;; This is useful for calling with a standard calling convention for code generators that don't insert this in 
;; the prolog.
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
FASTCALL_FUNC RhpReversePInvoke2, 0
        mov         eax, ecx
        jmp         @RhpReversePInvoke@0
FASTCALL_ENDFUNC

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvoke
;;
;; IN:  EAX: address of reverse pinvoke frame
;;                  0: save slot for previous M->U transition frame
;;                  4: save slot for thread pointer to avoid re-calc in epilog sequence
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
FASTCALL_FUNC RhpReversePInvoke, 0
        push        ecx         ; save arg regs -- we could omit this if we knew the calling convention wasn't fastcall.
        push        edx         ; ...

        ;; edx = GetThread(), TRASHES ecx
        INLINE_GETTHREAD edx, ecx
        mov         [eax + 4], edx          ; save thread pointer for RhpReversePInvokeReturn
        
        ; edx = thread
        ; eax = prev save slot
        ; ecx = scratch

        test        dword ptr [edx + OFFSETOF__Thread__m_ThreadStateFlags], TSF_Attached
        jz          AttachThread

ThreadAttached:
        ;;
        ;; Check for the correct mode.  This is accessible via various odd things that we cannot completely 
        ;; prevent such as :
        ;;     1) Registering a reverse pinvoke entrypoint as a vectored exception handler
        ;;     2) Performing a managed delegate invoke on a reverse pinvoke delegate.
        ;;
        cmp         dword ptr [edx + OFFSETOF__Thread__m_pTransitionFrame], 0
        jne         ValidTransition

        ;; Allow 'bad transitions' in when the TSF_DoNotTriggerGc mode is set.  This allows us to have 
        ;; [NativeCallable] methods that are called via the "restricted GC callouts" as well as from native,
        ;; which is necessary because the methods are CCW vtable methods on interfaces passed to native.
        test        dword ptr [edx + OFFSETOF__Thread__m_ThreadStateFlags], TSF_DoNotTriggerGc
        jz          BadTransition

        ;; zero-out our 'previous transition frame' save slot
        mov         dword ptr [eax], 0

        ;; nothing more to do
        jmp         AllDone

ValidTransition:
        ; Save previous TransitionFrame prior to making the mode transition so that it is always valid 
        ; whenever we might attempt to hijack this thread.
        mov         ecx, [edx + OFFSETOF__Thread__m_pTransitionFrame]
        mov         [eax], ecx

ReverseRetry:
        mov         dword ptr [edx + OFFSETOF__Thread__m_pTransitionFrame], 0
        cmp         [RhpTrapThreads], 0
        jne         ReverseTrapReturningThread

AllDone:
        pop         edx         ; restore arg reg
        pop         ecx         ; restore arg reg
        ret

AttachThread:
        ;;
        ;; Thread attach is done here to avoid taking the ThreadStore lock while in DllMain.  The lock is 
        ;; avoided for DllMain thread attach notifications, but not process attach notifications because
        ;; our managed DllMain does work during process attach, so it needs to reverse pinvoke.
        ;;

        ; edx = thread
        ; eax = prev save slot
        ; ecx = scratch
        
        push        eax
        push        edx
        call        THREADSTORE__ATTACHCURRENTTHREAD
        pop         edx
        pop         eax
        jmp         ThreadAttached

ReverseTrapReturningThread:
        ; edx = thread
        ; eax = prev save slot
        ; ecx = scratch

        mov         ecx, [eax]
        mov         [edx + OFFSETOF__Thread__m_pTransitionFrame], ecx

        push        eax
        push        edx
        mov         ecx, edx                    ; passing Thread pointer in ecx
        call        @RhpPInvokeReturnWaitEx@4
        pop         edx
        pop         eax
        
        jmp         ReverseRetry

BadTransition:
        pop         edx
        pop         ecx
        jmp         RhpReversePInvokeBadTransition
FASTCALL_ENDFUNC

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvokeReturn
;;
;; IN:  ECX: address of reverse pinvoke frame
;;                  0: save slot for previous M->U transition frame
;;                  4: save slot for thread pointer to avoid re-calc in epilog sequence
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
FASTCALL_FUNC RhpReversePInvokeReturn, 0
        push        edx         ; save return value

        mov         edx, [ecx + 4]  ; get Thread pointer
        mov         ecx, [ecx + 0]  ; get previous M->U transition frame

        mov         [edx + OFFSETOF__Thread__m_pTransitionFrame], ecx
        cmp         [RhpTrapThreads], 0
        pop         edx         ; restore return value
        jne         _RhpWaitForSuspend
        ret

FASTCALL_ENDFUNC


        end
