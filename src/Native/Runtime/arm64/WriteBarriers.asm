;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

;;
;; Define the helpers used to implement the write barrier required when writing an object reference into a
;; location residing on the GC heap. Such write barriers allow the GC to optimize which objects in
;; non-ephemeral generations need to be scanned for references to ephemeral objects during an ephemeral
;; collection.
;;

#include "AsmMacros.h"

    TEXTAREA

;; Macro used to copy contents of newly updated GC heap locations to a shadow copy of the heap. This is used
;; during garbage collections to verify that object references where never written to the heap without using a
;; write barrier. Note that we're potentially racing to update the shadow heap while other threads are writing
;; new references to the real heap. Since this can't be solved perfectly without critical sections around the
;; entire update process, we instead update the shadow location and then re-check the real location (as two
;; ordered operations) and if there is a disparity we'll re-write the shadow location with a special value
;; (INVALIDGCVALUE) which disables the check for that location. Since the shadow heap is only validated at GC
;; time and these write barrier operations are atomic wrt to GCs this is sufficient to guarantee that the
;; shadow heap contains only valid copies of real heap values or INVALIDGCVALUE.
#ifdef WRITE_BARRIER_CHECK  

    SETALIAS    g_GCShadow, ?g_GCShadow@@3PEAEEA
    SETALIAS    g_GCShadowEnd, ?g_GCShadowEnd@@3PEAEEA
    EXTERN      $g_GCShadow
    EXTERN      $g_GCShadowEnd

INVALIDGCVALUE  EQU 0xCCCCCCCD

    MACRO
        ;; On entry:
        ;;  $destReg: location to be updated
        ;;  $refReg: objectref to be stored
        ;;
        ;; On exit:
        ;;  x9,x10: trashed
        ;;  other registers are preserved
        ;;
        UPDATE_GC_SHADOW $destReg, $refReg

        ;; If g_GCShadow is 0, don't perform the check.
        adrp    x9, $g_GCShadow
        ldr     x9, [x9, $g_GCShadow]
        cbz     x9, %ft1

        ;; Save $destReg since we're about to modify it (and we need the original value both within the macro and
        ;; once we exit the macro).
        mov     x10, $destReg

        ;; Transform $destReg into the equivalent address in the shadow heap.
        adrp    x9, g_lowest_address
        ldr     x9, [x9, g_lowest_address]
        subs    $destReg, $destReg, x9
        blt     %ft0

        adrp    x9, $g_GCShadow
        ldr     x9, [x9, $g_GCShadow]
        add     $destReg, $destReg, x9

        adrp    x9, $g_GCShadowEnd
        ldr     x9, [x9, $g_GCShadowEnd]
        cmp     $destReg, x9
        bgt     %ft0

        ;; Update the shadow heap.
        str     $refReg, [$destReg]

        ;; The following read must be strongly ordered wrt to the write we've just performed in order to
        ;; prevent race conditions.
        dmb     ish

        ;; Now check that the real heap location still contains the value we just wrote into the shadow heap.
        mov     x9, x10
        ldr     x9, [x9]
        cmp     x9, $refReg
        beq     %ft0

        ;; Someone went and updated the real heap. We need to invalidate the shadow location since we can't
        ;; guarantee whose shadow update won.
        MOVL64  x9, INVALIDGCVALUE, 0
        str     x9, [$destReg]

0
        ;; Restore original $destReg value
        mov     $destReg, x10

1
    MEND

#else // WRITE_BARRIER_CHECK

    MACRO
        UPDATE_GC_SHADOW $destReg, $refReg
    MEND

#endif // WRITE_BARRIER_CHECK

;; There are several different helpers used depending on which register holds the object reference. Since all
;; the helpers have identical structure we use a macro to define this structure. Two arguments are taken, the
;; name of the register that points to the location to be updated and the name of the register that holds the
;; object reference (this should be in upper case as it's used in the definition of the name of the helper).

;; Define a sub-macro first that expands to the majority of the barrier implementation. This is used below for
;; some interlocked helpers that need an inline barrier.
    MACRO
        ;; On entry:
        ;;   $destReg: location to be updated
        ;;   $refReg:  objectref to be stored
        ;;
        ;; On exit:
        ;;   $destReg:   trashed
        ;;   x9:         trashed
        ;;
        INSERT_UNCHECKED_WRITE_BARRIER_CORE $destReg, $refReg

        ;; Update the shadow copy of the heap with the same value just written to the same heap. (A no-op unless
        ;; we're in a debug build and write barrier checking has been enabled).
        UPDATE_GC_SHADOW $destReg, $refReg

        ;; We can skip the card table write if the reference is to
        ;; an object not on the epehemeral segment.
        adrp    x9, g_ephemeral_low
        ldr     x9, [x9, g_ephemeral_low]
        cmp     $refReg, x9
        blt     %ft0

        adrp    x9, g_ephemeral_high
        ldr     x9, [x9, g_ephemeral_high]
        cmp     $refReg, x9
        bge     %ft0

        ;; Set this object's card, if it hasn't already been set.
        adrp    x9, g_card_table
        ldr     x9, [x9, g_card_table]
        add     $destReg, x9, $destReg lsr #11

        ;; Check that this card hasn't already been written. Avoiding useless writes is a big win on
        ;; multi-proc systems since it avoids cache thrashing.
        ldrb    w9, [$destReg]
        cmp     x9, 0xFF
        beq     %ft0

        mov     x9, 0xFF
        strb    w9, [$destReg]

0
        ;; Exit label
    MEND

    MACRO
        ;; On entry:
        ;;   $destReg: location to be updated
        ;;   $refReg:  objectref to be stored
        ;;
        ;; On exit:
        ;;   $destReg:   trashed
        ;;   x9:         trashed
        ;;
        INSERT_CHECKED_WRITE_BARRIER_CORE $destReg, $refReg

        ;; The "check" of this checked write barrier - is $destReg
        ;; within the heap? if no, early out.
        adrp    x9, g_lowest_address
        ldr     x9, [x9, g_lowest_address]
        cmp     $destReg, x9
        blt     %ft0

        adrp    x9, g_highest_address
        ldr     x9, [x9, g_highest_address]
        cmp     $destReg, x9
        bgt     %ft0

        INSERT_UNCHECKED_WRITE_BARRIER_CORE $destReg, $refReg

0
        ;; Exit label
    MEND

;; RhpCheckedAssignRef(Object** dst, Object* src)
;;
;; Write barrier for writes to objects that may reside
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
        
        ret

    LEAF_END RhpCheckedAssignRef
 
;; RhpAssignRef(Object** dst, Object* src)
;;
;; Write barrier for writes to objects that are known to
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
 
        ret

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
        ;; Check location value is what we expect.
        ldaxr   x10, [x0]
        cmp     x10, x2
        bne     CmpXchgNoUpdate

        ;; Current value matches comparand, attempt to update with the new value.
        stlxr   w9, x1, [x0]
        cbnz    w9, CmpXchgRetry

        ;; We've successfully updated the value of the objectref so now we need a GC write barrier.
        ;; The following barrier code takes the destination in x0 and the value in x1 so the arguments are
        ;; already correctly set up.

        INSERT_CHECKED_WRITE_BARRIER_CORE x0, x1

CmpXchgNoUpdate
        ;; x10 still contains the original value.
        mov     x0, x10
        ArmInterlockedOperationBarrier
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
        ;; Read the existing memory location.
        ldaxr   x10,  [x0]

        ;; Attempt to update with the new value.
        stlxr   w9, x1, [x0]
        cbnz    w9, ExchangeRetry

        ;; We've successfully updated the value of the objectref so now we need a GC write barrier.
        ;; The following barrier code takes the destination in x0 and the value in x1 so the arguments are
        ;; already correctly set up.

        INSERT_CHECKED_WRITE_BARRIER_CORE x0, x1

        ;; x10 still contains the original value.
        mov     x0, x10
        ArmInterlockedOperationBarrier
        ret

    LEAF_END RhpCheckedXchg

    end
