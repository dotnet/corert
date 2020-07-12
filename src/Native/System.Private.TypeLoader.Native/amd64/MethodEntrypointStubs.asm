;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

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

.data

g_methodEntrypointThunk qword 0  ; The method which operates on universal transition
g_universalTransition qword 0 ; The address of Redhawk's UniversalTransition thunk

.code

;  - TAILCALL_RAX: ("jmp rax") should be used for tailcalls, this emits an instruction 
;            sequence which is recognized by the unwinder as a valid epilogue terminator
TAILJMP_RAX TEXTEQU <DB 048h, 0FFh, 0E0h>

PointerSize equ 8

;;
;; __jmpstub__MethodEntrypointStubs_CommonCallingStub(?)
;;  Used when we dynamically need a VTableResolver not pre-generated
;;
;; r10 contains a pointer to a VTableResolverStruct
;;   struct MethodEntryPointStubInfo
;;   {
;;       IntPtr targetCodePointer;
;;       IntPtr MethodEntrypointStructPointer;
;;   };
;;
LEAF_ENTRY __jmpstub__MethodEntrypointStubs_CommonCallingStub, _TEXT
        ;; r10 <- stub info
        mov rax, [r10]
        cmp rax, 0
        je SLOW_PATH
        mov rax, [r10]
        TAILJMP_RAX
SLOW_PATH:
        mov         r11, [r10 + 8]
        mov         r10, g_methodEntrypointThunk
        mov         rax, g_universalTransition
        TAILJMP_RAX
LEAF_END __jmpstub__MethodEntrypointStubs_CommonCallingStub, _TEXT

;; Returns the size of the pre-generated thunks
;; IntPtr MethodEntrypointStubs_SetupPointers(
;;                          IntPtr universalTransition,
;;                          IntPtr methodEntrypointThunk)
;;
LEAF_ENTRY MethodEntrypointStubs_SetupPointers, _TEXT
        mov    g_universalTransition, rcx
        mov    g_methodEntrypointThunk, rdx
        lea    rax, [__jmpstub__MethodEntrypointStubs_CommonCallingStub]
        ret
LEAF_END MethodEntrypointStubs_SetupPointers, _TEXT

end
