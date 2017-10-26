;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

;;
;; Define the helpers used to implement the write barrier required when writing an object reference into a
;; location residing on the GC heap. Such write barriers allow the GC to optimize which objects in
;; non-ephemeral generations need to be scanned for references to ephemeral objects during an ephemeral
;; collection.
;;

#include "AsmMacros.h"

    MACRO
        ;; On entry:
        ;;   $DESTREG: location to be updated
        ;;   $REFREG:  objectref to be stored
        ;; On exit:
        ;;   $DESTREG:   trashed
        ;;   x9:         trashed
        INSERT_UNCHECKED_WRITE_BARRIER_CORE $DESTREG, $REFREG
            ;; we can skip the card table write if the reference is to
            ;; an object not on the epehemeral segment.
            adrp    x9, g_ephemeral_low
            ldr     x9, [x9, g_ephemeral_low]
            cmp     $REFREG, x9
            blt     %ft0

            adrp    x9, g_ephemeral_high
            ldr     x9, [x9, g_ephemeral_high]
            cmp     $REFREG, x9
            bge     %ft0

            ;; set this object's card, if it hasn't already been set.
            adrp    x9, g_card_table
            ldr     x9, [x9, g_card_table]
            add     $DESTREG, x9, $DESTREG lsr #11
            ldrb    w9, [$DESTREG]
            cmp     x9, 0xFF
            beq     %ft0

            mov     x9, 0xFF
            strb    w9, [$DESTREG]

0
            ;; exit label
    MEND ;; INSERT_UNCHECKED_WRITE_BARRIER_CORE

    MACRO
        ;; On entry:
        ;;   $DESTREG: location to be updated
        ;;   $REFREG:  objectref to be stored
        ;; On exit:
        ;;   $DESTREG:   trashed
        ;;   x9:         trashed
        INSERT_CHECKED_WRITE_BARRIER_CORE $DESTREG, $REFREG
            ;; the "check" of this checked write barrier - is $DESTREG
            ;; within the heap? if no, early out.
            adrp    x9, g_lowest_address
            ldr     x9, [x9, g_lowest_address]
            cmp     $DESTREG, x9
            blt     %ft0

            adrp    x9, g_highest_address
            ldr     x9, [x9, g_highest_address]
            cmp     $DESTREG, x9
            bgt     %ft0

            INSERT_UNCHECKED_WRITE_BARRIER_CORE $DESTREG, $REFREG

0
            ;; exit label
    MEND ;; INSERT_CHECKED_WRITE_BARRIER_CORE


    TEXTAREA

    ;; RhpCheckedAssignRef(Object** dst, Object* src)
    ;;
    ;; write barrier for writes to objects that may reside
    ;; on the managed heap.
    ;;
    ;; On entry:
    ;;   x0 : the destination address (LHS of the assignment).
    ;;        May not be an object reference (hence the checked).
    ;;   x1 : the object reference (RHS of the assignment).
    ;; On exit:
    ;;   x1 : trashed
    ;;   x9 : trashed
    LEAF_ENTRY RhpCheckedAssignRef
    ALTERNATE_ENTRY RhpCheckedAssignRefAVLocation
    ALTERNATE_ENTRY RhpCheckedAssignRefX1
    ALTERNATE_ENTRY RhpCheckedAssignRefX1AVLocation
        stlr    x1, [x0]

        INSERT_CHECKED_WRITE_BARRIER_CORE x0, x1

        ret     lr
    LEAF_END RhpCheckedAssignRef

    ;; RhpAssignRef(Object** dst, Object* src)
    ;;
    ;; write barrier for writes to objects that are known to
    ;; reside on the managed heap.
    ;;
    ;; On entry:
    ;;  x0 : the destination address (LHS of the assignment).
    ;;  x1 : the object reference (RHS of the assignment).
    ;; On exit:
    ;;  x1 : trashed
    ;;  x9 : trashed
    LEAF_ENTRY RhpAssignRef
    ALTERNATE_ENTRY RhpAssignRefAVLocation
    ALTERNATE_ENTRY RhpAssignRefX1
    ALTERNATE_ENTRY RhpAssignRefX1AVLocation
        stlr    x1, [x0]

        INSERT_UNCHECKED_WRITE_BARRIER_CORE x0, x1

        ret     lr
    LEAF_END RhpAssignRef

;; Interlocked operation helpers where the location is an objectref, thus requiring a GC write barrier upon
;; successful updates. 

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpCheckedLockCmpXchgAVLocation
;; - Function "UnwindWriteBarrierToCaller" assumes no registers where pushed and LR contains the return address

    ;; RhpCheckedLockCmpXchg(Object** dest, Object* value, Object* comparand)
    ;;
    ;; Interlocked compare exchange on objectref.
    ;;
    ;; On entry:
    ;;  x0: pointer to objectref
    ;;  x1: exchange value
    ;;  x2: comparand
    ;;
    ;; On exit:
    ;;  x0: original value of objectref
    ;;  x9: trashed
    ;;  x10: trashed
    ;;
    LEAF_ENTRY RhpCheckedLockCmpXchg
    ALTERNATE_ENTRY  RhpCheckedLockCmpXchgAVLocation
CmpXchgRetry
        ldaxr   x10, [x0]
        cmp     x10, x2
        bne NoUpdate

        stlxr   w9, x1, [x0]
        cbnz    w9, CmpXchgRetry

        ;; write was successful.
        INSERT_CHECKED_WRITE_BARRIER_CORE x0, x1

NoUpdate
        ;; x10 still contains the original value.
        mov     x0, x10
        ret     lr

    LEAF_END RhpCheckedLockCmpXchg

    ;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
    ;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen within at RhpCheckedXchgAVLocation
    ;; - Function "UnwindWriteBarrierToCaller" assumes no registers where pushed and LR contains the return address
 
    ;; RhpCheckedXchg(Object** destination, Object* value)
    ;;
    ;; Interlocked exchange on objectref.
    ;;
    ;; On entry:
    ;;  x0: pointer to objectref
    ;;  x1: exchange value
    ;;
    ;; On exit:
    ;;  x0: original value of objectref
    ;;  x9: trashed
    ;;  x10: trashed
    ;;
    LEAF_ENTRY RhpCheckedXchg
    ALTERNATE_ENTRY  RhpCheckedXchgAVLocation
ExchangeRetry
        ;; read the existing memory location.
        ldaxr   x10,  [x0]

        ;; INSERT_CHECKED_WRITE_BARRIER_CORE trashes x9,
        ;; so we'll use it for the short-lifetime variable here.
        stlxr   w9, x1, [x0]
        cbnz    w9, ExchangeRetry

        ;; write was successful.
        INSERT_CHECKED_WRITE_BARRIER_CORE x0, x1

        ;; x10 still contains the original value.
        mov     x0, x10
        ret     lr
    LEAF_END RhpCheckedXchg

    end
