;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.

#include "ksarm64.h"

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;  DATA SECTIONS  ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
POINTER_SIZE                        equ 0x08


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; ConstrainedCall Support Helpers ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;;
;; Note: The "__jmpstub__" prefix is used to indicate to debugger
;; that it must step-through this stub when it encounters it while
;; stepping.
;;
;; INPUT: xip0: AddressOfAddressOfFunctionToCallAfterDereferencingThis
;;
    LEAF_ENTRY __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub
        ldr     x12, [xip0]             ; Load tail jump target
        ldr     x0, [x0]                ; Dereference this to get real function pointer
        ret     x12
    LEAF_END __jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub

;;
;; void ConstrainedCallSupport_GetStubs(IntPtr *__jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub,
;;                                      IntPtr *__jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub)
;;
    LEAF_ENTRY ConstrainedCallSupport_GetStubs
        ldr     x12, =__jmpstub__ConstrainedCallSupport_DerefThisAndCall_CommonCallingStub
        str     x12, [x0]
        ldr     x12, =__jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub
        str     x12, [x1]
        ret
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
;; INPUT: xip0: Points at CommonCallingStubInputData
;;  
;;
    LEAF_ENTRY __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub
        ldr     xip1, [xip0]                    ; put ConstrainedCallDesc into xip1 (Arg to LookupFunc/Temp for getting ExactTarget)
        ldr     x12, [xip1]                     ; put ExactTarget into x12
        cbnz    x12, JumpToTarget               ; compare against null
        ; If we reach here, we need to use a universal thunk to call the LookupFunc
        ldr     x12, [xip1, #POINTER_SIZE]      ; Get Universal thunk function pointer into x12
        ldr     xip0, [xip0, #POINTER_SIZE]     ; Put DirectConstrainedCallResolver into xip0 for UniversalTransitionThunk call
JumpToTarget
        ret     x12
    LEAF_END __jmpstub__ConstrainedCallSupport_DirectConstrainedCallCommonStub

    END
