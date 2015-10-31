;;
;; Copyright (c) Microsoft. All rights reserved.
;; Licensed under the MIT license. See LICENSE file in the project root for full license information. 
;;

#include "AsmMacros.h"

#define COUNT_ARG_REGISTERS (4)
#define INTEGER_REGISTER_SIZE (4)
#define ARGUMENT_REGISTERS_SIZE (COUNT_ARG_REGISTERS * INTEGER_REGISTER_SIZE)

;; Largest return block is 4 doubles
#define RETURN_BLOCK_SIZE (32) 

#define COUNT_FLOAT_ARG_REGISTERS (8)
#define FLOAT_REGISTER_SIZE (8)
#define FLOAT_ARG_REGISTERS_SIZE (COUNT_FLOAT_ARG_REGISTERS * FLOAT_REGISTER_SIZE)
#define PINVOKE_TRANSITION_BLOCK_SIZE (12*INTEGER_REGISTER_SIZE)

#define PINVOKE_TRANSITION_FRAME_SP_OFFSET (0)
#define PINVOKE_TRANSITION_FRAME_FLAGS (4 * 7)

#define TRANSITION_FRAMEPOINTER_AND_ALIGNMENT 8

#define TRANSITION_FRAME_STACK_OFFSET (TRANSITION_FRAMEPOINTER_AND_ALIGNMENT)
#define FLOATING_ARGS_STACK_OFFSET (TRANSITION_FRAME_STACK_OFFSET + PINVOKE_TRANSITION_BLOCK_SIZE)
#define RETURN_BLOCK_STACK_OFFSET (FLOATING_ARGS_STACK_OFFSET + FLOAT_ARG_REGISTERS_SIZE)
#define ARG_REGISTERS_OFFSET (RETURN_BLOCK_STACK_OFFSET + RETURN_BLOCK_SIZE)
#define INITIAL_STACK_POINTER_OFFSET (ARG_REGISTERS_OFFSET + ARGUMENT_REGISTERS_SIZE)

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
;;        in the neg space of the TransitionBlock pointer.
;; D7
;; D6
;; D5
;; D4
;; D3
;; D2
;; D1
;; D0
;;---------------------
;; PINVOKE TRANSITION_FRAME
;;---------------------
;; Pointer to Transition Frame
;; Alignment Padding (4 bytes)

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

        ;; Build PInvokeTransitionFrame. This is used to ensure all arguments are reported conservatively.

        PROLOG_STACK_ALLOC 4        ; Align the stack and save space for caller's SP
        PROLOG_PUSH {r4-r6,r8-r10}  ; Save preserved registers
        PROLOG_STACK_ALLOC 8        ; Save space for flags and Thread*
        PROLOG_PUSH {r7}            ; Save caller's FP
        PROLOG_PUSH {r11,lr}        ; Save caller's frame-chain pointer and PC

        ;; Build space to save pointer to transition frame, and setup frame pointer
        PROLOG_STACK_SAVE r7
        PROLOG_STACK_ALLOC TRANSITION_FRAMEPOINTER_AND_ALIGNMENT    ; Space for transition frame pointer plus stack alignment padding

        ;; Compute Transition frame address and store into frame
        add         r1, sp, #(TRANSITION_FRAME_STACK_OFFSET)
        str         r1, [r7, #MANAGED_CALLOUT_THUNK_TRANSITION_FRAME_POINTER_OFFSET]

        ;; Compute SP value at entry to this method and save it in the last slot of the frame (slot #11).
        add         r1, sp, #(INITIAL_STACK_POINTER_OFFSET)
        str         r1, [sp, #(TRANSITION_FRAME_STACK_OFFSET + (11 * 4))]

        ;; Record the bitmask of saved registers in the frame (slot #4).
        mov         r1, #DEFAULT_FRAME_SAVE_FLAGS
        str         r1, [sp, #(TRANSITION_FRAME_STACK_OFFSET + (4 * 4))]

        ;; Setup the arguments to the transition thunk.
        mov         r1, r3

        ;; Make the ReturnFromUniversalTransition alternate entry 4 byte aligned
        ALIGN 4
        add         r0, sp, #(RETURN_BLOCK_STACK_OFFSET) ; First parameter to target function is a pointer to the return block
        blx         r12
        ALTERNATE_ENTRY ReturnFromUniversalTransition

        ;; Move the result (the target address) to r12 so it doesn't get overridden when we restore the
        ;; argument registers. Additionally make sure the thumb2 bit is set.
        orr     r12, r0, #1

        ;; Pop the transition frame pointer and alignment
        EPILOG_STACK_FREE TRANSITION_FRAMEPOINTER_AND_ALIGNMENT      ; Discard transition pointer region

        ;; Pop the PInvokeTransitionFrame
        EPILOG_POP  {r11,lr}        ; Restore caller's frame-chain pointer and PC (return address)
        EPILOG_POP  {r7}            ; Restore caller's FP
        EPILOG_STACK_FREE 8         ; Discard flags and Thread*
        EPILOG_POP  {r4-r6,r8-r10}  ; Restore preserved registers
        EPILOG_STACK_FREE 4         ; Discard caller's SP and stack alignment padding

        ;; Restore the argument registers.
        EPILOG_VPOP {d0-d7}
        EPILOG_STACK_FREE RETURN_BLOCK_SIZE        ; pop return block conservatively reported area
        EPILOG_POP {r0-r3}

        EPILOG_BRANCH_REG r12

        NESTED_END RhpUniversalTransition

        END
