;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

;;
;; Define the helpers used to implement the write barrier required when writing an object reference into a
;; location residing on the GC heap. Such write barriers allow the GC to optimize which objects in
;; non-ephemeral generations need to be scanned for references to ephemeral objects during an ephemeral
;; collection.
;;

    .xmm
    .model  flat
    option  casemap:none
    .code

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

g_GCShadow      TEXTEQU <?g_GCShadow@@3PAEA>
g_GCShadowEnd   TEXTEQU <?g_GCShadowEnd@@3PAEA>
INVALIDGCVALUE  EQU 0CCCCCCCDh

EXTERN  g_GCShadow : DWORD
EXTERN  g_GCShadowEnd : DWORD

UPDATE_GC_SHADOW macro BASENAME, DESTREG, REFREG

    ;; If g_GCShadow is 0, don't perform the check.
    cmp     g_GCShadow, 0
    je      &BASENAME&_UpdateShadowHeap_Done_&DESTREG&_&REFREG&

    ;; Save DESTREG since we're about to modify it (and we need the original value both within the macro and
    ;; once we exit the macro).
    push    DESTREG

    ;; Transform DESTREG into the equivalent address in the shadow heap.
    sub     DESTREG, G_LOWEST_ADDRESS
    jb      &BASENAME&_UpdateShadowHeap_PopThenDone_&DESTREG&_&REFREG&
    add     DESTREG, [g_GCShadow]
    cmp     DESTREG, [g_GCShadowEnd]
    ja      &BASENAME&_UpdateShadowHeap_PopThenDone_&DESTREG&_&REFREG&

    ;; Update the shadow heap.
    mov     [DESTREG], REFREG

    ;; Now check that the real heap location still contains the value we just wrote into the shadow heap. This
    ;; read must be strongly ordered wrt to the previous write to prevent race conditions. We also need to
    ;; recover the old value of DESTREG for the comparison so use an xchg instruction (which has an implicit lock
    ;; prefix).
    xchg    [esp], DESTREG
    cmp     [DESTREG], REFREG
    jne     &BASENAME&_UpdateShadowHeap_Invalidate_&DESTREG&_&REFREG&

    ;; The original DESTREG value is now restored but the stack has a value (the shadow version of the
    ;; location) pushed. Need to discard this push before we are done.
    add     esp, 4
    jmp     &BASENAME&_UpdateShadowHeap_Done_&DESTREG&_&REFREG&

&BASENAME&_UpdateShadowHeap_Invalidate_&DESTREG&_&REFREG&:
    ;; Someone went and updated the real heap. We need to invalidate the shadow location since we can't
    ;; guarantee whose shadow update won.

    ;; Retrieve shadow location from the stack and restore original DESTREG to the stack. This is an
    ;; additional memory barrier we don't require but it's on the rare path and x86 doesn't have an xchg
    ;; variant that doesn't implicitly specify the lock prefix.
    xchg    [esp], DESTREG
    mov     dword ptr [DESTREG], INVALIDGCVALUE

&BASENAME&_UpdateShadowHeap_PopThenDone_&DESTREG&_&REFREG&:
    ;; Restore original DESTREG value from the stack.
    pop     DESTREG

&BASENAME&_UpdateShadowHeap_Done_&DESTREG&_&REFREG&:
endm

else ; WRITE_BARRIER_CHECK

UPDATE_GC_SHADOW macro BASENAME, DESTREG, REFREG
endm

endif ; WRITE_BARRIER_CHECK

;; There are several different helpers used depending on which register holds the object reference. Since all
;; the helpers have identical structure we use a macro to define this structure. Two arguments are taken, the
;; name of the register that points to the location to be updated and the name of the register that holds the
;; object reference (this should be in upper case as it's used in the definition of the name of the helper).
DEFINE_WRITE_BARRIER macro DESTREG, REFREG

;; Define a helper with a name of the form RhpAssignRefEAX etc. (along with suitable calling standard
;; decoration). The location to be updated is in DESTREG. The object reference that will be assigned into that
;; location is in one of the other general registers determined by the value of REFREG.
FASTCALL_FUNC RhpAssignRef&REFREG&, 0

    ;; Export the canonical write barrier under unqualified name as well
    ifidni <REFREG>, <EDX>
    ALTERNATE_ENTRY @RhpAssignRef@0
    ALTERNATE_ENTRY RhpAssignRefAVLocation
    endif

    ;; Write the reference into the location. Note that we rely on the fact that no GC can occur between here
    ;; and the card table update we may perform below.
    mov     dword ptr [DESTREG], REFREG

    ;; Update the shadow copy of the heap with the same value (if enabled).
    UPDATE_GC_SHADOW RhpAssignRef, DESTREG, REFREG

    ;; If the reference is to an object that's not in an ephemeral generation we have no need to track it
    ;; (since the object won't be collected or moved by an ephemeral collection).
    cmp     REFREG, [G_EPHEMERAL_LOW]
    jb      WriteBarrier_NoBarrierRequired_&DESTREG&_&REFREG&
    cmp     REFREG, [G_EPHEMERAL_HIGH]
    jae     WriteBarrier_NoBarrierRequired_&DESTREG&_&REFREG&

    ;; We have a location on the GC heap being updated with a reference to an ephemeral object so we must
    ;; track this write. The location address is translated into an offset in the card table bitmap. We set
    ;; an entire byte in the card table since it's quicker than messing around with bitmasks and we only write
    ;; the byte if it hasn't already been done since writes are expensive and impact scaling.
    shr     DESTREG, 10
    add     DESTREG, [G_CARD_TABLE]
    cmp     byte ptr [DESTREG], 0FFh
    jne     WriteBarrier_UpdateCardTable_&DESTREG&_&REFREG&

WriteBarrier_NoBarrierRequired_&DESTREG&_&REFREG&:
    ret

;; We get here if it's necessary to update the card table.
WriteBarrier_UpdateCardTable_&DESTREG&_&REFREG&:
    mov     byte ptr [DESTREG], 0FFh
    ret
FASTCALL_ENDFUNC
endm

RET4    macro
    ret     4
endm

DEFINE_CHECKED_WRITE_BARRIER_CORE macro BASENAME, DESTREG, REFREG, RETINST

    ;; The location being updated might not even lie in the GC heap (a handle or stack location for instance),
    ;; in which case no write barrier is required.
    cmp     DESTREG, [G_LOWEST_ADDRESS]
    jb      &BASENAME&_NoBarrierRequired_&DESTREG&_&REFREG&
    cmp     DESTREG, [G_HIGHEST_ADDRESS]
    jae     &BASENAME&_NoBarrierRequired_&DESTREG&_&REFREG&

    ;; Update the shadow copy of the heap with the same value just written to the same heap. (A no-op unless
    ;; we're in a debug build and write barrier checking has been enabled).
    UPDATE_GC_SHADOW BASENAME, DESTREG, REFREG

    ;; If the reference is to an object that's not in an ephemeral generation we have no need to track it
    ;; (since the object won't be collected or moved by an ephemeral collection).
    cmp     REFREG, [G_EPHEMERAL_LOW]
    jb      &BASENAME&_NoBarrierRequired_&DESTREG&_&REFREG&
    cmp     REFREG, [G_EPHEMERAL_HIGH]
    jae     &BASENAME&_NoBarrierRequired_&DESTREG&_&REFREG&

    ;; We have a location on the GC heap being updated with a reference to an ephemeral object so we must
    ;; track this write. The location address is translated into an offset in the card table bitmap. We set
    ;; an entire byte in the card table since it's quicker than messing around with bitmasks and we only write
    ;; the byte if it hasn't already been done since writes are expensive and impact scaling.
    shr     DESTREG, 10
    add     DESTREG, [G_CARD_TABLE]
    cmp     byte ptr [DESTREG], 0FFh
    jne     &BASENAME&_UpdateCardTable_&DESTREG&_&REFREG&

&BASENAME&_NoBarrierRequired_&DESTREG&_&REFREG&:
    RETINST

;; We get here if it's necessary to update the card table.
&BASENAME&_UpdateCardTable_&DESTREG&_&REFREG&:
    mov     byte ptr [DESTREG], 0FFh
    RETINST

endm


;; This macro is very much like the one above except that it generates a variant of the function which also
;; checks whether the destination is actually somewhere within the GC heap.
DEFINE_CHECKED_WRITE_BARRIER macro DESTREG, REFREG

;; Define a helper with a name of the form RhpCheckedAssignRefEAX etc. (along with suitable calling standard
;; decoration). The location to be updated is in DESTREG. The object reference that will be assigned into
;; that location is in one of the other general registers determined by the value of REFREG.

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen on the first instruction
;; - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
FASTCALL_FUNC RhpCheckedAssignRef&REFREG&, 0

    ;; Export the canonical write barrier under unqualified name as well
    ifidni <REFREG>, <EDX>
    ALTERNATE_ENTRY @RhpCheckedAssignRef@0
    ALTERNATE_ENTRY RhpCheckedAssignRefAVLocation
    endif

    ;; Write the reference into the location. Note that we rely on the fact that no GC can occur between here
    ;; and the card table update we may perform below.
    mov     dword ptr [DESTREG], REFREG

    DEFINE_CHECKED_WRITE_BARRIER_CORE RhpCheckedAssignRef, DESTREG, REFREG, ret

FASTCALL_ENDFUNC

endm

;; One day we might have write barriers for all the possible argument registers but for now we have
;; just one write barrier that assumes the input register is EDX.
DEFINE_CHECKED_WRITE_BARRIER ECX, EDX
DEFINE_WRITE_BARRIER ECX, EDX

;; Need some more write barriers to run CLR compiled MDIL on Redhawk - commented out for now
;; DEFINE_WRITE_BARRIER EDX, EAX
;; DEFINE_WRITE_BARRIER EDX, ECX
;; DEFINE_WRITE_BARRIER EDX, EBX
;; DEFINE_WRITE_BARRIER EDX, ESI
;; DEFINE_WRITE_BARRIER EDX, EDI
;; DEFINE_WRITE_BARRIER EDX, EBP

;; DEFINE_CHECKED_WRITE_BARRIER EDX, EAX
;; DEFINE_CHECKED_WRITE_BARRIER EDX, ECX
;; DEFINE_CHECKED_WRITE_BARRIER EDX, EBX
;; DEFINE_CHECKED_WRITE_BARRIER EDX, ESI
;; DEFINE_CHECKED_WRITE_BARRIER EDX, EDI
;; DEFINE_CHECKED_WRITE_BARRIER EDX, EBP

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at @RhpCheckedLockCmpXchgAVLocation@0
;; - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
;; pass third argument in EAX
FASTCALL_FUNC RhpCheckedLockCmpXchg
ALTERNATE_ENTRY RhpCheckedLockCmpXchgAVLocation
    lock cmpxchg    [ecx], edx
    jne              RhpCheckedLockCmpXchg_NoBarrierRequired_ECX_EDX

    DEFINE_CHECKED_WRITE_BARRIER_CORE RhpCheckedLockCmpXchg, ECX, EDX, ret

FASTCALL_ENDFUNC

;; WARNING: Code in EHHelpers.cpp makes assumptions about write barrier code, in particular:
;; - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at @RhpCheckedXchgAVLocation@0
;; - Function "UnwindWriteBarrierToCaller" assumes the stack contains just the pushed return address
FASTCALL_FUNC RhpCheckedXchg, 0

    ;; Setup eax with the new object for the exchange, that way it will automatically hold the correct result
    ;; afterwards and we can leave edx unaltered ready for the GC write barrier below.
    mov             eax, edx
ALTERNATE_ENTRY RhpCheckedXchgAVLocation
    xchg            [ecx], eax

    DEFINE_CHECKED_WRITE_BARRIER_CORE RhpCheckedXchg, ECX, EDX, ret

FASTCALL_ENDFUNC

;;
;; Write barrier used when a large number of bytes possibly containing GC references have been updated. For
;; speed we don't try to determine GC series information for the value or array of values. Instead we just
;; mark all the cards covered by the memory range given to us. Additionally, at least for now, we don't try to
;; mark card bits individually, which incurs the cost of an interlocked operation. Instead, like the single
;; write barrier case, we mark 8 cards at a time by writing byte values of 0xff.
;;
;; On entry:
;;      ecx : Start of memory region that was written
;;      edx : Length of memory region written
;;
;; On exit:
;;      ecx/edx : Trashed
;;
FASTCALL_FUNC RhpBulkWriteBarrier, 8

    ;; For the following range checks we assume it is sufficient to test just the start address. No valid
    ;; write region should span a GC heap or generation boundary.

    ;; Check whether the writes were even into the heap. If not there's no card update required.
    cmp     ecx, [G_LOWEST_ADDRESS]
    jb      NoBarrierRequired
    cmp     ecx, [G_HIGHEST_ADDRESS]
    jae     NoBarrierRequired

    ;; If the size is smaller than a pointer, no write barrier is required
    ;; This case can occur with universal shared generic code where the size
    ;; is not known at compile time
    cmp     edx, 4
    jb      NoBarrierRequired

ifdef WRITE_BARRIER_CHECK  

    ;; Perform shadow heap updates corresponding to the gc heap updates that immediately preceded this helper
    ;; call. See the comment for UPDATE_GC_SHADOW above for a more detailed explanation of why we do this and
    ;; the synchronization implications.

    ;; If g_GCShadow is 0, don't perform the check.
    cmp     g_GCShadow, 0
    je      BulkWriteBarrier_UpdateShadowHeap_Done

    ;; We need some scratch registers and to preserve eax\ecx.
    push    eax
    push    ebx
    push    esi
    push    ecx

    ;; Compute the shadow heap address corresponding to the beginning of the range of heap addresses modified
    ;; and in the process range check it to make sure we have the shadow version allocated.
    mov     ebx, ecx
    sub     ebx, G_LOWEST_ADDRESS
    jb      BulkWriteBarrier_UpdateShadowHeap_PopThenDone
    add     ebx, [g_GCShadow]
    cmp     ebx, [g_GCShadowEnd]
    ja      BulkWriteBarrier_UpdateShadowHeap_PopThenDone

    ;; Initialize esi to the length of data to copy.
    mov     esi, edx

    ;; Iterate over every pointer sized slot in the range, copying data from the real heap to the shadow heap.
    ;; As we perform each copy we need to recheck the real heap contents with an ordered read to ensure we're
    ;; not racing with another heap updater. If we discover a race we invalidate the corresponding shadow heap
    ;; slot using a special well-known value so that this location will not be tested during the next shadow
    ;; heap validation.
BulkWriteBarrier_UpdateShadowHeap_CopyLoop:
    ;; Decrement the copy count.
    sub     esi, 4
    jb      BulkWriteBarrier_UpdateShadowHeap_PopThenDone

    ;; Ecx == current real heap slot
    ;; Ebx == current shadow heap slot

    ;; Update shadow slot from real slot.
    mov     eax, [ecx]
    mov     [ebx], eax

    ;; Memory barrier to ensure the next read is ordered wrt to the shadow heap write we just made.
    mfence

    ;; Read the real slot contents again. If they don't agree with what we just wrote then someone just raced
    ;; with us and updated the heap again. In such cases we invalidate the shadow slot.
    cmp     [ecx], eax
    jne     BulkWriteBarrier_UpdateShadowHeap_LostRace

BulkWriteBarrier_UpdateShadowHeap_NextIteration:
    ;; Advance the heap pointers and loop again.
    add     ecx, 4
    add     ebx, 4
    jmp     BulkWriteBarrier_UpdateShadowHeap_CopyLoop

BulkWriteBarrier_UpdateShadowHeap_LostRace:
    mov     dword ptr [ebx], INVALIDGCVALUE
    jmp     BulkWriteBarrier_UpdateShadowHeap_NextIteration

BulkWriteBarrier_UpdateShadowHeap_PopThenDone:
    pop     ecx
    pop     esi
    pop     ebx
    pop     eax

BulkWriteBarrier_UpdateShadowHeap_Done:

endif ; WRITE_BARRIER_CHECK

    ;; Compute the starting card address and the number of bytes to write (groups of 8 cards). We could try
    ;; for further optimization here using aligned 32-bit writes but there's some overhead in setup required
    ;; and additional complexity. It's not clear this is warranted given that a single byte of card table
    ;; update already covers 1K of object space (2K on 64-bit platforms). It's also not worth probing that
    ;; 1K/2K range to see if any of the pointers appear to be non-ephemeral GC references. Given the size of
    ;; the area the chances are high that at least one interesting GC refenence is present.

    add     edx, ecx                ; edx <- end address
    shr     ecx, LOG2_CLUMP_SIZE    ; ecx <- starting clump
    add     edx, CLUMP_SIZE-1       ; edx <- end address + round up
    shr     edx, LOG2_CLUMP_SIZE    ; edx <- ending clump index (rounded up)

    ;; calculate the number of clumps to mark (round_up(end) - start)
    sub     edx, ecx

    ;; Starting card address.
    add     ecx, [G_CARD_TABLE]

    ; ecx: pointer to starting byte in card table
    ; edx: number of bytes to set

    ;; Fill the cards. To avoid cache line thrashing we check whether the cards have already been set before
    ;; writing.
CardUpdateLoop:
    cmp     byte ptr [ecx], 0FFh
    jz      SkipCardUpdate

    mov     byte ptr [ecx], 0FFh

SkipCardUpdate:
    inc     ecx
    dec     edx
    jnz     CardUpdateLoop

NoBarrierRequired:
    ret

FASTCALL_ENDFUNC

    end
