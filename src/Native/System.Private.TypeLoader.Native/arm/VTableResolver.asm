;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

#include "kxarm.h"

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;  DATA SECTIONS  ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
        DATAAREA

g_vtableResolveCallback DCD 0  ; The vtableresolve method
    EXPORT g_vtableResolveCallback
g_universalTransition DCD 0  ; The address of Redhawk's UniversalTransition thunk

#define VTableThunkSize 0x20
;; TODO - do something similar to Redhawk's asmoffsets to compute the value at compile time
#define EETypeVTableOffset 0x14
#define PointerSize 4
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; ConstrainedCall Support Helpers ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

        TEXTAREA
;;
;; Note: The "__jmpstub__" prefix is used to indicate to debugger
;; that it must step-through this stub when it encounters it while
;; stepping.
;;

;; Returns the size of the pre-generated thunks
;; int VTableResolver_Init(IntPtr *__jmpstub__VTableResolverSlot0,
;;                          IntPtr vtableResolveCallback,
;;                          IntPtr universalTransition,
;;                          int *slotCount)
;;
    LEAF_ENTRY VTableResolver_Init
        ldr     r12, __jmpstub__VTableSlot000
        add     r12, r12, 1 ; Add thumb bit
        str     r12, [r0]
        ldr r12, =g_vtableResolveCallback
        str     r1, [r12]
        ldr r12, =g_universalTransition
        str     r2, [r12]
        mov     r12, 100 ; This file defines 100 slot helpers
        str     r12, [r3]
        mov     r0, VTableThunkSize ; Each thunk is VTableThunkSize in bytes
        bx      lr
    LEAF_END VTableResolver_Init

;; void* VTableResolver_GetCommonCallingStub()
;;  - Get the address of the common calling stub
    LEAF_ENTRY VTableResolver_GetCommonCallingStub
        ldr     r0, __jmpstub__VTableResolver_CommonCallingStub
        bx      lr
    LEAF_END VTableResolver_GetCommonCallingStub

;;
;; __jmpstub__VTableResolver_CommonCallingStub(?)
;;  Used when we dynamically need a VTableResolver not pre-generated
;;
;; sp-4 contains a pointer to a VTableResolverStruct
;;   struct VTableResolverStruct
;;   {
;;       int offsetFromStartOfEETypePtr;
;;       IntPtr VTableThunkAddress;
;;   };
;;
    LEAF_ENTRY __jmpstub__VTableResolver_CommonCallingStub
        ;; Custom calling convention:
        ;;      red zone has pointer to the VTableResolverStruct
        ;;      Copy red zone value into r12 so that the PROLOG_PUSH doesn't destroy it
        PROLOG_NOP  ldr r12, [sp, #-4]
        PROLOG_PUSH {r3}
        PROLOG_PUSH {r1-r2}
        ldr r2, [r0]
        ;; r2 is the EEType pointer add the VTableOffset + slot_number * pointer size to get the vtable entry
        ;; compare the that value to the address of the thunk being executed and if the values are equal then
        ;; call to the resolver otherwise call the method
        ldr r1, [r12]
        ;; r1 is the offset from start of EEType to interesting slot
        ldr r3, [r2,r1]
        ;; r3 is now the function pointer in the vtable
        ldr r2,[r12,#4]
        ;; is now the address of the function pointer that serves as the entry point for this particular instantiation
        ;; of __jmpstub__VTableResolver_CommonCallingStub
        cmp r2,r3
        beq  __jmpstub__JumpToVTableResolver
        mov r12,r3 ; Move the target function pointer to r12
        EPILOG_POP {r1,r2}
        EPILOG_POP {r3}
        EPILOG_BRANCH_REG r12
    LEAF_END __jmpstub__VTableResolver_CommonCallingStub

;; stub for dispatch will come in with r1 set to EETypeVTableOffset + ($slot_number * PointerSize), 
;; and r1, r2 and r3 of the function we really want to call pushed on the stack
    LEAF_ENTRY __jmpstub__JumpToVTableResolver
        mov r3, r1
        POP {r1,r2}
        str r3,  [sp, #-4] ; Store slot number into red zone at appropriate spot
        POP {r3}
        ldr     r12, =g_vtableResolveCallback
        ldr     r12, [r12]
        str     r12,  [sp, #-4] ; Store vtable resolve callback into red zone
        ldr     r12, =g_universalTransition
        ldr     r12, [r12]
        bx      r12
    LEAF_END __jmpstub__VTableResolver_Init


    MACRO
        VTableThunkDecl $name, $slot_number
        ALIGN 16 ; The alignment here forces the thunks to be the same size which gives all of the macros the same size and allows us to index
        LEAF_ENTRY __jmpstub__$name
        ;; rcx is the this pointer to the call being made
        PUSH {r3}
        PUSH {r1,r2}                            ; Push r1,r2
        ldr r2, [r0]
        ;; r2 is the EEType pointer add the VTableOffset + slot_number * pointer size to get the vtable entry
        ;; compare the that value to the address of the thunk being executed and if the values are equal then
        ;; call to the resolver otherwise call the method
        mov r1, EETypeVTableOffset + ($slot_number * PointerSize)
        ldr r3, [r2,r1]
        ; r3 is now the function pointer in the vtable
        ldr r2,=__jmpstub__$name
        cmp r2,r3
        beq  JumpVTableResolver$name
        mov r12,r3 ; Move the target function pointer to r12 before popping r2 and r3. We used r3 instead of r12 here so that
                   ; we could use the 2 byte thumb instructions and the whole thunk could fit in less than 32 bytes
        POP {r1,r2,r3}
        bx r12
JumpVTableResolver$name
        b  __jmpstub__JumpToVTableResolver
        LEAF_END __jmpstub__$name
    MEND

    MACRO
        VTableThunkDeclTen $slotnumberDecimal
        VTableThunkDecl VTableSlot$slotnumberDecimal0,$slotnumberDecimal0
        VTableThunkDecl VTableSlot$slotnumberDecimal1,$slotnumberDecimal1
        VTableThunkDecl VTableSlot$slotnumberDecimal2,$slotnumberDecimal2
        VTableThunkDecl VTableSlot$slotnumberDecimal3,$slotnumberDecimal3
        VTableThunkDecl VTableSlot$slotnumberDecimal4,$slotnumberDecimal4
        VTableThunkDecl VTableSlot$slotnumberDecimal5,$slotnumberDecimal5
        VTableThunkDecl VTableSlot$slotnumberDecimal6,$slotnumberDecimal6
        VTableThunkDecl VTableSlot$slotnumberDecimal7,$slotnumberDecimal7
        VTableThunkDecl VTableSlot$slotnumberDecimal8,$slotnumberDecimal8
        VTableThunkDecl VTableSlot$slotnumberDecimal9,$slotnumberDecimal9
    MEND

    MACRO
        VTableThunkDeclHundred $slotnumberPerHundred
        VTableThunkDeclTen $slotnumberPerHundred0
        VTableThunkDeclTen $slotnumberPerHundred1
        VTableThunkDeclTen $slotnumberPerHundred2
        VTableThunkDeclTen $slotnumberPerHundred3
        VTableThunkDeclTen $slotnumberPerHundred4
        VTableThunkDeclTen $slotnumberPerHundred5
        VTableThunkDeclTen $slotnumberPerHundred6
        VTableThunkDeclTen $slotnumberPerHundred7
        VTableThunkDeclTen $slotnumberPerHundred8
        VTableThunkDeclTen $slotnumberPerHundred9
    MEND

    VTableThunkDeclHundred 0

    END
