;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

include AsmMacros.inc

EXTERN RhpShutdownHelper            : PROC
EXTERN GetClasslibCCtorCheck        : PROC
EXTERN memcpy                       : PROC
EXTERN memcpyGCRefs                 : PROC

;;
;; Currently called only from a managed executable once Main returns, this routine does whatever is needed to
;; cleanup managed state before exiting.
;;
;;  Input:
;;      rcx : Process exit code
;;
NESTED_ENTRY RhpShutdown, _TEXT

        INLINE_GETTHREAD        rax, r10                ; rax <- Thread pointer, r10 <- trashed
        PUSH_COOP_PINVOKE_FRAME rax, r10, no_extraStack ; rax <- in: Thread, out: trashed, r10 <- trashed
        END_PROLOGUE

        ;; Call the bulk of the helper implemented in C++. Takes the exit code already in rcx.
        call    RhpShutdownHelper

        POP_COOP_PINVOKE_FRAME  no_extraStack
        ret

NESTED_END RhpShutdown, _TEXT

;;
;; Checks whether the static class constructor for the type indicated by the context structure has been
;; executed yet. If not the classlib is called via their CheckStaticClassConstruction callback which will
;; execute the cctor and update the context to record this fact.
;;
;;  Input:
;;      rax : Address of StaticClassConstructionContext structure
;;
;;  Output:
;;      All volatile registers and the condition codes may be trashed.
;;
LEAF_ENTRY RhpCheckCctor, _TEXT

        ;; Check the m_initialized field of the context. The cctor has been run only if this equals 1 (the
        ;; initial state is 0 and the remaining values are reserved for classlib use). This check is
        ;; unsynchronized; if we go down the slow path and call the classlib then it is responsible for
        ;; synchronizing with other threads and re-checking the value.
        cmp     dword ptr [rax + OFFSETOF__StaticClassConstructionContext__m_initialized], 1
        jne     RhpCheckCctor__SlowPath
        ret
RhpCheckCctor__SlowPath:
        mov     rdx, rax
        jmp     RhpCheckCctor2 ; Tail-call the check cctor helper that can actually call the cctor
LEAF_END RhpCheckCctor, _TEXT

;;
;; Checks whether the static class constructor for the type indicated by the context structure has been
;; executed yet. If not the classlib is called via their CheckStaticClassConstruction callback which will
;; execute the cctor and update the context to record this fact.
;;
;;  Input:
;;      rax : Value that must be preserved in this register across the cctor check.
;;      rdx : Address of StaticClassConstructionContext structure
;;
;;  Output:
;;      All volatile registers other than rax may be trashed and the condition codes may also be trashed.
;;
LEAF_ENTRY RhpCheckCctor2, _TEXT

        ;; Check the m_initialized field of the context. The cctor has been run only if this equals 1 (the
        ;; initial state is 0 and the remaining values are reserved for classlib use). This check is
        ;; unsynchronized; if we go down the slow path and call the classlib then it is responsible for
        ;; synchronizing with other threads and re-checking the value.
        cmp     dword ptr [rdx + OFFSETOF__StaticClassConstructionContext__m_initialized], 1
        jne     RhpCheckCctor2__SlowPath
        ret

LEAF_END RhpCheckCctor2, _TEXT

;;
;; Slow path helper for RhpCheckCctor2.
;;
;;  Input:
;;      rax : Value that must be preserved in this register across the cctor check.
;;      rdx : Address of StaticClassConstructionContext structure
;;
;;  Output:
;;      All volatile registers other than rax may be trashed and the condition codes may also be trashed.
;;
NESTED_ENTRY RhpCheckCctor2__SlowPath, _TEXT

RhpCheckCctor2__SlowPath_FrameSize equ 20h + 10h  ;; Scratch space + storage to save off rax, and rdx value

        alloc_stack RhpCheckCctor2__SlowPath_FrameSize
        save_reg_postrsp    rdx, 20h
        save_reg_postrsp    rax, 28h

        END_PROLOGUE

        ;; Call a C++ helper to retrieve the address of the classlib callback.

        ;; The caller's return address is passed as the argument to the helper; it's an address in the module
        ;; and is used by the helper to locate the classlib.
        mov     rcx, [rsp + RhpCheckCctor2__SlowPath_FrameSize]

        call    GetClasslibCCtorCheck

        ;; Rax now contains the address of the classlib method to call. The single argument is the context
        ;; structure address currently in stashed on the stack. Clean up and tail call to the classlib
        ;; callback so we're not on the stack should a GC occur (so we don't need to worry about transition
        ;; frames).
        mov     rdx, [rsp + 20h]
        mov     rcx, [rsp + 28h]
        add     rsp, RhpCheckCctor2__SlowPath_FrameSize
        ;; Tail-call the classlib cctor check function. Note that the incoming rax value is moved to rcx
        ;; and the classlib cctor check function is required to return that value, so that rax is preserved
        ;; across a RhpCheckCctor call.
        TAILJMP_RAX

NESTED_END RhpCheckCctor2__SlowPath, _TEXT


;;
;; Input:
;;      rcx: address of location on stack containing return address.
;;      
;; Outpt:
;;      rax: proper (unhijacked) return address
;;
;; Trashes: rdx
;;
LEAF_ENTRY RhpLoadReturnAddress, _TEXT

        INLINE_GETTHREAD   rax, rdx
        cmp     rcx, [rax + OFFSETOF__Thread__m_ppvHijackedReturnAddressLocation]
        je      GetHijackedReturnAddress
        mov     rax, [rcx]
        ret

GetHijackedReturnAddress:
        mov     rax, [rax + OFFSETOF__Thread__m_pvHijackedReturnAddress]
        ret

LEAF_END RhpLoadReturnAddress, _TEXT


;;
;; RCX = output buffer (an IntPtr[] managed object)
;;
NESTED_ENTRY RhGetCurrentThreadStackTrace, _TEXT
        INLINE_GETTHREAD        rax, r10        ; rax <- Thread pointer, r10 <- trashed
        mov                     r11, rax        ; r11 <- Thread pointer
        PUSH_COOP_PINVOKE_FRAME rax, r10, no_extraStack ; rax <- in: Thread, out: trashed, r10 <- trashed
        END_PROLOGUE

        ;; pass-through argument registers
        call        RhpCalculateStackTraceWorker

        POP_COOP_PINVOKE_FRAME  no_extraStack
        ret
NESTED_END RhGetCurrentThreadStackTrace, _TEXT


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* RhpCopyMultibyteNoGCRefs(void*, void*, size_t)
;;
;; The purpose of this wrapper is to hoist the potential null reference exceptions of copying memory up to a place where
;; the stack unwinder and exception dispatch can properly transform the exception into a managed exception and dispatch
;; it to managed code.
;;
LEAF_ENTRY RhpCopyMultibyteNoGCRefs, _TEXT

        ; rcx       dest
        ; rdx       src
        ; r8        count

        test        r8, r8              ; check for a zero-length copy
        jz          NothingToCopy

        ; Now check the dest and src pointers.  If they AV, the EH subsystem will recognize the address of the AV,
        ; unwind the frame, and fixup the stack to make it look like the (managed) caller AV'ed, which will be 
        ; translated to a managed exception as usual.
ALTERNATE_ENTRY RhpCopyMultibyteNoGCRefsDestAVLocation
        cmp         byte ptr [rcx], 0   
ALTERNATE_ENTRY RhpCopyMultibyteNoGCRefsSrcAVLocation
        cmp         byte ptr [rdx], 0

        ; tail-call to plain-old-memcpy
        jmp         memcpy

NothingToCopy:
        mov         rax, rcx            ; return dest
        ret

LEAF_END RhpCopyMultibyteNoGCRefs, _TEXT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* RhpCopyMultibyte(void*, void*, size_t)
;;
;; The purpose of this wrapper is to hoist the potential null reference exceptions of copying memory up to a place where
;; the stack unwinder and exception dispatch can properly transform the exception into a managed exception and dispatch
;; it to managed code.
;;
LEAF_ENTRY RhpCopyMultibyte, _TEXT

        ; rcx       dest
        ; rdx       src
        ; r8        count

        test        r8, r8              ; check for a zero-length copy
        jz          NothingToCopy

        ; Now check the dest and src pointers.  If they AV, the EH subsystem will recognize the address of the AV,
        ; unwind the frame, and fixup the stack to make it look like the (managed) caller AV'ed, which will be 
        ; translated to a managed exception as usual.
ALTERNATE_ENTRY RhpCopyMultibyteDestAVLocation
        cmp         byte ptr [rcx], 0
ALTERNATE_ENTRY RhpCopyMultibyteSrcAVLocation
        cmp         byte ptr [rdx], 0

        ; tail-call to the GC-safe memcpy implementation
        jmp         memcpyGCRefs

NothingToCopy:
        mov         rax, rcx            ; return dest
        ret

LEAF_END RhpCopyMultibyte, _TEXT

end
