;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.


;; -----------------------------------------------------------------------------------------------------------
;;#include "asmmacros.inc"
;; -----------------------------------------------------------------------------------------------------------

LEAF_ENTRY macro Name, Section
    Section segment para 'CODE'
    align   16
    public  Name
    Name    proc
endm

LEAF_END macro Name, Section
    Name    endp
    Section ends
endm

;  - TAILCALL_RAX: ("jmp rax") should be used for tailcalls, this emits an instruction 
;            sequence which is recognized by the unwinder as a valid epilogue terminator
TAILJMP_RAX TEXTEQU <DB 048h, 0FFh, 0E0h>


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;  DATA SECTIONS  ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

_tls_array                          equ 58h     ;; offsetof(TEB, ThreadLocalStoragePointer)

POINTER_SIZE                        equ 08h

;; TLS variables
_TLS    SEGMENT ALIAS(".tls$")
    ThunkParamSlot  DQ 0000000000000000H
_TLS    ENDS

EXTRN   _tls_index:DWORD


;;;;;;;;;;;;;;;;;;;;;;; Interop Thunks Helpers ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;;
;; RhpCommonStub
;;
LEAF_ENTRY RhpCommonStub, _TEXT
        ;; There are arbitrary callers passing arguments with arbitrary signatures.
        ;; Custom calling convention:
        ;;      r10: pointer to the current thunk's data block (data contains 2 pointer values: context + target pointers)

        ;; Save context data into the ThunkParamSlot thread-local variable
        ;; A pointer to the delegate and function pointer for open static delegate should have been saved in the thunk's context cell during thunk allocation
        mov     [rsp + 8], rcx                                     ;; Save rcx in a home scratch location. Pushing the 
                                                                   ;; register on the stack will break callstack unwind
        mov     ecx, [_tls_index]
        mov     r11, gs:[_tls_array]
        mov     rax, [r11 + rcx * POINTER_SIZE]

        mov     rcx, [rsp + 8]                                     ;; Restore rcx
        
        ;; rax = base address of TLS data
        ;; r10 = address of context cell in thunk's data
        ;; r11 = trashed
        ;; r8 = trashed

        ;; store thunk address in thread static
        mov     r11, [r10]
        xor     r8, r8
        mov     r8d, SECTIONREL ThunkParamSlot
        mov     [rax + r8], r11                 ;;   ThunkParamSlot <- context slot data

        ;; jump to the target
        mov     rax, [r10 + POINTER_SIZE]
        TAILJMP_RAX
LEAF_END RhpCommonStub, _TEXT


;;
;; IntPtr RhpGetCommonStubAddress()
;;
LEAF_ENTRY RhpGetCommonStubAddress, _TEXT
        lea     rax, [RhpCommonStub]
        ret
LEAF_END RhpGetCommonStubAddress, _TEXT


;;
;; IntPtr RhpGetCurrentThunkContext()
;;
LEAF_ENTRY RhpGetCurrentThunkContext, _TEXT
        mov     r10d, [_tls_index]
        mov     r11, gs:[_tls_array]
        mov     r10, [r11 + r10 * POINTER_SIZE]
        xor     r8, r8 
        mov     r8d, SECTIONREL ThunkParamSlot
        mov     rax, [r10 + r8]                 ;;   rax <- ThunkParamSlot
        ret
LEAF_END RhpGetCurrentThunkContext, _TEXT


end
