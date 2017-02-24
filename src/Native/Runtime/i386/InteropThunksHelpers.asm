;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.


.586
.model  flat
option  casemap:none
.code

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


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;  DATA SECTIONS  ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

__tls_array                         equ 2Ch     ;; offsetof(TEB, ThreadLocalStoragePointer)

POINTER_SIZE                        equ 04h

;; TLS variables
_TLS    SEGMENT ALIAS(".tls$")
    ThunkParamSlot  DD 00000000H
_TLS    ENDS

ASSUME  fs : NOTHING
EXTRN   __tls_index:DWORD



;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; Interop Thunks Helpers ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;;
;; InteropNative_CommonStub
;;
LEAF_ENTRY InteropNative_CommonStub, _TEXT
        ;; There are arbitrary callers passing arguments with arbitrary signatures.
        ;; Custom calling convention:
        ;;      eax: pointer to the current thunk's data block (data contains 2 pointer values: context + target pointers)

        ;; Save context data into the ThunkParamSlot thread-local variable
        ;; A pointer to the delegate and function pointer for open static delegate should have been saved in the thunk's context cell during thunk allocation
        
        ;; make some scratch regs
        push    ecx
        push    edx

        mov     ecx, [__tls_index]
        mov     edx, fs:[__tls_array]
        mov     ecx, [edx + ecx * POINTER_SIZE]

        ;; eax = address of context cell in thunk's data
        ;; ecx = base address of TLS data
        ;; edx = trashed

        ;; store thunk address in thread static
        mov     edx, [eax]
        mov     eax, [eax + POINTER_SIZE]                          ;;   eax <- target slot data
        mov     [ecx + OFFSET ThunkParamSlot], edx                 ;;   ThunkParamSlot <- context slot data
        
        ;; restore the regs we used
        pop     edx
        pop     ecx

        ;; jump to the target
        jmp     eax
LEAF_END InteropNative_CommonStub, _TEXT


;;
;; IntPtr InteropNative_GetCommonStubAddress()
;;
LEAF_ENTRY InteropNative_GetCommonStubAddress, _TEXT
        lea     eax, [InteropNative_CommonStub]
        ret
LEAF_END InteropNative_GetCommonStubAddress, _TEXT


;;
;; IntPtr InteropNative_GetCurrentThunkContext()
;;
LEAF_ENTRY InteropNative_GetCurrentThunkContext, _TEXT
        mov     ecx, [__tls_index]
        mov     edx, fs:[__tls_array]
        mov     ecx, [edx + ecx * POINTER_SIZE]
        mov     eax, [ecx + OFFSET ThunkParamSlot]                 ;;   eax <- ThunkParamSlot
        ret
LEAF_END InteropNative_GetCurrentThunkContext, _TEXT


end
