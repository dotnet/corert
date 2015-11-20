;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

include AsmMacros.inc

;; Macro used to copy contents of newly updated GC heap locations to a shadow copy of the heap. This is used
;; during garbage collections to verify that object references where never written to the heap without using a
;; write barrier. Note that we're potentially racing to update the shadow heap while other threads are writing
;; new references to the real heap. Since this can't be solved perfectly without critical sections around the
;; entire update process, we instead update the shadow location and then re-check the real location (as two
;; ordered operations) and if there is a disparity we'll re-write the shadow location with a special value
;; (INVALIDGCVALUE) which disables the check for that location. Since the shadow heap is only validated at GC
;; time and these write barrier operations are atomic wrt to GCs this is sufficient to guarantee that the
;; shadow heap contains only valid copies of real heap values or INVALIDGCVALUE.
ifdef WRITE_BARRIER_CHECK

g_GCShadow      TEXTEQU <?g_GCShadow@@3PEAEEA>
g_GCShadowEnd   TEXTEQU <?g_GCShadowEnd@@3PEAEEA>
INVALIDGCVALUE  EQU 0CCCCCCCDh

EXTERN  g_GCShadow : QWORD
EXTERN  g_GCShadowEnd : QWORD

UPDATE_GC_SHADOW macro BASENAME, REFREG, DESTREG

    ;; If g_GCShadow is 0, don't perform the check.
    cmp     g_GCShadow, 0
    je      &BASENAME&_UpdateShadowHeap_Done_&REFREG&

    ;; Save DESTREG since we're about to modify it (and we need the original value both within the macro and
    ;; once we exit the macro). Note that this is naughty since we're altering the stack pointer outside of
    ;; the prolog inside a method without a frame. But given that this is only debug code and generally we
    ;; shouldn't be walking the stack at this point it seems preferable to recoding the all the barrier
    ;; variants to set up frames. Unlike RhpBulkWriteBarrier below which is treated as a helper call using the
    ;; usual calling convention, the compiler knows exactly which registers are trashed in the simple write
    ;; barrier case, so we don't have any more scratch registers to play with (and doing so would only make
    ;; things harder if at a later stage we want to allow multiple barrier versions based on the input
    ;; registers).
    push    DESTREG

    ;; Transform DESTREG into the equivalent address in the shadow heap.
    sub     DESTREG, G_LOWEST_ADDRESS
    jb      &BASENAME&_UpdateShadowHeap_PopThenDone_&REFREG&
    add     DESTREG, [g_GCShadow]
    cmp     DESTREG, [g_GCShadowEnd]
    ja      &BASENAME&_UpdateShadowHeap_PopThenDone_&REFREG&

    ;; Update the shadow heap.
    mov     [DESTREG], REFREG

    ;; Now check that the real heap location still contains the value we just wrote into the shadow heap. This
    ;; read must be strongly ordered wrt to the previous write to prevent race conditions. We also need to
    ;; recover the old value of DESTREG for the comparison so use an xchg instruction (which has an implicit lock
    ;; prefix).
    xchg    [rsp], DESTREG
    cmp     [DESTREG], REFREG
    jne     &BASENAME&_UpdateShadowHeap_Invalidate_&REFREG&

    ;; The original DESTREG value is now restored but the stack has a value (the shadow version of the
    ;; location) pushed. Need to discard this push before we are done.
    add     rsp, 8
    jmp     &BASENAME&_UpdateShadowHeap_Done_&REFREG&

&BASENAME&_UpdateShadowHeap_Invalidate_&REFREG&:
    ;; Someone went and updated the real heap. We need to invalidate the shadow location since we can't
    ;; guarantee whose shadow update won.

    ;; Retrieve shadow location from the stack and restore original DESTREG to the stack. This is an
    ;; additional memory barrier we don't require but it's on the rare path and x86 doesn't have an xchg
    ;; variant that doesn't implicitly specify the lock prefix.
    xchg    [rsp], DESTREG
    mov     qword ptr [DESTREG], INVALIDGCVALUE

&BASENAME&_UpdateShadowHeap_PopThenDone_&REFREG&:
    ;; Restore original DESTREG value from the stack.
    pop     DESTREG

&BASENAME&_UpdateShadowHeap_Done_&REFREG&:
endm

else ; WRITE_BARRIER_CHECK

UPDATE_GC_SHADOW macro BASENAME, REFREG, DESTREG
endm

endif ; WRITE_BARRIER_CHECK

;; There are several different helpers used depending on which register holds the object reference. Since all
;; the helpers have identical structure we use a macro to define this structure. Two arguments are taken, the
;; name of the register that points to the location to be updated and the name of the register that holds the
;; object reference (this should be in upper case as it's used in the definition of the name of the helper).
DEFINE_UNCHECKED_WRITE_BARRIER_CORE macro BASENAME, REFREG

    ;; Update the shadow copy of the heap with the same value just written to the same heap. (A no-op unless
    ;; we're in a debug build and write barrier checking has been enabled).
    UPDATE_GC_SHADOW BASENAME, REFREG, rcx

    ;; If the reference is to an object that's not in an ephemeral generation we have no need to track it
    ;; (since the object won't be collected or moved by an ephemeral collection).
    cmp     REFREG, [g_ephemeral_low]
    jb      &BASENAME&_NoBarrierRequired_&REFREG&
    cmp     REFREG, [g_ephemeral_high]
    jae     &BASENAME&_NoBarrierRequired_&REFREG&

    ;; We have a location on the GC heap being updated with a reference to an ephemeral object so we must
    ;; track this write. The location address is translated into an offset in the card table bitmap. We set
    ;; an entire byte in the card table since it's quicker than messing around with bitmasks and we only write
    ;; the byte if it hasn't already been done since writes are expensive and impact scaling.
    shr     rcx, 11
    add     rcx, [g_card_table]
    cmp     byte ptr [rcx], 0FFh
    jne     &BASENAME&_UpdateCardTable_&REFREG&

&BASENAME&_NoBarrierRequired_&REFREG&:
    ret

;; We get here if it's necessary to update the card table.
&BASENAME&_UpdateCardTable_&REFREG&:
    mov     byte ptr [rcx], 0FFh
    ret

endm

;; There are several different helpers used depending on which register holds the object reference. Since all
;; the helpers have identical structure we use a macro to define this structure. One argument is taken, the
;; name of the register that will hold the object reference (this should be in upper case as it's used in the
;; definition of the name of the helper).
DEFINE_UNCHECKED_WRITE_BARRIER macro REFREG, EXPORT_REG_NAME

;; Define a helper with a name of the form RhpAssignRefEAX etc. (along with suitable calling standard
;; decoration). The location to be updated is in DESTREG. The object reference that will be assigned into that
;; location is in one of the other general registers determined by the value of REFREG.

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen on the first instruction
;; - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
LEAF_ENTRY RhpAssignRef&EXPORT_REG_NAME&, _TEXT

    ;; Export the canonical write barrier under unqualified name as well
    ifidni <REFREG>, <RDX>
    ALTERNATE_ENTRY RhpAssignRef
    ALTERNATE_ENTRY RhpAssignRefAVLocation
    endif

    ;; Write the reference into the location. Note that we rely on the fact that no GC can occur between here
    ;; and the card table update we may perform below.
    mov     qword ptr [rcx], REFREG

    DEFINE_UNCHECKED_WRITE_BARRIER_CORE RhpAssignRef, REFREG

LEAF_END RhpAssignRef&EXPORT_REG_NAME&, _TEXT
endm

;; One day we might have write barriers for all the possible argument registers but for now we have
;; just one write barrier that assumes the input register is RDX.
DEFINE_UNCHECKED_WRITE_BARRIER RDX, EDX

;;
;; Define the helpers used to implement the write barrier required when writing an object reference into a
;; location residing on the GC heap. Such write barriers allow the GC to optimize which objects in
;; non-ephemeral generations need to be scanned for references to ephemeral objects during an ephemeral
;; collection.
;;

DEFINE_CHECKED_WRITE_BARRIER_CORE macro BASENAME, REFREG

    ;; The location being updated might not even lie in the GC heap (a handle or stack location for instance),
    ;; in which case no write barrier is required.
    cmp     rcx, [g_lowest_address]
    jb      &BASENAME&_NoBarrierRequired_&REFREG&
    cmp     rcx, [g_highest_address]
    jae     &BASENAME&_NoBarrierRequired_&REFREG&

    DEFINE_UNCHECKED_WRITE_BARRIER_CORE BASENAME, REFREG

endm

;; There are several different helpers used depending on which register holds the object reference. Since all
;; the helpers have identical structure we use a macro to define this structure. One argument is taken, the
;; name of the register that will hold the object reference (this should be in upper case as it's used in the
;; definition of the name of the helper).
DEFINE_CHECKED_WRITE_BARRIER macro REFREG, EXPORT_REG_NAME

;; Define a helper with a name of the form RhpCheckedAssignRefEAX etc. (along with suitable calling standard
;; decoration). The location to be updated is always in RCX. The object reference that will be assigned into
;; that location is in one of the other general registers determined by the value of REFREG.

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen on the first instruction
;; - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
LEAF_ENTRY RhpCheckedAssignRef&EXPORT_REG_NAME&, _TEXT

    ;; Export the canonical write barrier under unqualified name as well
    ifidni <REFREG>, <RDX>
    ALTERNATE_ENTRY RhpCheckedAssignRef
    ALTERNATE_ENTRY RhpCheckedAssignRefAVLocation
    endif

    ;; Write the reference into the location. Note that we rely on the fact that no GC can occur between here
    ;; and the card table update we may perform below.
    mov     qword ptr [rcx], REFREG

    DEFINE_CHECKED_WRITE_BARRIER_CORE RhpCheckedAssignRef, REFREG

LEAF_END RhpCheckedAssignRef&EXPORT_REG_NAME&, _TEXT
endm

;; One day we might have write barriers for all the possible argument registers but for now we have
;; just one write barrier that assumes the input register is RDX.
DEFINE_CHECKED_WRITE_BARRIER RDX, EDX

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpCheckedLockCmpXchgAVLocation
;; - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
LEAF_ENTRY RhpCheckedLockCmpXchg, _TEXT
    mov             rax, r8
ALTERNATE_ENTRY RhpCheckedLockCmpXchgAVLocation
    lock cmpxchg    [rcx], rdx
    jne             RhpCheckedLockCmpXchg_NoBarrierRequired_RDX

    DEFINE_CHECKED_WRITE_BARRIER_CORE RhpCheckedLockCmpXchg, RDX

LEAF_END RhpCheckedLockCmpXchg, _TEXT

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpCheckedXchgAVLocation
;; - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
LEAF_ENTRY RhpCheckedXchg, _TEXT
    
    ;; Setup rax with the new object for the exchange, that way it will automatically hold the correct result
    ;; afterwards and we can leave rdx unaltered ready for the GC write barrier below.
    mov             rax, rdx
ALTERNATE_ENTRY RhpCheckedXchgAVLocation
    xchg            [rcx], rax

    DEFINE_CHECKED_WRITE_BARRIER_CORE RhpCheckedXchg, RDX

LEAF_END RhpCheckedXchg, _TEXT


;;
;; Write barrier used when a large number of bytes possibly containing GC references have been updated. For
;; speed we don't try to determine GC series information for the value or array of values. Instead we just
;; mark all the cards covered by the memory range given to us. Additionally, at least for now, we don't try to
;; mark card bits individually, which incurs the cost of an interlocked operation. Instead, like the single
;; write barrier case, we mark 8 cards at a time by writing byte values of 0xff.
;;
;; On entry:
;;      rcx : Start of memory region that was written
;;      rdx : Length of memory region written
;;
;; On exit:
;;      rcx/rdx : Trashed
;;
LEAF_ENTRY RhpBulkWriteBarrier, _TEXT

    ;; For the following range checks we assume it is sufficient to test just the start address. No valid
    ;; write region should span a GC heap or generation boundary.

    ;; Check whether the writes were even into the heap. If not there's no card update required.
    cmp     rcx, [G_LOWEST_ADDRESS]
    jb      NoBarrierRequired
    cmp     rcx, [G_HIGHEST_ADDRESS]
    jae     NoBarrierRequired

    ;; If the size is smaller than a pointer, no write barrier is required
    ;; This case can occur with universal shared generic code where the size
    ;; is not known at compile time
    cmp     rdx, 8
    jb      NoBarrierRequired

ifdef WRITE_BARRIER_CHECK
  
    ;; Perform shadow heap updates corresponding to the gc heap updates that immediately preceded this helper
    ;; call. See the comment for UPDATE_GC_SHADOW above for a more detailed explanation of why we do this and
    ;; the synchronization implications.

    ;; If g_GCShadow is 0, don't perform the check.
    cmp     g_GCShadow, 0
    je      BulkWriteBarrier_UpdateShadowHeap_Skipped

    ;; Save our scratch registers since the compiler doesn't expect them to be modified (and this is just
    ;; debug code). Not strictly legal outside of the prolog but we don't expect to generate any exceptions,
    ;; call methods or otherwise crawl the stack before we pop them again.
    push    rax
    push    r8
    push    r9
    push    r10

    ;; Take a copy of rcx in r8 since we're going to modify the pointer but still need the original value for
    ;; the code after the shadow heap update.
    mov     r8, rcx

    ;; Compute the shadow heap address corresponding to the beginning of the range of heap addresses modified
    ;; and in the process range check it to make sure we have the shadow version allocated.
    mov     r9, rcx
    sub     r9, G_LOWEST_ADDRESS
    jb      BulkWriteBarrier_UpdateShadowHeap_Done
    add     r9, [g_GCShadow]
    cmp     r9, [g_GCShadowEnd]
    ja      BulkWriteBarrier_UpdateShadowHeap_Done

    ;; Initialize r10 to the length of data to copy.
    mov     r10, rdx

    ;; Iterate over every pointer sized slot in the range, copying data from the real heap to the shadow heap.
    ;; As we perform each copy we need to recheck the real heap contents with an ordered read to ensure we're
    ;; not racing with another heap updater. If we discover a race we invalidate the corresponding shadow heap
    ;; slot using a special well-known value so that this location will not be tested during the next shadow
    ;; heap validation.
BulkWriteBarrier_UpdateShadowHeap_CopyLoop:
    ;; Decrement the copy count.
    sub     r10, 8
    jb      BulkWriteBarrier_UpdateShadowHeap_Done

    ;; R8 == current real heap slot
    ;; R9 == current shadow heap slot

    ;; Update shadow slot from real slot.
    mov     rax, [r8]
    mov     [r9], rax

    ;; Memory barrier to ensure the next read is ordered wrt to the shadow heap write we just made.
    mfence

    ;; Read the real slot contents again. If they don't agree with what we just wrote then someone just raced
    ;; with us and updated the heap again. In such cases we invalidate the shadow slot.
    cmp     [r8], rax
    jne     BulkWriteBarrier_UpdateShadowHeap_LostRace

BulkWriteBarrier_UpdateShadowHeap_NextIteration:
    ;; Advance the heap pointers and loop again.
    add     r8, 8
    add     r9, 8
    jmp     BulkWriteBarrier_UpdateShadowHeap_CopyLoop

BulkWriteBarrier_UpdateShadowHeap_LostRace:
    mov     qword ptr [r9], INVALIDGCVALUE
    jmp     BulkWriteBarrier_UpdateShadowHeap_NextIteration

BulkWriteBarrier_UpdateShadowHeap_Done:
    ;; Restore our saved scratch registers.
    pop     r10
    pop     r9
    pop     r8
    pop     rax

BulkWriteBarrier_UpdateShadowHeap_Skipped:

endif ; WRITE_BARRIER_CHECK

    ;; Compute the starting card address and the number of bytes to write (groups of 8 cards). We could try
    ;; for further optimization here using aligned 32-bit writes but there's some overhead in setup required
    ;; and additional complexity. It's not clear this is warranted given that a single byte of card table
    ;; update already covers 1K of object space (2K on 64-bit platforms). It's also not worth probing that
    ;; 1K/2K range to see if any of the pointers appear to be non-ephemeral GC references. Given the size of
    ;; the area the chances are high that at least one interesting GC refenence is present.

    add     rdx, rcx                ; rdx <- end address
    shr     rcx, LOG2_CLUMP_SIZE    ; rcx <- starting clump
    add     rdx, CLUMP_SIZE-1       ; rdx <- end address + round up 
    shr     rdx, LOG2_CLUMP_SIZE    ; rdx <- ending clump index (rounded up)

    ;; calculate the number of clumps to mark (round_up(end) - start)
    sub     rdx, rcx

    ;; Starting card address.
    add     rcx, [G_CARD_TABLE]

    ; rcx: pointer to starting byte in card table
    ; rdx: number of bytes to set

    ;; Fill the cards. To avoid cache line thrashing we check whether the cards have already been set before
    ;; writing.
CardUpdateLoop:
    cmp     byte ptr [rcx], 0FFh
    jz      SkipCardUpdate

    mov     byte ptr [rcx], 0FFh

SkipCardUpdate:
    inc     rcx
    dec     rdx
    jnz     CardUpdateLoop

NoBarrierRequired:
    ret

LEAF_END RhpBulkWriteBarrier, _TEXT

;;
;; RhpByRefAssignRef simulates movs instruction for object references.
;;
;; On entry:
;;      rdi: address of ref-field (assigned to)
;;      rsi: address of the data (source)
;;      rcx: be trashed
;;
;; On exit:
;;      rdi, rsi are incremented by 8, 
;;      rcx: trashed
;;
LEAF_ENTRY RhpByRefAssignRef, _TEXT
    mov     rcx, [rsi]
    mov     [rdi], rcx

    ;; Update the shadow copy of the heap with the same value just written to the same heap. (A no-op unless
    ;; we're in a debug build and write barrier checking has been enabled).
    UPDATE_GC_SHADOW BASENAME, rcx, rdi

    ;; If the reference is to an object that's not in an ephemeral generation we have no need to track it
    ;; (since the object won't be collected or moved by an ephemeral collection).
    cmp     rcx, [g_ephemeral_low]
    jb      RhpByRefAssignRef_NotInHeap
    cmp     rcx, [g_ephemeral_high]
    jae     RhpByRefAssignRef_NotInHeap

    ;; move current rdi value into rcx and then increment the pointers
    mov     rcx, rdi
    add     rsi, 8h
    add     rdi, 8h

    ;; We have a location on the GC heap being updated with a reference to an ephemeral object so we must
    ;; track this write. The location address is translated into an offset in the card table bitmap. We set
    ;; an entire byte in the card table since it's quicker than messing around with bitmasks and we only write
    ;; the byte if it hasn't already been done since writes are expensive and impact scaling.
    shr     rcx, 11
    add     rcx, [g_card_table]
    cmp     byte ptr [rcx], 0FFh
    jne     RhpByRefAssignRef_UpdateCardTable
    ret

;; We get here if it's necessary to update the card table.
RhpByRefAssignRef_UpdateCardTable:
    mov     byte ptr [rcx], 0FFh
    ret

RhpByRefAssignRef_NotInHeap:
    ; Increment the pointers before leaving
    add     rdi, 8h
    add     rsi, 8h
    ret
LEAF_END RhpByRefAssignRef, _TEXT

    end
