;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

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

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; ConstrainedCall Support Helpers ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;


;;
;; Note: The "__jmpstub__" prefix is used to indicate to debugger
;; that it must step-through this stub when it encounters it while
;; stepping.
;;

;;
;; __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub
;;
;; eax - AddressOfAddressOfFunctionToCallAfterDereferencingThis
;;
LEAF_ENTRY __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub, _TEXT
        mov     eax, [eax]                ; Get function pointer to call
        mov     ecx, [ecx]                ; Deference this to get real this pointer
        jmp     eax
LEAF_END __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub, _TEXT

;;
;; void ConstrainedCallSupport_GetStubs(IntPtr *__jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub,
;;                                      IntPtr *__jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub)
;;
LEAF_ENTRY ConstrainedCallSupport_GetStubs, _TEXT
        lea     eax, [__jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub]
        mov     ecx, [esp+04h]
        mov     [ecx], eax
        lea     eax, [__jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub]
        mov     ecx, [esp+08h]
        mov     [ecx], eax
        retn 8h
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
;; eax - Points at CommonCallingStubInputData
;;  
;;
LEAF_ENTRY __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub, _TEXT
;; eax points at CommonCallingStubInputData
        push        eax       ; save eax
        mov         eax,[eax] ; put ConstrainedCallDesc in eax
        mov         eax,[eax] ; Load ExactTarget into eax
        test        eax,eax   ; Check ExactTarget for null
        jz          NeedsHelperCall
        add         esp,4     ; Adjust eax back to what it was before the first instruction push
        jmp         eax       ; TailCall exact target
NeedsHelperCall:
        pop         eax       ; Restore back to exact state that was present at start of function
;; eax points at CommonCallingStubInputData
        push        ebp
        mov         ebp, esp
        push        [eax]   ; First argument (ConstrainedCallDesc)
        push        [eax+4] ; Second argument (DirectConstrainedCallResolver)
        mov         eax,[eax] ; Load ConstrainedCallDesc into eax
        mov         eax,[eax+4] ; Load Universal Thunk address into eax
        jmp         eax
LEAF_END __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub, _TEXT

end
