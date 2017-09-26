;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

        TEXTAREA

        IMPORT RhpReversePInvokeBadTransition

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpWaitForSuspend -- rare path for RhpPInvoke and RhpReversePInvokeReturn
;;
;;
;; INPUT: none
;;
;; TRASHES: none
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpWaitForSuspend
        brk 0xf000
    NESTED_END RhpWaitForSuspend


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpWaitForGC
;;
;;
;; INPUT: r2: transition frame
;;
;; OUTPUT: 
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpWaitForGC
        brk 0xf000
    NESTED_END RhpWaitForGC


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvoke
;;
;; IN:  r4: address of reverse pinvoke frame
;;                  0: save slot for previous M->U transition frame
;;                  4: save slot for thread pointer to avoid re-calc in epilog sequence
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpReversePInvoke
        brk 0xf000
    NESTED_END RhpReversePInvoke


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvokeAttachOrTrapThread -- rare path for RhpPInvoke
;;
;;
;; INPUT: r4: address of reverse pinvoke frame
;;
;; TRASHES: none
;; 
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    NESTED_ENTRY RhpReversePInvokeAttachOrTrapThread
        brk 0xf000
    NESTED_END RhpReversePInvokeTrapThread


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; RhpReversePInvokeReturn
;;
;; IN:  r3: address of reverse pinvoke frame
;;                  0: save slot for previous M->U transition frame
;;                  4: save slot for thread pointer to avoid re-calc in epilog sequence
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
    LEAF_ENTRY RhpReversePInvokeReturn
        brk 0xf000
    LEAF_END RhpReversePInvokeReturn


    end
