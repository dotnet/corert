;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

        TEXTAREA

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpThrowHwEx
;;
;; INPUT:  R0:  exception code of fault
;;         R1:  faulting IP
;;
;; OUTPUT:
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpThrowHwEx
        brk 0xf000
    EXPORT_POINTER_TO_ADDRESS PointerToRhpThrowHwEx2
        ;; no return
        brk 0xf000
    NESTED_END RhpThrowHwEx

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpThrowEx
;;
;; INPUT:  R0:  exception object
;;
;; OUTPUT:
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpThrowEx
        brk 0xf000
    EXPORT_POINTER_TO_ADDRESS PointerToRhpThrowEx2

        ;; no return
        brk 0xf000
    NESTED_END RhpThrowEx

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void FASTCALL RhpRethrow()
;;
;; SUMMARY:  Similar to RhpThrowEx, except that it passes along the currently active ExInfo
;;
;; INPUT:
;;
;; OUTPUT:
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpRethrow
        brk 0xf000
    EXPORT_POINTER_TO_ADDRESS PointerToRhpRethrow2

        ;; no return
        brk 0xf000
    NESTED_END RhpRethrow

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* FASTCALL RhpCallCatchFunclet(RtuObjectRef exceptionObj, void* pHandlerIP, REGDISPLAY* pRegDisplay,
;;                                    ExInfo* pExInfo)
;;
;; INPUT:  R0:  exception object
;;         R1:  handler funclet address
;;         R2:  REGDISPLAY*
;;         R3:  ExInfo*
;;
;; OUTPUT:
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpCallCatchFunclet
        brk 0xf000
    EXPORT_POINTER_TO_ADDRESS PointerToRhpCallCatchFunclet2
        brk 0xf000
    NESTED_END RhpCallCatchFunclet

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void FASTCALL RhpCallFinallyFunclet(void* pHandlerIP, REGDISPLAY* pRegDisplay)
;;
;; INPUT:  R0:  handler funclet address
;;         R1:  REGDISPLAY*
;;
;; OUTPUT:
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpCallFinallyFunclet
        brk 0xf000
    EXPORT_POINTER_TO_ADDRESS PointerToRhpCallFinallyFunclet2
        brk 0xf000
    NESTED_END RhpCallFinallyFunclet


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; void* FASTCALL RhpCallFilterFunclet(RtuObjectRef exceptionObj, void* pFilterIP, REGDISPLAY* pRegDisplay)
;;
;; INPUT:  R0:  exception object
;;         R1:  filter funclet address
;;         R2:  REGDISPLAY*
;;
;; OUTPUT:
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpCallFilterFunclet
        brk 0xf000
    EXPORT_POINTER_TO_ADDRESS PointerToRhpCallFilterFunclet2
        brk 0xf000
    NESTED_END RhpCallFilterFunclet

    end
