;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

include AsmMacros.inc

EXTERN GetClasslibCCtorCheck        : PROC
EXTERN memcpy                       : PROC
EXTERN memcpyGCRefs                 : PROC
EXTERN memcpyGCRefsWithWriteBarrier : PROC
EXTERN memcpyAnyWithWriteBarrier    : PROC

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

RhpCheckCctor2__SlowPath_FrameSize equ 20h + 10h + 8h ;; Scratch space + storage to save off rax/rdx value + align stack 

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

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* RhpCopyMultibyteWithWriteBarrier(void*, void*, size_t)
;;
;; The purpose of this wrapper is to hoist the potential null reference exceptions of copying memory up to a place where
;; the stack unwinder and exception dispatch can properly transform the exception into a managed exception and dispatch
;; it to managed code.
;; Runs a card table update via RhpBulkWriteBarrier after the copy
;;
LEAF_ENTRY RhpCopyMultibyteWithWriteBarrier, _TEXT

        ; rcx       dest
        ; rdx       src
        ; r8        count

        test        r8, r8              ; check for a zero-length copy
        jz          NothingToCopy

        ; Now check the dest and src pointers.  If they AV, the EH subsystem will recognize the address of the AV,
        ; unwind the frame, and fixup the stack to make it look like the (managed) caller AV'ed, which will be 
        ; translated to a managed exception as usual.
ALTERNATE_ENTRY RhpCopyMultibyteWithWriteBarrierDestAVLocation
        cmp         byte ptr [rcx], 0
ALTERNATE_ENTRY RhpCopyMultibyteWithWriteBarrierSrcAVLocation
        cmp         byte ptr [rdx], 0

        ; tail-call to the GC-safe memcpy implementation
        jmp         memcpyGCRefsWithWriteBarrier

NothingToCopy:
        mov         rax, rcx            ; return dest
        ret

LEAF_END RhpCopyMultibyteWithWriteBarrier, _TEXT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* RhpCopyAnyWithWriteBarrier(void*, void*, size_t)
;;
;; The purpose of this wrapper is to hoist the potential null reference exceptions of copying memory up to a place where
;; the stack unwinder and exception dispatch can properly transform the exception into a managed exception and dispatch
;; it to managed code.
;; Runs a card table update via RhpBulkWriteBarrier after the copy if the copy may contain GC pointers
;;
LEAF_ENTRY RhpCopyAnyWithWriteBarrier, _TEXT

        ; rcx       dest
        ; rdx       src
        ; r8        count

        test        r8, r8              ; check for a zero-length copy
        jz          NothingToCopy

        ; Now check the dest and src pointers.  If they AV, the EH subsystem will recognize the address of the AV,
        ; unwind the frame, and fixup the stack to make it look like the (managed) caller AV'ed, which will be 
        ; translated to a managed exception as usual.
ALTERNATE_ENTRY RhpCopyAnyWithWriteBarrierDestAVLocation
        cmp         byte ptr [rcx], 0
ALTERNATE_ENTRY RhpCopyAnyWithWriteBarrierSrcAVLocation
        cmp         byte ptr [rdx], 0

        ; tail-call to the GC-safe memcpy implementation
        jmp         memcpyAnyWithWriteBarrier

NothingToCopy:
        mov         rax, rcx            ; return dest
        ret

LEAF_END RhpCopyAnyWithWriteBarrier, _TEXT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
; The following helper will access ("probe") a word on each page of the stack
; starting with the page right beneath rsp down to the one pointed to by r11.
; The procedure is needed to make sure that the "guard" page is pushed down below the allocated stack frame.
; The call to the helper will be emitted by JIT in the function/funclet prolog when large (larger than 0x3000 bytes) stack frame is required.
;
; NOTE: this helper will NOT modify a value of rsp and can be defined as a leaf function.

PAGE_SIZE equ 1000h

LEAF_ENTRY RhpStackProbe, _TEXT
        ; On entry:
        ;   r11 - points to the lowest address on the stack frame being allocated (i.e. [InitialSp - FrameSize])
        ;   rsp - points to some byte on the last probed page
        ; On exit:
        ;   rax - is not preserved
        ;   r11 - is preserved
        ;
        ; NOTE: this helper will probe at least one page below the one pointed by rsp.

        mov     rax, rsp               ; rax points to some byte on the last probed page
        and     rax, -PAGE_SIZE        ; rax points to the **lowest address** on the last probed page
                                       ; This is done to make the following loop end condition simpler.

ProbeLoop:
        sub     rax, PAGE_SIZE         ; rax points to the lowest address of the **next page** to probe
        test    dword ptr [rax], eax   ; rax points to the lowest address on the **last probed** page
        cmp     rax, r11
        jg      ProbeLoop              ; If (rax > r11), then we need to probe at least one more page.

        ret

LEAF_END RhpStackProbe, _TEXT

end
