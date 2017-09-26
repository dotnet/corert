;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "ksarm64.h"
    
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;; CallingConventionCoverter Helpers ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;;
;; Note: The "__jmpstub__" prefix is used to indicate to debugger
;; that it must step-through this stub when it encounters it while
;; stepping.
;;

    ;;
    ;; void CallingConventionConverter_ReturnThunk()
    ;;
    LEAF_ENTRY CallingConventionConverter_ReturnThunk
        brk 0xf000
    LEAF_END CallingConventionConverter_ReturnThunk

    ;;
    ;; __jmpstub__CallingConventionConverter_CommonCallingStub
    ;;
    ;; struct CallingConventionConverter_CommonCallingStub_PointerData
    ;; {
    ;;     void *ManagedCallConverterThunk;
    ;;     void *UniversalThunk;
    ;; }
    ;;
    ;; struct CommonCallingStubInputData
    ;; {
    ;;     ULONG_PTR CallingConventionId;
    ;;     CallingConventionConverter_CommonCallingStub_PointerData *commonData; // Only the ManagedCallConverterThunk field is used
    ;;                                                                           // However, it is specified just like other platforms, so the behavior of the common
    ;;                                                                           // calling stub is easier to debug
    ;; }
    ;;
    ;; sp-4 - Points at CommonCallingStubInputData
    ;;  
    ;;
    LEAF_ENTRY __jmpstub__CallingConventionConverter_CommonCallingStub
        brk 0xf000
    LEAF_END __jmpstub__CallingConventionConverter_CommonCallingStub

    ;;
    ;; void CallingConventionConverter_SpecifyCommonStubData(CallingConventionConverter_CommonCallingStub_PointerData *commonData);
    ;;
    LEAF_ENTRY CallingConventionConverter_SpecifyCommonStubData
        brk 0xf000
    LEAF_END CallingConventionConverter_SpecifyCommonStubData

    ;;
    ;; void CallingConventionConverter_GetStubs(IntPtr *returnVoidStub, IntPtr *returnIntegerStub, IntPtr *commonCallingStub)
    ;;
    LEAF_ENTRY CallingConventionConverter_GetStubs
        brk 0xf000
    LEAF_END CallingConventionConverter_GetStubs

    END
