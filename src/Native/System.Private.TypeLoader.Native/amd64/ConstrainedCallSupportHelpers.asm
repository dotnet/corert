;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

;; -----------------------------------------------------------------------------------------------------------
;; #include "asmmacros.inc"
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
POINTER_SIZE                        equ 08h

;;
;; Note: The "__jmpstub__" prefix is used to indicate to debugger
;; that it must step-through this stub when it encounters it while
;; stepping.
;;

;;
;; __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub
;;
;; r10 - AddressOfAddressOfFunctionToCallAfterDereferencingThis
;;
LEAF_ENTRY __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub, _TEXT
        mov     rax, [r10]                ; Tail jumps go through RAX, so copy function pointer there
        mov     rcx, [rcx]                ; Deference this to get real function pointer
        TAILJMP_RAX
LEAF_END __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub, _TEXT

;;
;; void ConstrainedCallSupport_GetStubs(IntPtr *__jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub,
;;                                      IntPtr *__jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub)
;;
LEAF_ENTRY ConstrainedCallSupport_GetStubs, _TEXT
        lea     rax, [__jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub]
        mov    [rcx], rax
        lea     rax, [__jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub]
        mov    [rdx], rax
        ret
LEAF_END ConstrainedCallSupport_GetStubs, _TEXT

;;
;; __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub
;;
;; struct ConstrainedCallDesc
;; {
;;     ULONG_PTR ExactTarget;
;;     ULONG_PTR LookupFunc; // Put UniversalThunk here
;; }
;;
;; struct CommonCallingStubInputData
;; {
;;     ULONG_PTR ConstrainedCallDesc;
;;     ULONG_PTR DirectConstrainedCallResolver;
;; }
;;
;; r10 - Points at CommonCallingStubInputData
;;  
;;
LEAF_ENTRY __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub, _TEXT
        mov     r11, [r10]                ; put ConstrainedCallDesc into r11 (Arg to LookupFunc/Temp for getting ExactTarget)
        mov     rax, [r11]                ; put ExactTarget into rax
        test    rax, rax                  ; compare against null
        jnz     JumpToTarget              ; if not null, we don't need to call helper to get result. Just jump
        ; If we reach here, we need to use a universal thunk to call the LookupFunc
        mov     rax, [r11 + POINTER_SIZE] ; Get Universal thunk function pointer into rax
        mov     r10, [r10 + POINTER_SIZE] ; Put DirectConstrainedCallResolver into r10 for UniversalTransitionThunk call
JumpToTarget:
        TAILJMP_RAX
LEAF_END __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub, _TEXT

end
