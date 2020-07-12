;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

.586
.model  flat
option  casemap:none

;; -----------------------------------------------------------------------------------------------------------
;; standard macros
;; -----------------------------------------------------------------------------------------------------------
LEAF_ENTRY macro Name, Section
    Section segment para 'CODE'
    public  Name
    Name    proc
endm

LEAF_END macro Name, Section
    Name    endp
    Section ends
endm

;;
;; When an EEType is created its vTable entries will be initially filled with calls to the vTableThunk for the appropriate 
;; slot number. When the thunk is invoked it'll check to see if the slot has already been resolved. If so then just call
;; the universal thunk, otherwise call to do the resolution which will also update the vtable slot and then call
;; the universal thunk
;; 

VTableThunkSize equ 20h
;; TODO - do something similar to Redhawk's asmoffsets to compute the value at compile time
EETypeVTableOffset equ 14h
PointerSize equ 4

.data

g_vtableResolveCallback dword 0  ; The vtableresolve method
g_universalTransition dword 0 ; The address of Redhawk's UniversalTransition thunk

.code

;;
;; __jmpstub__VTableResolver_CommonCallingStub(?)
;;  Used when we dynamically need a VTableResolver not pre-generated
;;
;; eax contains a pointer to a VTableResolverStruct
;;   struct VTableResolverStruct
;;   {
;;       int offsetFromStartOfEETypePtr;
;;       IntPtr VTableThunkAddress;
;;   };
;;
LEAF_ENTRY __jmpstub__VTableResolver_CommonCallingStub, _TEXT
        ;; eax <- stub info
        push        esi
        push        edi
        mov         esi, [eax] ; Get EEType offset into esi

        mov         edi, [eax + 4]
        ;; edi now has specific address of the exact thunk being processed here

        ;; ecx is the this pointer to the call being made
        mov         eax, [ecx]
        ;; eax is the EEType pointer add the VTableOffset + slot_number * pointer size to get the vtable entry

        ;; compare the that value to the address of the thunk being executed and if the values are equal then
        ;; call to the resolver otherwise call the method

        mov         eax, [eax + esi]
        cmp         eax, edi
        je  SLOW_DYNAMIC_STUB
        pop         edi
        pop         esi
        jmp eax
SLOW_DYNAMIC_STUB:
        ;; Capture EEType offset into eax
        mov         eax,esi

        ;; Restore edi and esi so that we can set up the call into the universal transition thunk
        pop         edi
        pop         esi

        jmp         __jmpstub__JumpToVTableResolver
LEAF_END __jmpstub__VTableResolver_CommonCallingStub, _TEXT

;; Returns the size of the pre-generated thunks
;; int VTableResolver_Init(IntPtr *__jmpstub__VTableResolverSlot0,
;;                          IntPtr vtableResolveCallback,
;;                          IntPtr universalTransition,
;;                          int *slotCount)
;;
LEAF_ENTRY VTableResolver_Init, _TEXT
        lea     eax, dword ptr [__jmpstub__VTableSlot0]
        mov     ecx, [esp+04h]
        mov     [ecx], eax
        mov     ecx, [esp+08h]
        mov    g_vtableResolveCallback, ecx
        mov     ecx, [esp+0Ch]
        mov    g_universalTransition, ecx
        mov     ecx, [esp+10h]
        mov     eax, 100 ;; 100 Pregenerated thunks
        mov     [ecx], eax
        mov     eax, VTableThunkSize
        ret
LEAF_END VTableResolver_Init, _TEXT

;; void* VTableResolver_GetCommonCallingStub()
;;  - Get the address of the common calling stub
LEAF_ENTRY VTableResolver_GetCommonCallingStub, _TEXT
        lea     eax, [__jmpstub__VTableResolver_CommonCallingStub]
        ret
LEAF_END VTableResolver_GetCommonCallingStub, _TEXT


;; stub for dispatch will come in with eax set to EETypeVTableOffset + ($slot_number * PointerSize), 
LEAF_ENTRY __jmpstub__JumpToVTableResolver, _TEXT
        push        ebp
        mov         ebp, esp
        push        eax   ; First argument
        push g_vtableResolveCallback
        jmp g_universalTransition
LEAF_END __jmpstub__JumpToVTableResolver, _TEXT


VTableThunkDecl macro name, slot_number

ALIGN 16 ; The alignment here forces the thunks to be the same size which gives all of the macros the same size and allows us to index
LEAF_ENTRY name, _TEXT
        ;; rcx is the this pointer to the call being made
        mov eax, [ecx]
        ;; eax is the EEType pointer add the VTableOffset + slot_number * pointer size to get the vtable entry
        ;; compare the that value to the address of the thunk being executed and if the values are equal then
        ;; call to the resolver otherwise call the method
        mov eax, [eax + EETypeVTableOffset + slot_number * PointerSize]
        cmp eax, name
        je  SLOW
        jmp eax
SLOW:
        mov eax, EETypeVTableOffset + slot_number * PointerSize
        jmp __jmpstub__JumpToVTableResolver
LEAF_END name, _TEXT

        endm

VTableThunkDecl __jmpstub__VTableSlot0,0
VTableThunkDecl __jmpstub__VTableSlot1,1
VTableThunkDecl __jmpstub__VTableSlot2,2
VTableThunkDecl __jmpstub__VTableSlot3,3
VTableThunkDecl __jmpstub__VTableSlot4,4
VTableThunkDecl __jmpstub__VTableSlot5,5
VTableThunkDecl __jmpstub__VTableSlot6,6
VTableThunkDecl __jmpstub__VTableSlot7,7
VTableThunkDecl __jmpstub__VTableSlot8,8
VTableThunkDecl __jmpstub__VTableSlot9,9
VTableThunkDecl __jmpstub__VTableSlot10,10
VTableThunkDecl __jmpstub__VTableSlot11,11
VTableThunkDecl __jmpstub__VTableSlot12,12
VTableThunkDecl __jmpstub__VTableSlot13,13
VTableThunkDecl __jmpstub__VTableSlot14,14
VTableThunkDecl __jmpstub__VTableSlot15,15
VTableThunkDecl __jmpstub__VTableSlot16,16
VTableThunkDecl __jmpstub__VTableSlot17,17
VTableThunkDecl __jmpstub__VTableSlot18,18
VTableThunkDecl __jmpstub__VTableSlot19,19
VTableThunkDecl __jmpstub__VTableSlot20,20
VTableThunkDecl __jmpstub__VTableSlot21,21
VTableThunkDecl __jmpstub__VTableSlot22,22
VTableThunkDecl __jmpstub__VTableSlot23,23
VTableThunkDecl __jmpstub__VTableSlot24,24
VTableThunkDecl __jmpstub__VTableSlot25,25
VTableThunkDecl __jmpstub__VTableSlot26,26
VTableThunkDecl __jmpstub__VTableSlot27,27
VTableThunkDecl __jmpstub__VTableSlot28,28
VTableThunkDecl __jmpstub__VTableSlot29,29
VTableThunkDecl __jmpstub__VTableSlot30,30
VTableThunkDecl __jmpstub__VTableSlot31,31
VTableThunkDecl __jmpstub__VTableSlot32,32
VTableThunkDecl __jmpstub__VTableSlot33,33
VTableThunkDecl __jmpstub__VTableSlot34,34
VTableThunkDecl __jmpstub__VTableSlot35,35
VTableThunkDecl __jmpstub__VTableSlot36,36
VTableThunkDecl __jmpstub__VTableSlot37,37
VTableThunkDecl __jmpstub__VTableSlot38,38
VTableThunkDecl __jmpstub__VTableSlot39,39
VTableThunkDecl __jmpstub__VTableSlot40,40
VTableThunkDecl __jmpstub__VTableSlot41,41
VTableThunkDecl __jmpstub__VTableSlot42,42
VTableThunkDecl __jmpstub__VTableSlot43,43
VTableThunkDecl __jmpstub__VTableSlot44,44
VTableThunkDecl __jmpstub__VTableSlot45,45
VTableThunkDecl __jmpstub__VTableSlot46,46
VTableThunkDecl __jmpstub__VTableSlot47,47
VTableThunkDecl __jmpstub__VTableSlot48,48
VTableThunkDecl __jmpstub__VTableSlot49,49
VTableThunkDecl __jmpstub__VTableSlot50,50
VTableThunkDecl __jmpstub__VTableSlot51,51
VTableThunkDecl __jmpstub__VTableSlot52,52
VTableThunkDecl __jmpstub__VTableSlot53,53
VTableThunkDecl __jmpstub__VTableSlot54,54
VTableThunkDecl __jmpstub__VTableSlot55,55
VTableThunkDecl __jmpstub__VTableSlot56,56
VTableThunkDecl __jmpstub__VTableSlot57,57
VTableThunkDecl __jmpstub__VTableSlot58,58
VTableThunkDecl __jmpstub__VTableSlot59,59
VTableThunkDecl __jmpstub__VTableSlot60,60
VTableThunkDecl __jmpstub__VTableSlot61,61
VTableThunkDecl __jmpstub__VTableSlot62,62
VTableThunkDecl __jmpstub__VTableSlot63,63
VTableThunkDecl __jmpstub__VTableSlot64,64
VTableThunkDecl __jmpstub__VTableSlot65,65
VTableThunkDecl __jmpstub__VTableSlot66,66
VTableThunkDecl __jmpstub__VTableSlot67,67
VTableThunkDecl __jmpstub__VTableSlot68,68
VTableThunkDecl __jmpstub__VTableSlot69,69
VTableThunkDecl __jmpstub__VTableSlot70,70
VTableThunkDecl __jmpstub__VTableSlot71,71
VTableThunkDecl __jmpstub__VTableSlot72,72
VTableThunkDecl __jmpstub__VTableSlot73,73
VTableThunkDecl __jmpstub__VTableSlot74,74
VTableThunkDecl __jmpstub__VTableSlot75,75
VTableThunkDecl __jmpstub__VTableSlot76,76
VTableThunkDecl __jmpstub__VTableSlot77,77
VTableThunkDecl __jmpstub__VTableSlot78,78
VTableThunkDecl __jmpstub__VTableSlot79,79
VTableThunkDecl __jmpstub__VTableSlot80,80
VTableThunkDecl __jmpstub__VTableSlot81,81
VTableThunkDecl __jmpstub__VTableSlot82,82
VTableThunkDecl __jmpstub__VTableSlot83,83
VTableThunkDecl __jmpstub__VTableSlot84,84
VTableThunkDecl __jmpstub__VTableSlot85,85
VTableThunkDecl __jmpstub__VTableSlot86,86
VTableThunkDecl __jmpstub__VTableSlot87,87
VTableThunkDecl __jmpstub__VTableSlot88,88
VTableThunkDecl __jmpstub__VTableSlot89,89
VTableThunkDecl __jmpstub__VTableSlot90,90
VTableThunkDecl __jmpstub__VTableSlot91,91
VTableThunkDecl __jmpstub__VTableSlot92,92
VTableThunkDecl __jmpstub__VTableSlot93,93
VTableThunkDecl __jmpstub__VTableSlot94,94
VTableThunkDecl __jmpstub__VTableSlot95,95
VTableThunkDecl __jmpstub__VTableSlot96,96
VTableThunkDecl __jmpstub__VTableSlot97,97
VTableThunkDecl __jmpstub__VTableSlot98,98
VTableThunkDecl __jmpstub__VTableSlot99,99

end
