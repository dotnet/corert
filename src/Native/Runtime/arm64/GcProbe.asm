;; Licensed to the .NET Foundation under one or more agreements.
;; The .NET Foundation licenses this file to you under the MIT license.
;; See the LICENSE file in the project root for more information.

#include "AsmMacros.h"

    TEXTAREA

;;
;;
;;
;; GC Probe Hijack targets
;;
;;
    EXTERN RhpPInvokeExceptionGuard


    NESTED_ENTRY RhpGcProbeHijackScalarWrapper, .text, RhpPInvokeExceptionGuard
        brk 0xf000
    LABELED_RETURN_ADDRESS RhpGcProbeHijackScalar
        brk 0xf000
    NESTED_END RhpGcProbeHijackScalarWrapper

    NESTED_ENTRY RhpGcProbeHijackObjectWrapper, .text, RhpPInvokeExceptionGuard
        brk 0xf000
    LABELED_RETURN_ADDRESS RhpGcProbeHijackObject
        brk 0xf000
    NESTED_END RhpGcProbeHijackObjectWrapper

    NESTED_ENTRY RhpGcProbeHijackByrefWrapper, .text, RhpPInvokeExceptionGuard
        brk 0xf000
    LABELED_RETURN_ADDRESS RhpGcProbeHijackByref
        brk 0xf000
    NESTED_END RhpGcProbeHijackByrefWrapper

#ifdef FEATURE_GC_STRESS
;;
;;
;; GC Stress Hijack targets
;;
;;
    LEAF_ENTRY RhpGcStressHijackScalar
        brk 0xf000
    LEAF_END RhpGcStressHijackScalar

    LEAF_ENTRY RhpGcStressHijackObject
        brk 0xf000
    LEAF_END RhpGcStressHijackObject

    LEAF_ENTRY RhpGcStressHijackByref
        brk 0xf000
    LEAF_END RhpGcStressHijackByref


;;
;; Worker for our GC stress probes.  Do not call directly!!  
;; Instead, go through RhpGcStressHijack{Scalar|Object|Byref}. 
;; This worker performs the GC Stress work and returns to the original return address.
;;
;; Register state on entry:
;;  r0: hijacked function return value
;;  r1: hijacked function return value
;;  r2: thread pointer
;;  r12: register bitmask
;;
;; Register state on exit:
;;  Scratch registers, except for r0, have been trashed
;;  All other registers restored as they were when the hijack was first reached.
;;
    NESTED_ENTRY RhpGcStressProbe
        brk 0xf000
    NESTED_END RhpGcStressProbe
#endif ;; FEATURE_GC_STRESS

    LEAF_ENTRY RhpGcProbe
        brk 0xf000
    LEAF_END RhpGcProbe

    NESTED_ENTRY RhpGcProbeRare
        brk 0xf000
    NESTED_END RhpGcProbe

    LEAF_ENTRY RhpGcPoll
        brk 0xf000
    LEAF_END RhpGcPoll

    NESTED_ENTRY RhpGcPollRare
        brk 0xf000
    NESTED_END RhpGcPoll

    LEAF_ENTRY RhpGcPollStress
        ;
        ; loop hijacking is used instead
        ;
        brk 0xf000

    LEAF_END RhpGcPollStress


#ifdef FEATURE_GC_STRESS
    NESTED_ENTRY RhpHijackForGcStress
        brk 0xf000
    NESTED_END RhpHijackForGcStress
#endif ;; FEATURE_GC_STRESS


;;
;; The following functions are _jumped_ to when we need to transfer control from one method to another for EH 
;; dispatch. These are needed to properly coordinate with the GC hijacking logic. We are essentially replacing
;; the return from the throwing method with a jump to the handler in the caller, but we need to be aware of 
;; any return address hijack that may be in place for GC suspension. These routines use a quick test of the 
;; return address against a specific GC hijack routine, and then fixup the stack pointer to what it would be 
;; after a real return from the throwing method. Then, if we are not hijacked we can simply jump to the 
;; handler in the caller.
;; 
;; If we are hijacked, then we jump to a routine that will unhijack appropriatley and wait for the GC to 
;; complete. There are also variants for GC stress.
;;
;; Note that at this point we are eiher hijacked or we are not, and this will not change until we return to 
;; managed code. It is an invariant of the system that a thread will only attempt to hijack or unhijack 
;; another thread while the target thread is suspended in managed code, and this is _not_ managed code.
;;
    MACRO
        RTU_EH_JUMP_HELPER $funcName, $hijackFuncName, $isStress, $stressFuncName

        LEAF_ENTRY $funcName
        brk 0xf000
        LEAF_END $funcName
    MEND
;; We need an instance of the helper for each possible hijack function. The binder has enough
;; information to determine which one we need to use for any function.
    RTU_EH_JUMP_HELPER RhpEHJumpScalar,         RhpGcProbeHijackScalar, {false}, 0
    RTU_EH_JUMP_HELPER RhpEHJumpObject,         RhpGcProbeHijackObject, {false}, 0
    RTU_EH_JUMP_HELPER RhpEHJumpByref,          RhpGcProbeHijackByref,  {false}, 0
#ifdef FEATURE_GC_STRESS
    RTU_EH_JUMP_HELPER RhpEHJumpScalarGCStress, RhpGcProbeHijackScalar, {true},  RhpGcStressHijackScalar
    RTU_EH_JUMP_HELPER RhpEHJumpObjectGCStress, RhpGcProbeHijackObject, {true},  RhpGcStressHijackObject
    RTU_EH_JUMP_HELPER RhpEHJumpByrefGCStress,  RhpGcProbeHijackByref,  {true},  RhpGcStressHijackByref
#endif


;;
;; We are hijacked for a normal GC (not GC stress), so we need to unhijack and wait for the GC to complete.
;;
;; Register state on entry:
;;  r0: reference to the exception object.
;;  r2: thread
;;  Non-volatile registers are all already correct for return to the caller.
;;  The stack is as if we have tail called to this function (lr points to return address).
;;        
;; Register state on exit:
;;  r7: previous frame pointer
;;  r0: reference to the exception object
;;
    NESTED_ENTRY RhpGCProbeForEHJump
        brk 0xf000
    NESTED_END RhpGCProbeForEHJump

#ifdef FEATURE_GC_STRESS
;;
;; We are hijacked for GC Stress (not a normal GC) so we need to invoke the GC stress helper.
;;
;; Register state on entry:
;;  r1: reference to the exception object.
;;  r2: thread
;;  Non-volatile registers are all already correct for return to the caller.
;;  The stack is as if we have tail called to this function (lr points to return address).
;;        
;; Register state on exit:
;;  r7: previous frame pointer
;;  r0: reference to the exception object
;;
    NESTED_ENTRY RhpGCStressProbeForEHJump
        brk 0xf000
    NESTED_END RhpGCStressProbeForEHJump

;;
;; INVARIANT: Don't trash the argument registers, the binder codegen depends on this.
;;
    LEAF_ENTRY RhpSuppressGcStress
        brk 0xf000
    LEAF_END RhpSuppressGcStress
#endif ;; FEATURE_GC_STRESS

;; ALLOC_PROBE_FRAME will save the first 4 vfp registers, in order to avoid trashing VFP registers across the loop 
;; hijack, we must save the rest -- d4-d15 (12) and d16-d31 (16).
VFP_EXTRA_SAVE_SIZE equ ((12*8) + (16*8))

;; Helper called from hijacked loops
    LEAF_ENTRY RhpLoopHijack
        brk 0xf000
    LEAF_END RhpLoopHijack

    end
