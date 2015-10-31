;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

;;
;; Unmanaged helpers used by the managed System.GC class.
;;

    .586
    .model  flat
    option  casemap:none
    .code

include AsmMacros.inc

;; Force a collection.
;; On entry:
;;  ECX = generation to collect (-1 for all)
;;  EDX = mode (default, forced or optimized)
;;
;; This helper is special because it's not called via a p/invoke that transitions to pre-emptive mode. We do
;; this because the GC wants to be called in co-operative mode. But we are going to cause a GC, so we need to
;; make this stack crawlable. As a result we use the same trick as the allocation helpers and build an
;; explicit transition frame based on the entry state so the GC knows where to start crawling this thread's
;; stack.
FASTCALL_FUNC RhCollect, 8

        ;; Prolog, build an EBP frame
        push        ebp
        mov         ebp, esp

        ;; Save EDX (mode argument) since we need a register to stash thread pointer
        push        edx

        ;; edx = GetThread(), TRASHES eax
        INLINE_GETTHREAD edx, eax

        ;; Save managed state in a frame and update the thread so it can find this frame once we transition to
        ;; pre-emptive mode in the garbage collection.
        PUSH_COOP_PINVOKE_FRAME edx

        ;; Initiate the collection.
        push        [ebp - 4]                           ;; Push mode
        push        ecx                                 ;; Push generation number
        call        REDHAWKGCINTERFACE__GARBAGECOLLECT

        ;; Restore register state.
        POP_COOP_PINVOKE_FRAME

        ;; Discard saved EDX
        add         esp, 4

        ;; Epilog, tear down EBP frame and return.
        pop         ebp
        ret

FASTCALL_ENDFUNC

;; DWORD getcpuid(DWORD arg, unsigned char result[16])

FASTCALL_FUNC getcpuid, 8

        push    ebx
        push    esi
        mov     esi, edx
        mov     eax, ecx
        cpuid
        mov     [esi+ 0], eax
        mov     [esi+ 4], ebx
        mov     [esi+ 8], ecx
        mov     [esi+12], edx
        pop     esi
        pop     ebx

        ret

FASTCALL_ENDFUNC

;; The following function uses Deterministic Cache Parameter leafs to crack the cache hierarchy information on Prescott & Above platforms. 
;;  This function takes 3 arguments:
;;     Arg1 is an input to ECX. Used as index to specify which cache level to return infoformation on by CPUID.
;;     Arg2 is an input to EAX. For deterministic code enumeration, we pass in 4H in arg2.
;;     Arg3 is a pointer to the return buffer
;;   No need to check whether or not CPUID is supported because we have already called CPUID with success to come here.

;; DWORD getextcpuid(DWORD arg1, DWORD arg2, unsigned char result[16])

FASTCALL_FUNC getextcpuid, 12

        push    ebx
        push    esi
        mov     ecx, ecx
        mov     eax, edx
        cpuid
        mov     esi, [esp + 12]
        mov     [esi+ 0], eax
        mov     [esi+ 4], ebx
        mov     [esi+ 8], ecx
        mov     [esi+12], edx
        pop     esi
        pop     ebx

        ret

FASTCALL_ENDFUNC

;; Re-register an object of a finalizable type for finalization.
;;  ecx : object
;;
FASTCALL_FUNC RhReRegisterForFinalize, 4

        EXTERN @RhReRegisterForFinalizeHelper@4 : PROC

        ;; Prolog, build an EBP frame
        push        ebp
        mov         ebp, esp

        ;; edx = GetThread(), TRASHES eax
        INLINE_GETTHREAD edx, eax

        ;; Save managed state in a frame and update the thread so it can find this frame if we transition to
        ;; pre-emptive mode in the helper below.
        PUSH_COOP_PINVOKE_FRAME edx

        ;; Call to the C++ helper that does most of the work.
        call        @RhReRegisterForFinalizeHelper@4

        ;; Restore register state.
        POP_COOP_PINVOKE_FRAME

        ;; Epilog, tear down EBP frame and return.
        pop         ebp
        ret

FASTCALL_ENDFUNC

;; RhGetGcTotalMemory
;;  No inputs, returns total GC memory as 64-bit value in eax/edx.
;;
FASTCALL_FUNC RhGetGcTotalMemory, 0

        EXTERN @RhGetGcTotalMemoryHelper@0 : PROC

        ;; Prolog, build an EBP frame
        push        ebp
        mov         ebp, esp

        ;; edx = GetThread(), TRASHES eax
        INLINE_GETTHREAD edx, eax

        ;; Save managed state in a frame and update the thread so it can find this frame if we transition to
        ;; pre-emptive mode in the helper below.
        PUSH_COOP_PINVOKE_FRAME edx

        ;; Call to the C++ helper that does most of the work.
        call        @RhGetGcTotalMemoryHelper@0

        ;; Restore register state.
        POP_COOP_PINVOKE_FRAME

        ;; Epilog, tear down EBP frame and return.
        pop         ebp
        ret

FASTCALL_ENDFUNC

        end
