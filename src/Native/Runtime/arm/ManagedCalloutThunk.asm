;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

#include "AsmMacros.h"

        TEXTAREA

;;
;; Defines a small assembly thunk designed to be used when unmanaged code in the runtime calls out to managed
;; code. In such cases the stack walker needs to be able to bridge the unmanaged gap in the stack between the
;; callout and whatever managed code initially entered the runtime. This thunk makes that goal achievable by
;; (a) exporting a well-known address in the thunk that will be the result of unwinding from the callout (so
;; the stack frame iterator knows when its hit this case) and (b) placing a copy of a pointer to a transition
;; frame saved when the previous managed caller entered the runtime into a well-known location relative to the
;; thunk's frame, enabling the stack frame iterator to recover the transition frame address and use it to
;; re-initialize the stack walk at the previous managed caller.
;;
;; If we end up with more cases of this (currently it's used only for the ICastable extension point for
;; interface dispatch) then we might decide to produce a general routine which can handle an arbitrary number
;; of arguments to the target method. For now we'll just implement the case we need, which takes two regular
;; arguments (that's the 2 in the ManagedCallout2 name).
;;
;; Inputs:
;;      r0  : Argument 1 to target method
;;      r1  : Argument 2 to target method
;;      r2  : Target method address
;;      r3  : Pointer to previous managed method's transition frame into the runtime
;;
    NESTED_ENTRY ManagedCallout2

        ;; Push an r7 frame. Apart from making it easier to walk the stack the stack frame iterator locates
        ;; the transition frame for the previous managed caller relative to the frame pointer to keep the code
        ;; architecture independent.
        PROLOG_PUSH {r7,lr}
        PROLOG_STACK_SAVE r7
        PROLOG_STACK_ALLOC 8    ; Space for transition frame pointer plus stack alignment padding

        ;; Stash the previous transition frame's address immediately on top of the old r7 value. This
        ;; position is important; the stack frame iterator knows about this setup.
        str     r3, [r7, #MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET]

        ;; Call the target method. Arguments are already in the correct registers. The
        ;; ReturnFromManagedCallout2 label must immediately follow the blx instruction.
        blx     r2
    LABELED_RETURN_ADDRESS ReturnFromManagedCallout2

        ;; Pop the frame and return.
        EPILOG_STACK_RESTORE r7
        EPILOG_POP {r7,pc}

    NESTED_END ManagedCallout2

    EXPORT ReturnFromManagedCallout2

    END
