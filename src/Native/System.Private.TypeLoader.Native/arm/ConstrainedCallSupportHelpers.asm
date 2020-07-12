;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

#include "kxarm.h"

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;  DATA SECTIONS  ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; ConstrainedCall Support Helpers ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;;
;; Note: The "__jmpstub__" prefix is used to indicate to debugger
;; that it must step-through this stub when it encounters it while
;; stepping.
;;
    ;;
    ;; sp-4 - AddressOfAddressOfFunctionToCallAfterDereferencingThis
    ;;
    LEAF_ENTRY __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub
        ldr     r12, [sp, #-4]
        ldr     r12, [r12]
        ldr     r0, [r0]
        bx      r12
    LEAF_END __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub

;;
;; void ConstrainedCallSupport_GetStubs(IntPtr *__jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub,
;;                                      IntPtr *__jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub)
;;
    LEAF_ENTRY ConstrainedCallSupport_GetStubs
        ldr     r12, =__jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub
        str     r12, [r0]
        ldr     r12, =__jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub
        str     r12, [r1]
        bx      lr
    LEAF_END ConstrainedCallSupport_GetStubs

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
;; sp-4 - Points at CommonCallingStubInputData
;;  
;;
    LEAF_ENTRY __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub
        ldr    r12, [sp, #-4] ; put CommonCallingStubInputData into r12 (Temp for getting ExactTarget)
        ldr    r12, [r12]     ; put ConstrainedCallDesc into r12 (Temp for getting ExactTarget)
        ldr    r12, [r12]     ; put ExactTarget into r12
        cmp    r12, 0         ; Is ExactTarget null?
        beq    NeedHelperCall ; if null use a helper call
        bx     r12            ; Otherwise tail-call the ExactTarget
NeedHelperCall
        ;; Setup arguments for UniversalThunk and call it.
        ldr    r12, [sp, #-4] ; put CommonCallingStubInputData into r12 (Temp for getting ConstrainedCallDesc)
        ldr    r12, [r12]     ; put ConstrainedCallDesc into r12
        str    r12, [sp, #-8] ; put ConstrainedCallDesc into sp-8 (red zone location of custom calling convention for universal thunk)

        ldr    r12, [sp, #-4] ; put CommonCallingStubInputData into r12 (Temp for getting DirectConstrainedCallResolver)
        ldr    r12, [r12, #4] ; put DirectConstrainedCallResolver into r12
        str    r12, [sp, #-4] ; put DirectConstrainedCallResolver into sp-4 (red zone location of custom calling convention for universal thunk)

        ldr    r12, [sp, #-8] ; put ConstrainedCallDesc into r12 (Temp for getting ExactTarget)
        ldr    r12, [r12, #4] ; put LookupFunc into r12 (This should be universal thunk pointer)
        bx     r12            ; Tail-Call Universal thunk
    LEAF_END __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub

    END
