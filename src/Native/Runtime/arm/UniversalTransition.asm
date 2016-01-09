;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

#include "AsmMacros.h"

#ifdef _DEBUG
#define TRASH_SAVED_ARGUMENT_REGISTERS
#endif

#ifdef TRASH_SAVED_ARGUMENT_REGISTERS
        EXTERN RhpIntegerTrashValues
        EXTERN RhpFpTrashValues
#endif ;; TRASH_SAVED_ARGUMENT_REGISTERS

#define COUNT_ARG_REGISTERS (4)
#define INTEGER_REGISTER_SIZE (4)
#define ARGUMENT_REGISTERS_SIZE (COUNT_ARG_REGISTERS * INTEGER_REGISTER_SIZE)

;; Largest return block is 4 doubles
#define RETURN_BLOCK_SIZE (32) 

#define COUNT_FLOAT_ARG_REGISTERS (8)
#define FLOAT_REGISTER_SIZE (8)
#define FLOAT_ARG_REGISTERS_SIZE (COUNT_FLOAT_ARG_REGISTERS * FLOAT_REGISTER_SIZE)

#define TRANSITION_FRAMEPOINTER_AND_ALIGNMENT 8

#define PINVOKE_TRANSITION_BLOCK_SIZE (12*INTEGER_REGISTER_SIZE)

;;
;; From CallerSP to ChildSP, the stack frame is composed of the following five adjacent
;; regions:
;;
;;      ARGUMENT_REGISTERS_SIZE
;;      RETURN_BLOCK_SIZE
;;      FLOAT_ARG_REGISTERS_SIZE
;;      TRANSITION_FRAMEPOINTER_AND_ALIGNMENT
;;      PINVOKE_TRANSITION_BLOCK_SIZE
;;
;; R7 points to the top of the TRANSITION_FRAMEPOINTER_AND_ALIGNMENT region.
;;

#define DISTANCE_FROM_CHILDSP_TO_R7 (PINVOKE_TRANSITION_BLOCK_SIZE + TRANSITION_FRAMEPOINTER_AND_ALIGNMENT)

#define DISTANCE_FROM_CHILDSP_TO_RETURN_BLOCK (DISTANCE_FROM_CHILDSP_TO_R7 + FLOAT_ARG_REGISTERS_SIZE)

#define DISTANCE_FROM_CHILDSP_TO_CALLERSP (DISTANCE_FROM_CHILDSP_TO_RETURN_BLOCK + RETURN_BLOCK_SIZE + ARGUMENT_REGISTERS_SIZE)

#define DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_CALLERSP (DISTANCE_FROM_CHILDSP_TO_CALLERSP - PINVOKE_TRANSITION_BLOCK_SIZE)

        TEXTAREA

;;
;; RhpUniversalTransition
;; 
;; At input to this function, r0-3, d0-7 and the stack may contain any number of arguments.
;;
;; In addition, there are 2 extra arguments passed in the RED ZONE (8 byte negative space
;; off of sp).
;; sp-4 will contain the managed function that is to be called by this transition function
;; sp-8 will contain the pointer sized extra argument to the managed function
;;
;; This function will capture all of the arguments to the stack to make a TransitionBlock
;; and then create a PInvokeTransitionFrame for conservative stack walking.
;;
;; At the time of the call to the managed function the stack shall look as follows (stacks grow down)
;;
;; (STACK ARGS)
;; -------------
;; R3
;; R2
;; R1
;; R0
;; RETURN BLOCK (32 byte chunk of conservatively handled memory)
;; ------ The base address of the Return block is the TransitionBlock pointer, the floating point args are
;;        in the neg space of the TransitionBlock pointer.  Note that the callee has knowledge of the exact
;;        layout of all pieces of the frame that lie at or above the pushed floating point registers.
;; D7
;; D6
;; D5
;; D4
;; D3
;; D2
;; D1
;; D0
;; Pointer to Transition Frame
;; Alignment Padding (4 bytes)
;;---------------------
;; PINVOKE TRANSITION_FRAME
;;---------------------

;; r0 shall contain a pointer to the TransitionBlock
;; r1 shall contain the value that was in sp-8 at entry to this function
;;
        NESTED_ENTRY RhpUniversalTransition
        ;; Save argument registers (including floating point) and the return address. Note that we depend on
        ;; these registers (which may contain GC references) being spilled before we build the
        ;; PInvokeTransitionFrame below due to the way we build a stack range to report to the GC
        ;; conservatively during a collection.
        ;; NOTE: While we do that, capture the two arguments in the red zone into r12 and r3.
        PROLOG_NOP  ldr r12, [sp, #-4]              ; Capture first argument from red zone into r12
        PROLOG_PUSH {r3}                            ; Push r3
        PROLOG_NOP  ldr r3, [sp, #-4]               ; Capture second argument from red zone into r3
        PROLOG_PUSH {r0-r2}                         ; Push the rest of the registers
        PROLOG_STACK_ALLOC RETURN_BLOCK_SIZE        ; Save space a buffer to be used to hold return buffer data.
        PROLOG_VPUSH {d0-d7}                        ; Capture the floating point argument registers

        ;; Build space to save pointer to transition frame
        PROLOG_STACK_ALLOC TRANSITION_FRAMEPOINTER_AND_ALIGNMENT    ; Space for transition frame pointer plus stack alignment padding

        ;; Build the transition frame that the stack walker will use to unwind through this function.
        ;; The NoModeSwitch flag indicates that this function never uses Enable/DisablePreemptiveGC,
        ;; implying that frame address does not need to be recorded in the current thread object.
        ;; This macro trashes r4 (after it is saved into the frame) but does not trash any other registers.

        COOP_PINVOKE_FRAME_PROLOG_TAIL DISTANCE_FROM_TOP_OF_PINVOKE_FRAME_TO_CALLERSP, NoModeSwitch

        ;; The prolog has ended, r7 has been saved into the transition frame, and sp now holds
        ;; the address of the newly allocated transition frame.  If the stack walker unwinds
        ;; through this function, it will locate the transition frame pointer by checking the stack
        ;; slot directly below whatever address is in r7.  Point r7 to the top of the 8 byte save
        ;; area that was allocated above, then store the transition frame address directly below it.
        add         r7, sp, #DISTANCE_FROM_CHILDSP_TO_R7
        str         sp, [r7, #MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET]

        ;; Setup the arguments to the transition thunk.
        mov         r1, r3

#ifdef TRASH_SAVED_ARGUMENT_REGISTERS

        ;; Before calling out, trash all of the argument registers except the ones (r0, r1) that
        ;; hold outgoing arguments.  All of these registers have been saved to the transition
        ;; frame, and the code at the call target is required to use only the transition frame
        ;; copies when dispatching this call to the eventual callee.

        ldr         r3, =RhpFpTrashValues
        vldr        d0, [r3, #(0 * 8)]
        vldr        d1, [r3, #(1 * 8)]
        vldr        d2, [r3, #(2 * 8)]
        vldr        d3, [r3, #(3 * 8)]
        vldr        d4, [r3, #(4 * 8)]
        vldr        d5, [r3, #(5 * 8)]
        vldr        d6, [r3, #(6 * 8)]
        vldr        d7, [r3, #(7 * 8)]

        ldr         r3, =RhpIntegerTrashValues
        ldr         r2, [r3, #(2 * 4)]
        ldr         r3, [r3, #(3 * 4)]

#endif // TRASH_SAVED_ARGUMENT_REGISTERS

        ;; Make the ReturnFromUniversalTransition alternate entry 4 byte aligned
        ALIGN 4
        add         r0, sp, #DISTANCE_FROM_CHILDSP_TO_RETURN_BLOCK  ;; First parameter to target function is a pointer to the return block
        blx         r12
    LABELED_RETURN_ADDRESS ReturnFromUniversalTransition

        ;; Move the result (the target address) to r12 so it doesn't get overridden when we restore the
        ;; argument registers. Additionally make sure the thumb2 bit is set.
        orr     r12, r0, #1

        ;; Pop the PInvokeTransitionFrame
        COOP_PINVOKE_FRAME_EPILOG_NO_RETURN

        ;; Pop the transition frame pointer and alignment
        EPILOG_STACK_FREE TRANSITION_FRAMEPOINTER_AND_ALIGNMENT      ; Discard transition pointer region

        ;; Restore the argument registers.
        EPILOG_VPOP {d0-d7}
        EPILOG_STACK_FREE RETURN_BLOCK_SIZE        ; pop return block conservatively reported area
        EPILOG_POP {r0-r3}

        EPILOG_BRANCH_REG r12

        NESTED_END RhpUniversalTransition

        END
