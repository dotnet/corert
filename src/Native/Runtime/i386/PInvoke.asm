;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

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

        call        RhpWaitForSuspend2
        
        pop         edx
        pop         ecx
        pop         eax
        pop         ebp
        ret
_RhpWaitForSuspend endp


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpWaitForGCNoAbort
;;
;;
;; INPUT: ECX: transition frame
;;
;; OUTPUT: 
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
_RhpWaitForGCNoAbort proc public
        push        ebp
        mov         ebp, esp
        push        eax
        push        edx
        push        ebx
        push        esi

        mov         esi, [ecx + OFFSETOF__PInvokeTransitionFrame__m_pThread]

        test        dword ptr [esi + OFFSETOF__Thread__m_ThreadStateFlags], TSF_DoNotTriggerGc
        jnz         Done

        ; passing transition frame pointer in ecx
        call        RhpWaitForGC2

Done:
        pop         esi
        pop         ebx
        pop         edx
        pop         eax
        pop         ebp
        ret
_RhpWaitForGCNoAbort endp

RhpThrowHwEx equ @RhpThrowHwEx@0
EXTERN RhpThrowHwEx : PROC

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
        push        ebx

        mov         ebx, ecx
        test        [RhpTrapThreads], TrapThreadsFlags_TrapThreads
        jz          NoWait

        call        _RhpWaitForGCNoAbort
NoWait:
        test        [RhpTrapThreads], TrapThreadsFlags_AbortInProgress
        jz          Done
        test        dword ptr [ebx + OFFSETOF__PInvokeTransitionFrame__m_Flags], PTFF_THREAD_ABORT
        jz          Done

        mov         ecx, STATUS_REDHAWK_THREAD_ABORT
        pop         ebx
        pop         ebp
        pop         edx                 ; return address as exception RIP
        jmp         RhpThrowHwEx        ; Throw the ThreadAbortException as a special kind of hardware exception
Done:
        pop         ebx
        pop         ebp
        ret
_RhpWaitForGC endp

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
        je          CheckBadTransition

        ; Save previous TransitionFrame prior to making the mode transition so that it is always valid 
        ; whenever we might attempt to hijack this thread.
        mov         ecx, [edx + OFFSETOF__Thread__m_pTransitionFrame]
        mov         [eax], ecx

ReverseRetry:
        mov         dword ptr [edx + OFFSETOF__Thread__m_pTransitionFrame], 0
        test        [RhpTrapThreads], TrapThreadsFlags_TrapThreads
        jnz         ReverseTrapReturningThread

AllDone:
        pop         edx         ; restore arg reg
        pop         ecx         ; restore arg reg
        ret
        
CheckBadTransition:
        ;; Allow 'bad transitions' in when the TSF_DoNotTriggerGc mode is set.  This allows us to have 
        ;; [UnmanagedCallersOnly] methods that are called via the "restricted GC callouts" as well as from native,
        ;; which is necessary because the methods are CCW vtable methods on interfaces passed to native.
        test        dword ptr [edx + OFFSETOF__Thread__m_ThreadStateFlags], TSF_DoNotTriggerGc
        jz          BadTransition

        ;; zero-out our 'previous transition frame' save slot
        mov         dword ptr [eax], 0

        ;; nothing more to do
        jmp         AllDone

ReverseTrapReturningThread:
        ;; put the previous frame back (sets us back to preemptive mode)
        mov         ecx, [eax]
        mov         [edx + OFFSETOF__Thread__m_pTransitionFrame], ecx

AttachThread:
        mov         ecx, eax                    ; arg <- address of reverse pinvoke frame
        call        RhpReversePInvokeAttachOrTrapThread2
        jmp         AllDone

BadTransition:
        pop         edx
        pop         ecx
        mov         ecx, dword ptr [esp]        ; arg <- return address
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
        test        [RhpTrapThreads], TrapThreadsFlags_TrapThreads
        pop         edx         ; restore return value
        jnz         _RhpWaitForSuspend
        ret

FASTCALL_ENDFUNC


        end
