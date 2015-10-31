;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

include AsmMacros.inc

;;
;; Unmanaged helpers used by the managed System.GC class.
;;

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
NESTED_ENTRY RhCollect, _TEXT

        INLINE_GETTHREAD        rax, r10        ; rax <- Thread pointer, r10 <- trashed
        mov                     r11, rax        ; r11 <- Thread pointer
        PUSH_COOP_PINVOKE_FRAME rax, r10, no_extraStack ; rax <- in: Thread, out: trashed, r10 <- trashed
        END_PROLOGUE

        ; ECX: generation to collect
        ; EDX: alloc flags
        ; R11: Thread *

        ;; Initiate the collection.
        ;; void RedhawkGCInterface::GarbageCollect(UInt32 uGeneration, UInt32 uMode)
        call        REDHAWKGCINTERFACE__GARBAGECOLLECT

        POP_COOP_PINVOKE_FRAME  no_extraStack
        ret

NESTED_END RhCollect, _TEXT

; EXTERN_C UIntNative get_gdt() -- used by the GC
LEAF_ENTRY get_gdt, _TEXT
        push        rax
        sgdt        [rsp-2]
        pop         rax
        ret
LEAF_END get_gdt, _TEXT


;; extern "C" DWORD getcpuid(DWORD arg, unsigned char result[16]);
NESTED_ENTRY getcpuid, _TEXT

        push_nonvol_reg    rbx
        push_nonvol_reg    rsi
    END_PROLOGUE

        mov     eax, ecx                ; first arg
        mov     rsi, rdx                ; second arg (result)
        cpuid
        mov     [rsi+ 0], eax
        mov     [rsi+ 4], ebx
        mov     [rsi+ 8], ecx
        mov     [rsi+12], edx
        pop     rsi
        pop     rbx
        ret
NESTED_END getcpuid, _TEXT

;The following function uses Deterministic Cache Parameter leafs to crack the cache hierarchy information on Prescott & Above platforms. 
;  This function takes 3 arguments:
;     Arg1 is an input to ECX. Used as index to specify which cache level to return information on by CPUID.
;         Arg1 is already passed in ECX on call to getextcpuid, so no explicit assignment is required;  
;     Arg2 is an input to EAX. For deterministic code enumeration, we pass in 4H in arg2.
;     Arg3 is a pointer to the return dwbuffer
NESTED_ENTRY getextcpuid, _TEXT
        push_nonvol_reg    rbx
        push_nonvol_reg    rsi
    END_PROLOGUE
        
        mov     eax, edx                ; second arg (input to  EAX)
        mov     rsi, r8                 ; third arg  (pointer to return dwbuffer)       
        cpuid
        mov     [rsi+ 0], eax
        mov     [rsi+ 4], ebx
        mov     [rsi+ 8], ecx
        mov     [rsi+12], edx
        pop     rsi
        pop     rbx

        ret
NESTED_END getextcpuid, _TEXT

;; Re-register an object of a finalizable type for finalization.
;;  rcx : object
;;
NESTED_ENTRY RhReRegisterForFinalize, _TEXT

        EXTERN RhReRegisterForFinalizeHelper : PROC

        INLINE_GETTHREAD        rax, r10        ; rax <- Thread pointer, r10 <- trashed
        PUSH_COOP_PINVOKE_FRAME rax, r10, no_extraStack ; rax <- in: Thread, out: trashed, r10 <- trashed
        END_PROLOGUE

        ;; Call to the C++ helper that does most of the work.
        call        RhReRegisterForFinalizeHelper

        POP_COOP_PINVOKE_FRAME  no_extraStack
        ret

NESTED_END RhReRegisterForFinalize, _TEXT

;; RhGetGcTotalMemory
;;  No inputs, returns total GC memory as 64-bit value in rax.
;;
NESTED_ENTRY RhGetGcTotalMemory, _TEXT

        EXTERN RhGetGcTotalMemoryHelper : PROC

        INLINE_GETTHREAD        rax, r10        ; rax <- Thread pointer, r10 <- trashed
        PUSH_COOP_PINVOKE_FRAME rax, r10, no_extraStack ; rax <- in: Thread, out: trashed, r10 <- trashed
        END_PROLOGUE

        ;; Call to the C++ helper that does most of the work.
        call        RhGetGcTotalMemoryHelper

        POP_COOP_PINVOKE_FRAME  no_extraStack
        ret

NESTED_END RhGetGcTotalMemory, _TEXT

        end
