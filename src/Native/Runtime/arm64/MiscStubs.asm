;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

    EXTERN memcpy

    TEXTAREA

;;
;; Checks whether the static class constructor for the type indicated by the context structure has been
;; executed yet. If not the classlib is called via their CheckStaticClassConstruction callback which will
;; execute the cctor and update the context to record this fact.
;;
;;  Input:
;;      r0 : Address of StaticClassConstructionContext structure
;;
;;  Output:
;;      All volatile registers and the condition codes may be trashed.
;;
    LEAF_ENTRY RhpCheckCctor
        brk 0xf000
    LEAF_END RhpCheckCctor

;;
;; Checks whether the static class constructor for the type indicated by the context structure has been
;; executed yet. If not the classlib is called via their CheckStaticClassConstruction callback which will
;; execute the cctor and update the context to record this fact.
;;
;;  Input:
;;      r0 : Value that must be preserved in this register across the cctor check.
;;      r1 : Address of StaticClassConstructionContext structure
;;
;;  Output:
;;      All volatile registers other than r0 may be trashed and the condition codes may also be trashed.
;;
    LEAF_ENTRY RhpCheckCctor2
        brk 0xf000
    LEAF_END RhpCheckCctor2

;;
;; Slow path helper for RhpCheckCctor.
;;
;;  Input:
;;      r0 : Value that must be preserved in this register across the cctor check.
;;      r1 : Address of StaticClassConstructionContext structure
;;
;;  Output:
;;      All volatile registers other than r0 may be trashed and the condition codes may also be trashed.
;;
    NESTED_ENTRY RhpCheckCctor2__SlowPath
        brk 0xf000
    NESTED_END RhpCheckCctor__SlowPath2


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* RhpCopyMultibyteNoGCRefs(void*, void*, size_t)
;;
;; The purpose of this wrapper is to hoist the potential null reference exceptions of copying memory up to a place where
;; the stack unwinder and exception dispatch can properly transform the exception into a managed exception and dispatch
;; it to managed code.
;;

    LEAF_ENTRY    RhpCopyMultibyteNoGCRefs

        ; x0    dest
        ; x1    src
        ; x2    count

        cbz     x2, NothingToCopy_NoGCRefs  ; check for a zero-length copy

        ; Now check the dest and src pointers.  If they AV, the EH subsystem will recognize the address of the AV,
        ; unwind the frame, and fixup the stack to make it look like the (managed) caller AV'ed, which will be 
        ; translated to a managed exception as usual.
    ALTERNATE_ENTRY RhpCopyMultibyteNoGCRefsDestAVLocation
        ldrb    wzr, [x0]
    ALTERNATE_ENTRY RhpCopyMultibyteNoGCRefsSrcAVLocation
        ldrb    wzr, [x1]

        ; tail-call to plain-old-memcpy
        b       memcpy

NothingToCopy_NoGCRefs
        ; dest is already in x0
        ret

    LEAF_END


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* RhpCopyMultibyte(void*, void*, size_t)
;;
;; The purpose of this wrapper is to hoist the potential null reference exceptions of copying memory up to a place where
;; the stack unwinder and exception dispatch can properly transform the exception into a managed exception and dispatch
;; it to managed code.
;;

    LEAF_ENTRY    RhpCopyMultibyte
        brk 0xf000
    ALTERNATE_ENTRY RhpCopyMultibyteDestAVLocation
        brk 0xf000
    ALTERNATE_ENTRY RhpCopyMultibyteSrcAVLocation
        brk 0xf000
    LEAF_END

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* RhpCopyMultibyteWithWriteBarrier(void*, void*, size_t)
;;
;; The purpose of this wrapper is to hoist the potential null reference exceptions of copying memory up to a place where
;; the stack unwinder and exception dispatch can properly transform the exception into a managed exception and dispatch
;; it to managed code.
;; Runs a card table update via RhpBulkWriteBarrier after the copy
;;

    LEAF_ENTRY    RhpCopyMultibyteWithWriteBarrier
        brk 0xf000
    ALTERNATE_ENTRY RhpCopyMultibyteWithWriteBarrierDestAVLocation
        brk 0xf000
    ALTERNATE_ENTRY RhpCopyMultibyteWithWriteBarrierSrcAVLocation
        brk 0xf000
    LEAF_END

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* RhpCopyAnyWithWriteBarrier(void*, void*, size_t)
;;
;; The purpose of this wrapper is to hoist the potential null reference exceptions of copying memory up to a place where
;; the stack unwinder and exception dispatch can properly transform the exception into a managed exception and dispatch
;; it to managed code.
;; Runs a card table update via RhpBulkWriteBarrier after the copy if it contained GC pointers
;;

    LEAF_ENTRY    RhpCopyAnyWithWriteBarrier
        brk 0xf000
    ALTERNATE_ENTRY RhpCopyAnyWithWriteBarrierDestAVLocation
        brk 0xf000
    ALTERNATE_ENTRY RhpCopyAnyWithWriteBarrierSrcAVLocation
        brk 0xf000
    LEAF_END

    end
