;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

#include "kxarm64.h"

        DATAAREA

g_vtableResolveCallback DCQ 0 ; Address of virtual dispatch resolution callback
g_universalTransition   DCQ 0 ; Address of RhpUniversalTransition thunk

VTableThunkSize EQU 0x20
;; TODO - do something similar to Redhawk's asmoffsets to compute the value at compile time
EETypeVTableOffset EQU 0x18
PointerSize EQU 8

        TEXTAREA

;;
;; When an EEType is created, its VTable entries are initially filled with calls to the VTableSlot thunks for the appropriate
;; slot numbers. When the thunk is invoked, it checks whether the slot has already been resolved. If yes, then it just calls
;; the universal thunk. Otherwise, it calls the dispatch resolution callback, which will update the VTable slot and then call
;; the universal thunk.
;;

;;
;; Note: The "__jmpstub__" prefix is used to indicate to debugger
;; that it must step-through this stub when it encounters it while
;; stepping.
;;

    ;; int VTableResolver_Init(IntPtr *__jmpstub__VTableResolverSlot0,
    ;;                         IntPtr vtableResolveCallback,
    ;;                         IntPtr universalTransition,
    ;;                         int *slotCount)
    ;; Returns the size of the pre-generated thunks.
    ;;
    LEAF_ENTRY VTableResolver_Init
        adr     x9, __jmpstub__VTableSlot00
        str     x9, [x0]
        ADDROF  x10, g_vtableResolveCallback
        str     x1, [x10]
        ADDROF  x11, g_universalTransition
        str     x2, [x11]
        mov     x12, 100 ; This file defines 100 slot helpers
        str     x12, [x3]
        mov     x0, VTableThunkSize ; Each thunk is VTableThunkSize in bytes
        ret
    LEAF_END VTableResolver_Init

    ;; void* VTableResolver_GetCommonCallingStub()
    ;; Returns the address of the common calling stub.
    ;;
    LEAF_ENTRY VTableResolver_GetCommonCallingStub
        adr     x0, __jmpstub__VTableResolver_CommonCallingStub
        ret
    LEAF_END VTableResolver_GetCommonCallingStub

    ;; __jmpstub__VTableResolver_CommonCallingStub(?)
    ;; Used when we dynamically need a VTableResolver not pre-generated.
    ;;
    ;; xip0 contains a pointer to a VTableResolverStruct
    ;;   struct VTableResolverStruct
    ;;   {
    ;;       IntPtr offsetFromStartOfEETypePtr;
    ;;       IntPtr VTableThunkAddress;
    ;;   };
    ;;
    LEAF_ENTRY __jmpstub__VTableResolver_CommonCallingStub
        ;; Load the EEType pointer and add (EETypeVTableOffset + $slot_number * PointerSize) to calculate the VTable slot
        ;; address. Compare the pointer stored in the slot to the address of the thunk being executed. If the values are
        ;; equal, call the dispatch resolution callback; otherwise, call the function pointer stored in the slot.

        ;; x9 = EEType pointer (x0 is the "this" pointer for the call being made)
        ldr     x9, [x0]
        ;; xip1 = slot offset relative to EEType
        ldr     xip1, [xip0]
        ;; x10 = function pointer stored in the slot
        ldr     x10, [x9,xip1]
        ;; x11 = address of this thunk
        ldr     x11, [xip0,#PointerSize]
        ;; Compare two pointers
        cmp     x10, x11
        ;; If the method is not resolved yet, resolve it first
        beq     __jmpstub__JumpToVTableResolver
        ;; Otherwise, just call it
        br      x10
    LEAF_END __jmpstub__VTableResolver_CommonCallingStub

    ;; Calls the dispatch resolution callback with xip1 set to EETypeVTableOffset + ($slot_number * PointerSize)
    LEAF_ENTRY __jmpstub__JumpToVTableResolver
        ADDROF  xip0, g_vtableResolveCallback
        ldr     xip0, [xip0]
        ADDROF  x9, g_universalTransition
        ldr     x9, [x9]
        br      x9
    LEAF_END __jmpstub__VTableResolver_Init

    MACRO
        VTableThunkDecl $name, $slot_number
        ;; Force all thunks to be the same size, which allows us to index
        ALIGN 16
        LEAF_ENTRY __jmpstub__$name
        ;; Load the EEType pointer and add (EETypeVTableOffset + $slot_number * PointerSize) to calculate the VTable slot
        ;; address. Compare the pointer stored in the slot to the address of the thunk being executed. If the values are
        ;; equal, call the dispatch resolution callback; otherwise, call the function pointer stored in the slot.

        ;; x9 = EEType pointer (x0 is the "this" pointer for the call being made)
        ldr     x9, [x0]
        ;; xip1 = slot offset relative to EEType
        mov     xip1, EETypeVTableOffset + ($slot_number * PointerSize)
        ;; x10 = function pointer stored in the slot
        ldr     x10, [x9,xip1]
        ;; x11 = address of this thunk
        adr     x11, __jmpstub__$name
        ;; Compare two pointers
        cmp     x10, x11
        ;; If the method is not resolved yet, resolve it first
        beq     __jmpstub__JumpToVTableResolver
        ;; Otherwise, just call it
        br      x10
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

    VTableThunkDeclHundred ""

    END
